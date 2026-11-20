using UnityEngine;
using FNAF.Core;
using FNAF.Data;
using FNAF.Monitor;
using FNAF.Office;

namespace FNAF.Enemies
{
    /// <summary>
    /// 玩偶 AI 基类：实现 FNAF 1 的核心判定模型——
    /// 每隔 moveOpportunityInterval 秒掷一次 20 面骰，点数 <= AI 等级才行动。
    /// 沿 RoomRegistry 的邻接图在房间之间传送（FNAF 原版没有走路动画，切镜头玩偶就换了位置）。
    /// 到达门位（虚拟房间 LeftDoor/RightDoor）后进入"门袭"状态：
    /// 在 doorAttackWindow 秒内关门则玩偶撤退，否则玩家被抓。
    /// 行动起始时间随夜数提前（原版第一夜邦尼 2AM 起、越难越早），到点前不掷骰；
    /// 每次移动广播 AnimatronicMoved，监控画面借此切强噪点遮挡移动瞬间。
    /// </summary>
    public abstract class AnimatronicAI : MonoBehaviour
    {
        [Header("身份与数据源")]
        [Tooltip("玩偶 ID（PlayerCaught 事件会带上它，跳杀系统按 ID 找身体）")]
        [SerializeField] protected string enemyId;
        [Tooltip("积木身体（负责视觉表现）")]
        [SerializeField] protected AnimatronicBody body;
        [Tooltip("房间注册表（移动图数据源）")]
        [SerializeField] protected RoomRegistry roomRegistry;

        [Header("AI 行为")]
        [Tooltip("AI 等级 0-20（NightConfig 覆盖）")]
        [SerializeField, Range(0, 20)] protected int aiLevel = 1;
        [Tooltip("行动判定间隔（秒）")]
        [SerializeField] protected float moveOpportunityInterval = 5f;
        [Tooltip("到达门口后玩家还剩多少秒可以关门（不关就跳杀）")]
        [SerializeField] protected float doorAttackWindow = 5f;

        public string EnemyId => enemyId;
        public string CurrentRoomId { get; protected set; }

        protected bool active;
        protected bool atDoor;
        protected float doorTimer;

        [Header("关门滞留（关门后玩偶不会立刻消失）")]
        [Tooltip("关门后玩偶在门外继续滞留的最短整数秒（含）")]
        [SerializeField] protected int doorHoldMinSeconds = 3;
        [Tooltip("关门后玩偶在门外继续滞留的最长整数秒（含，随机取整数 3/4/5…）")]
        [SerializeField] protected int doorHoldMaxSeconds = 5;
        [Tooltip("滞留期间玩家开门后，多少秒内没重新关门就跳杀")]
        [SerializeField] protected float doorReopenGrace = 1.5f;
        [Tooltip("贴窗徘徊最短秒数：滞留结束后玩偶挪到窗外贴脸看你（纯视觉威慑，期间开门也安全）")]
        [SerializeField] protected float windowStareMinSeconds = 2f;
        [Tooltip("贴窗徘徊最长秒数，到点后玩偶退场")]
        [SerializeField] protected float windowStareMaxSeconds = 4f;
        private float windowStareTimer;

        /// <summary>门袭子状态：0=门口等待（开门，倒计时到点跳杀）/ 1=关门滞留（倒计时走完才走，不重置）/ 2=开门宽限（1.5 秒内没重新关门=跳杀）。</summary>
        private int doorHoldState;
        private float doorHoldTimer;
        private float doorGraceTimer;

        /// <summary>全部活着的玩偶实例（房间互斥查询用：同一房间同一时间只允许一具玩偶）。</summary>
        private static readonly System.Collections.Generic.List<AnimatronicAI> all = new System.Collections.Generic.List<AnimatronicAI>();

        protected DoorSide doorSide;
        [Tooltip("本夜从几点（0=12AM）开始行动：正篇=起始小时逐夜提前 1，自定义夜=12AM")]
        protected int activeFromHour;
        private int currentNight; // 本夜编号（1~6 正篇享受每小时递增，自定义夜不爬升）
        private float moveTimer;

        /// <summary>第一夜的起始行动小时（0=12AM）：邦尼 2 / 奇卡 3 / 狐狸 3 / 弗雷迪 4。</summary>
        protected abstract int BaseStartHour { get; }

        /// <summary>查询某侧门口当前是否有玩偶（门袭站在门口）——开灯发现音效用。</summary>
        public static bool IsAnyoneAtDoor(DoorSide side)
        {
            for (int i = 0; i < all.Count; i++)
            {
                AnimatronicAI ai = all[i];
                if (ai != null && ai.doorSide == side && ai.atDoor) return true;
            }
            return false;
        }

        protected virtual void OnEnable()
        {
            all.Add(this);
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.PowerOut += OnPowerOut;
            GameEvents.MenuReturned += OnMenuReturned;
            GameEvents.HourChanged += OnHourChanged;
        }

        protected virtual void OnDisable()
        {
            all.Remove(this);
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.PowerOut -= OnPowerOut;
            GameEvents.MenuReturned -= OnMenuReturned;
            GameEvents.HourChanged -= OnHourChanged;
        }

        private void OnNightStarted(int night)
        {
            NightConfig config = GameManager.Instance != null
                ? GameManager.Instance.GetNightConfig(night)
                : null;
            currentNight = night;
            activeFromHour = ComputeActiveFromHour(night);
            ResetState();
            if (config != null) ApplyNightConfig(config);
        }

        /// <summary>起始小时：正篇逐夜提前 1 小时（第 1 夜=BaseStartHour），自定义夜（7）12AM 即动（night>=7 短路）；狐狸覆写（第一夜整夜沉睡）。</summary>
        protected virtual int ComputeActiveFromHour(int night)
        {
            if (night >= 7) return 0;
            return Mathf.Max(0, BaseStartHour - (night - 1));
        }

        private void OnHourChanged(int hour)
        {
            // 到了起始小时（如第一夜邦尼 2AM）→ 解锁行动；activeFromHour < 0 = 本夜整夜沉睡
            if (!active && activeFromHour >= 0 && hour >= activeFromHour) active = true;

            // 原版式整夜爬升：2AM 兔 +1，3AM/4AM 兔鸡狐各 +1；熊整夜恒定
            if (currentNight >= 1 && currentNight <= 6) ApplyHourlyRamp(hour);
            // 自定义夜（7）：AI 0 的玩偶不是关掉，而是沉睡——每个整点苏醒一格，到 1 后开始行动
            else if (currentNight == 7 && aiLevel == 0 && hour >= 1 && hour <= 5) aiLevel = 1;
        }

        /// <summary>每小时 AI 递增（原版机制）：默认 3AM/4AM 各 +1（20 封顶）；兔覆写含 2AM；熊覆写为空。</summary>
        protected virtual void ApplyHourlyRamp(int hour)
        {
            if (hour == 3 || hour == 4) aiLevel = Mathf.Min(20, aiLevel + 1);
        }

        private void OnNightCompleted(int night) => active = false;
        private void OnPlayerCaught(string caughtId) => active = false;
        private void OnMenuReturned() => active = false; // ESC 退出本夜：主菜单下玩偶全部冻结
        private void OnPowerOut() => OnPowerOutInternal();

        protected virtual void OnPowerOutInternal()
        {
            // 断电后普通玩偶停手，弗雷迪子类会接管处刑流程
            active = false;
        }

        protected virtual void ResetState()
        {
            // 12AM 起的立即行动；activeFromHour > 0 先"沉睡"到点解锁；-1 = 整夜不行动
            active = activeFromHour == 0;
            atDoor = false;
            doorSide = default;
            doorHoldState = 0;
            doorHoldTimer = 0f;
            doorGraceTimer = 0f;
            windowStareTimer = 0f;
            moveTimer = 0f;
            eyesGlowing = false; // 跨夜重置：否则新夜首次注视可能不亮眼睛
            if (body != null)
            {
                body.SetVisible(true);
                body.SetEyesGlowing(false);
            }
        }

        /// <summary>从夜晚配置读取本玩偶的 AI 等级。</summary>
        protected abstract void ApplyNightConfig(NightConfig config);

        protected virtual void Update()
        {
            if (!active) return;

            if (atDoor)
            {
                UpdateDoorAttack();
                return;
            }

            moveTimer += Time.deltaTime;
            if (moveTimer >= moveOpportunityInterval)
            {
                moveTimer = 0f;
                RollForMovement();
            }

            // 被监控注视时眼睛发光（FNAF 的"它在看你"）
            bool watched = IsBeingWatched();
            if (watched != eyesGlowing && body != null)
            {
                eyesGlowing = watched;
                body.SetEyesGlowing(watched);
            }
        }

        private bool eyesGlowing;

        /// <summary>掷骰：1-20 点数 <= AI 等级则行动。</summary>
        protected virtual void RollForMovement()
        {
            int roll = Random.Range(1, 21);
            if (roll <= aiLevel) TryMove();
        }

        /// <summary>行动逻辑：移动/进攻/特殊行为，子类实现。</summary>
        protected abstract bool TryMove();

        /// <summary>传送到指定房间的第 stopIndex 个站位。force=true 绕过房间互斥（初始站位/断电处刑用）。</summary>
        protected void MoveToRoom(string roomId, int stopIndex = 0, bool force = false)
        {
            // 房间互斥：同一房间同一时间只能有 1 个玩偶，被占则本次移动作废（下个判定周期再试）
            if (!force && roomId != CurrentRoomId && IsRoomOccupied(roomId))
            {
                return;
            }

            string fromRoomId = CurrentRoomId;
            CurrentRoomId = roomId;

            RoomDefinition room = roomRegistry != null ? roomRegistry.GetById(roomId) : null;
            Transform stop = null;
            if (room != null && room.EnemyStops != null && room.EnemyStops.Length > 0)
            {
                stop = room.EnemyStops[Mathf.Clamp(stopIndex, 0, room.EnemyStops.Length - 1)];
            }
            else if (roomId != RoomIds.LeftDoor && roomId != RoomIds.RightDoor)
            {
                Debug.LogWarning($"[{enemyId}] 房间 {roomId} 不存在或没有站位");
            }

            if (stop != null && body != null)
            {
                body.transform.SetPositionAndRotation(stop.position, stop.rotation);
            }

            if (roomId == RoomIds.LeftDoor) EnterDoorAttack(DoorSide.Left);
            else if (roomId == RoomIds.RightDoor) EnterDoorAttack(DoorSide.Right);
            else atDoor = false;

            // 广播移动：你正看着的摄像头里这具玩偶离开时，监控切强噪点遮挡移动瞬间（原版体验）
            GameEvents.RaiseAnimatronicMoved(enemyId, fromRoomId, roomId);
        }

        /// <summary>进入门袭状态：站到门口，开始倒计时。</summary>
        protected virtual void EnterDoorAttack(DoorSide side)
        {
            atDoor = true;
            doorSide = side;
            doorTimer = doorAttackWindow;
            doorHoldState = 0;
            OnEnterDoorAttack(side);
        }

        protected virtual void OnEnterDoorAttack(DoorSide side) { }

        /// <summary>门袭三态：门口等待 → 关门滞留 →（开门时）宽限。关门≠绝对安全，开门≠立刻安全。</summary>
        private void UpdateDoorAttack()
        {
            bool closed = DoorController.IsDoorClosed(doorSide);

            switch (doorHoldState)
            {
                case 0: // 门口等待：玩家关门 → 进入滞留；一直不关 → 倒计时到点跳杀
                    if (closed)
                    {
                        EnterDoorHold();
                        return;
                    }
                    doorTimer -= Time.deltaTime;
                    if (doorTimer <= 0f) AttackPlayer();
                    else UpdateDoorAttackTick();
                    break;

                case 1: // 关门滞留：玩偶在门外继续待着，滞留倒计时走完才走（期间再关门不重置）
                    if (!closed)
                    {
                        doorHoldState = 2;
                        doorGraceTimer = doorReopenGrace;
                        return;
                    }
                    doorHoldTimer -= Time.deltaTime;
                    if (doorHoldTimer <= 0f) FinishDoorHold();
                    break;

                case 2: // 开门宽限：1.5 秒内重新关门 → 回滞留（滞留倒计时继续，不重置）；超时 → 跳杀
                    if (closed)
                    {
                        doorHoldState = 1;
                        return;
                    }
                    doorGraceTimer -= Time.deltaTime;
                    if (doorGraceTimer <= 0f) AttackPlayer();
                    break;

                case 3: // 窗边贴脸徘徊：玩偶已从门口挪到窗贴脸位，纯视觉威慑，期间开关门都安全；徘徊到点才退场
                    windowStareTimer -= Time.deltaTime;
                    if (windowStareTimer <= 0f) FinishWindowStare();
                    break;
            }
        }

        /// <summary>玩家关门挡住 → 进入滞留：随机整数秒（3/4/5…，熊改 5~7）后才离开。</summary>
        private void EnterDoorHold()
        {
            doorHoldState = 1;
            doorHoldTimer = Random.Range(doorHoldMinSeconds, doorHoldMaxSeconds + 1);
            OnEnterDoorHold();
        }

        /// <summary>滞留开始钩子（狐狸砸门声等）。</summary>
        protected virtual void OnEnterDoorHold() { }

        /// <summary>以固定秒数进入关门滞留（狐狸砸完门调用）；滞留期间开门同样触发宽限跳杀。</summary>
        protected void BeginDoorHold(float seconds)
        {
            doorHoldState = 1;
            doorHoldTimer = seconds;
        }

        /// <summary>滞留结束（待满离开）→ 进入窗边贴脸徘徊：玩偶挪到窗外贴脸看你 2~4 秒（原版氛围），
        /// 期间保持"在门口"判定（开灯能看见+发现音），但不攻击——然后才真正退场。
        /// 狐狸覆写本方法（撞完门直接回海盗湾，不贴窗）。</summary>
        protected virtual void FinishDoorHold()
        {
            EnterWindowStare();
        }

        /// <summary>挪到窗贴脸位开始徘徊。不经过 MoveToRoom（避免重置门袭三态机），只摆身体位置；
        /// 广播同房间移动用于"灯亮着才贴窗→发现音"判定。atDoor 保持 true：按灯也能看见它。</summary>
        private void EnterWindowStare()
        {
            doorHoldState = 3;
            windowStareTimer = Random.Range(windowStareMinSeconds, windowStareMaxSeconds);

            RoomDefinition room = roomRegistry != null ? roomRegistry.GetById(CurrentRoomId) : null;
            // 门房间站位：0=门口位，1=窗贴脸位
            if (room != null && room.EnemyStops != null && room.EnemyStops.Length > 1 && body != null)
            {
                Transform stop = room.EnemyStops[1];
                body.transform.SetPositionAndRotation(stop.position, stop.rotation);
            }

            GameEvents.RaiseAnimatronicMoved(enemyId, CurrentRoomId, CurrentRoomId); // 灯亮时播发现音
        }

        /// <summary>贴脸徘徊结束：脱离门袭状态并退场（退场目标被房间互斥挡下时等下个判定周期重试）。</summary>
        private void FinishWindowStare()
        {
            atDoor = false;
            doorHoldState = 0;
            OnRetreatFromDoor();
        }

        protected virtual void UpdateDoorAttackTick() { }

        /// <summary>门袭倒计时耗尽（玩家没关门）→ 玩家被抓。</summary>
        protected virtual void AttackPlayer()
        {
            active = false;
            GameEvents.RaisePlayerCaught(enemyId);
        }

        protected abstract void OnRetreatFromDoor();

        /// <summary>监控画面是否正在看本玩偶所在的房间。</summary>
        protected virtual bool IsBeingWatched()
        {
            return MonitorController.Instance != null
                && MonitorController.Instance.IsRoomOnFeed(CurrentRoomId);
        }

        /// <summary>目标房间是否被其他玩偶占着（同一房间同一时间只允许一具玩偶）。</summary>
        protected bool IsRoomOccupied(string roomId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                AnimatronicAI other = all[i];
                if (other != null && other != this
                    && other.gameObject.activeInHierarchy
                    && other.CurrentRoomId == roomId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>全局房间 ID 契约（与 RoomDefinition 注释一致，AI/场景生成器共用）。</summary>
    public static class RoomIds
    {
        public const string ShowStage = "ShowStage";
        public const string DiningArea = "DiningArea";
        public const string Backstage = "Backstage";
        public const string PirateCove = "PirateCove";
        public const string WestHall = "WestHall";
        public const string WestHallCorner = "WestHallCorner";
        public const string SupplyCloset = "SupplyCloset";
        public const string EastHall = "EastHall";
        public const string EastHallCorner = "EastHallCorner";
        public const string Restrooms = "Restrooms";
        public const string Kitchen = "Kitchen";

        public const string LeftDoor = "LeftDoor";
        public const string RightDoor = "RightDoor";
    }
}
