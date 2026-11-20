using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Enemies
{
    /// <summary>
    /// 弗雷迪（棕熊）：只走东侧固定路线，且被监控注视时完全冻结（原版核心机制）。
    /// 断电后接管"处刑流程"：出现在左门口、眼睛发光、随机倒计时后跳杀。
    /// </summary>
    public class FreddyAI : AnimatronicAI
    {
        [Header("弗雷迪专属")]
        [Tooltip("被监控注视时是否冻结（原版机制，关掉可供调试）")]
        [SerializeField] private bool freezeWhenWatched = true;

        [Tooltip("弗雷迪的门袭等待时长（与鸡/兔一致）")]
        [SerializeField] private float freddyDoorAttackWindow = 5f;

        [Tooltip("弗雷迪的行动判定间隔（鸡/兔是 5 秒，熊节奏更慢、频率更低）")]
        [SerializeField] private float freddyMoveInterval = 8f;

        [Tooltip("断电后隔多久才出现在左门外（秒，随机 5~8）")]
        [SerializeField] private float powerOutArriveMin = 5f;
        [SerializeField] private float powerOutArriveMax = 8f;

        [Tooltip("现身左门（停电神曲响起）后到跳杀的最短/最长时间（秒）")]
        [SerializeField] private float powerOutKillMinDelay = 10f;
        [SerializeField] private float powerOutKillMaxDelay = 25f;

        // 断电处刑的到达延迟状态
        private bool powerOutPending;
        private float powerOutDelayTimer;
        private bool executionActive; // 断电处刑进行中（熊现身左门、眼睛闪烁、随机跳杀窗口走表）
        private float blinkTimer;     // 眼睛闪烁计时器

        protected override void ApplyNightConfig(NightConfig config) => aiLevel = config.FreddyAI;

        // 熊不参与每小时递增——原版中弗雷迪的 AI 整夜恒定，威胁靠“监控放下才移动”的门控而非数值爬升
        protected override void ApplyHourlyRamp(int hour) { }

        // 原版第一夜弗雷迪 4AM 前后才开始行动（最晚最怂），逐夜提前
        protected override int BaseStartHour => 4;

        // 覆写起始时间：前 3 夜 4/3/2AM 不变，第 4 夜仍是 2AM，第 5 夜起固定 1AM（用户指定，不再提前）
        protected override int ComputeActiveFromHour(int night)
        {
            if (night >= 7) return 0;          // 自定义夜（7）不设限
            if (night <= 3) return 5 - night;  // n1→4AM、n2→3AM、n3→2AM
            if (night == 4) return 2;          // 第 4 夜仍是 2AM（用户指定）
            return 1;                          // 第 5 夜及以后固定 1AM
        }

        protected override void ResetState()
        {
            base.ResetState();
            doorAttackWindow = freddyDoorAttackWindow;
            moveOpportunityInterval = freddyMoveInterval; // 熊的进攻频率低于鸡/兔
            // 关门滞留：熊更有耐心，在门外多待一会儿（5~7 秒整数）
            doorHoldMinSeconds = 5;
            doorHoldMaxSeconds = 7;
            executionActive = false; // 断电处刑状态复位（眼睛闪烁停止）
            powerOutPending = false;
            CurrentRoomId = RoomIds.ShowStage;
            MoveToRoom(RoomIds.ShowStage, 0, true); // force：初始舞台站位允许多具玩偶（熊0/兔1/鸡2 错开）
        }

        protected override void Update()
        {
            // 夜晚已结束/已被抓/ESC 回菜单：处刑链整体冻结
            // （否则 6AM 后或主菜单里，断电延迟计时器会继续走、熊照样现身跳杀）
            if (!active)
            {
                powerOutPending = false;
                executionActive = false;
                return;
            }

            // 断电后延迟 5~8 秒才现身左门外（不是立刻出现）
            if (powerOutPending)
            {
                powerOutDelayTimer -= Time.deltaTime;
                if (powerOutDelayTimer <= 0f)
                {
                    powerOutPending = false;
                    BeginPowerOutExecution();
                }
                return;
            }
            base.Update();
            // 断电处刑中：双眼在黑暗里忽明忽暗（原版"停电神曲+闪脸"的还原）
            if (executionActive)
            {
                blinkTimer -= Time.deltaTime;
                if (blinkTimer <= 0f)
                {
                    blinkTimer = Random.Range(0.08f, 0.45f);
                    if (body != null) body.SetEyesGlowing(Random.value < 0.62f);
                }
            }
        }

        protected override bool TryMove()
        {
            if (powerOutPending) return false; // 断电延迟期间不行动
            if (freezeWhenWatched && IsBeingWatched()) return false;

            string next = NextRoomOnFreddyRoute();
            if (next == null) return false;
            MoveToRoom(next);
            // 原版标志音：弗雷迪每次移动都有一声低沉的"哈……哈……"，玩家靠听声追踪他
            GameEvents.RaiseFreddyMoved();
            return true;
        }

        private string NextRoomOnFreddyRoute()
        {
            switch (CurrentRoomId)
            {
                case RoomIds.ShowStage: return RoomIds.DiningArea;
                case RoomIds.DiningArea: return RoomIds.Restrooms;
                case RoomIds.Restrooms: return RoomIds.Kitchen;
                case RoomIds.Kitchen: return RoomIds.EastHall;
                case RoomIds.EastHall: return RoomIds.EastHallCorner;
                case RoomIds.EastHallCorner: return RoomIds.RightDoor;
                default: return RoomIds.DiningArea;
            }
        }

        protected override void OnPowerOutInternal()
        {
            // 断电处刑分两步：先随机等 5~8 秒才现身左门外；现身即眼睛开始闪烁+停电神曲响起，再随机 10~25 秒跳杀
            active = true;
            powerOutPending = true;
            powerOutDelayTimer = Random.Range(powerOutArriveMin, powerOutArriveMax);
        }

        /// <summary>现身左门外的小隔间，眼睛发光——玩家透过被迫敞开的门口就能看到他，倒计时耗尽即跳杀。</summary>
        private void BeginPowerOutExecution()
        {
            MoveToRoom(RoomIds.LeftDoor, 0, true); // force：断电处刑必须执行，不受房间互斥阻挡
            // MoveToRoom 已按 doorAttackWindow 起表，替换为随机时长（10~25 秒）
            doorTimer = Random.Range(powerOutKillMinDelay, powerOutKillMaxDelay);
            executionActive = true;
            blinkTimer = 0.5f; // 先亮一下，随后进入随机闪烁
            if (body != null) body.SetEyesGlowing(true);
            // 眼睛开始闪烁的这一刻才播放停电神曲（用户 2026-11-08 指定：神曲不再断电立刻响）
            GameEvents.RaisePowerOutExecutionBegan();
        }

        protected override void OnRetreatFromDoor()
        {
            MoveToRoom(RoomIds.EastHall);
        }
    }
}
