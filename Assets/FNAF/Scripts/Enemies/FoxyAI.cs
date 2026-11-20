using UnityEngine;
using FNAF.Core;
using FNAF.Data;
using FNAF.Monitor;
using FNAF.Office;

namespace FNAF.Enemies
{
    /// <summary>
    /// 狐狸（红狐）：蹲守海盗湾的特殊机制——
    /// 阶段 0 帘子拉严（完全看不见）→ 1 帘微开探头 → 2 帘全开将出未出 → 3 冲刺。
    /// 监控看着海盗湾会重置他的耐心计时（推迟阶段推进）；
    /// 阶段 3：狐狸从海盗湾消失、现身西走廊向办公室狂奔（前倾+颠簸的跑步动画+循环脚步声），
    /// 冲刺时长就是关门防御窗口（默认 6 秒）：窗口内关上左门=撞门 3 秒后撤退回阶段 0 并休整，否则跳杀。
    /// 撞门时长与敲门音对齐（3 秒），撞门期间把门打开=跳杀。
    /// </summary>
    public class FoxyAI : AnimatronicAI
    {
        [Header("狐狸专属 - 帷幕")]
        [Tooltip("海盗湾左帘（随阶段滑动开合）")]
        [SerializeField] private Transform curtainLeft;
        [Tooltip("海盗湾右帘")]
        [SerializeField] private Transform curtainRight;
        [Tooltip("帷幕滑动速度（米/秒）")]
        [SerializeField] private float curtainSlideSpeed = 2.5f;

        [Header("狐狸专属 - 冲刺")]
        [Tooltip("冲刺总时长（秒）＝玩家的关门防御窗口")]
        [SerializeField] private float sprintDuration = 6f;
        [Tooltip("被监控注视一次后，额外延迟下次判定的秒数")]
        [SerializeField] private float watchedCooldown = 4f;
        [Tooltip("冲刺被门挡住后的休整时间（秒，期间不推进阶段）")]
        [SerializeField] private float retreatCooldown = 8f;
        [Tooltip("跑步脚步声（2D 循环，冲刺期间播放）")]
        [SerializeField] private AudioSource footstepSource;
        [Tooltip("跑步身体前倾角（度）")]
        [SerializeField] private float runLeanAngle = 14f;
        [Tooltip("跑步颠簸幅度（米）")]
        [SerializeField] private float runBobAmplitude = 0.07f;
        [Tooltip("跑步颠簸频率（次/秒，与脚步声节奏一致）")]
        [SerializeField] private float runBobFrequency = 5f;

        // 各阶段帷幕半宽：0 关严（±1.15 中央合拢）/ 1 微开（±1.5）/ 2、3 全开（±1.85 贴墙）
        private static readonly float[] CurtainXByStage = { 1.15f, 1.5f, 1.85f, 1.85f };

        private int stage;

        /// <summary>海盗湾蓄力阶段：0 关严 / 1 微开 / 2 全开（循环唱歌，冲刺前兆）/ 3 冲刺中。每次变化广播 FoxyStageChanged。</summary>
        public int Stage
        {
            get => stage;
            private set
            {
                if (stage == value) return;
                stage = value;
                GameEvents.RaiseFoxyStageChanged(value);
            }
        }

        private bool sprinting;
        private float sprintTimer;
        private float cooldownTimer;
        private Vector3 runStart, runEnd;
        private float sprintYaw;
        private float bobPhase;

        protected override void ApplyNightConfig(NightConfig config) => aiLevel = config.FoxyAI;

        // 第一夜整夜按兵不动（新手保护夜）；第 2/3 夜 2AM/1AM 起步，第 4 夜仍是 1AM，第 5 夜起 12AM
        protected override int BaseStartHour => -1;

        // 覆写起始时间：-1 = 本夜不行动（基类已在 ResetState/OnHourChanged 约定该语义）
        protected override int ComputeActiveFromHour(int night)
        {
            if (night >= 7) return 0;          // 自定义夜（7）不设限
            if (night <= 1) return -1;         // 第一夜整夜沉睡
            if (night <= 3) return 4 - night;  // n2→2AM、n3→1AM
            if (night == 4) return 1;          // 第 4 夜仍是 1AM（用户指定）
            return 0;                          // 第 5 夜起 12AM
        }

        protected override void ResetState()
        {
            base.ResetState();
            Stage = 0;
            sprinting = false;
            sprintTimer = 0f;
            cooldownTimer = 0f;
            if (bangRoutine != null) { StopCoroutine(bangRoutine); bangRoutine = null; } // 撞门协程跨夜取消
            StopFootsteps();
            CurrentRoomId = RoomIds.PirateCove;
            MoveToRoom(RoomIds.PirateCove, 0);
        }

        protected override bool TryMove()
        {
            if (Stage >= 3) return false;

            Stage++;
            ApplyStageVisual();
            return true;
        }

        protected override void RollForMovement()
        {
            // 被注视时重置耐心：推迟判定并额外冷却
            if (IsBeingWatched())
            {
                cooldownTimer = Mathf.Max(cooldownTimer, watchedCooldown);
                return;
            }

            base.RollForMovement();
        }

        private void ApplyStageVisual()
        {
            if (body == null) return;

            if (Stage >= 3)
            {
                BeginSprint();
                return;
            }

            body.SetVisible(true);
            MoveToRoom(RoomIds.PirateCove, Stage);
        }

        private void BeginSprint()
        {
            // 狐狸从海盗湾消失（空湾+全开帘），现身西走廊北端，沿走廊向办公室狂奔
            RoomDefinition hall = roomRegistry != null ? roomRegistry.GetById(RoomIds.WestHall) : null;
            if (hall == null || hall.EnemyStops == null || hall.EnemyStops.Length == 0 || body == null)
            {
                // 站位缺失时不硬冲（否则玩偶会被瞬移到世界原点）：退回阶段 2，下个判定周期重试
                Debug.LogWarning("[FoxyAI] 西走廊站位缺失，本次冲刺取消，退回阶段 2 稍后重试");
                Stage = 2;
                return;
            }

            Transform hallStop = hall.EnemyStops[0];
            runStart = hallStop.position - hallStop.forward * 3.2f;
            runEnd = hallStop.position + hallStop.forward * 3.5f;
            sprintYaw = hallStop.eulerAngles.y;

            CurrentRoomId = RoomIds.WestHall; // 冲刺直接改房间（设计豁免：不参与房间互斥，走廊视作通道）
            body.SetVisible(true);
            body.transform.SetPositionAndRotation(runStart, Quaternion.Euler(0f, sprintYaw, 0f));
            sprinting = true;
            sprintTimer = sprintDuration;
            bobPhase = 0f;
            if (footstepSource != null && !footstepSource.isPlaying) footstepSource.Play();
        }

        protected override void Update()
        {
            UpdateCurtains(); // 未激活期间也滑动帘子：上一夜结束时敞开的帘要在沉睡期归位，否则误导玩家

            if (!active)
            {
                StopFootsteps();
                return;
            }

            // 冲刺阶段：沿走廊奔跑，倒计时耗尽时检查左门
            if (sprinting)
            {
                UpdateSprint();
                return;
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            base.Update();
        }

        private void UpdateSprint()
        {
            sprintTimer -= Time.deltaTime;

            if (body != null)
            {
                // 跑步动画：沿走廊线性推进 + 身体前倾 + 按脚步节奏上下颠簸
                float t = 1f - Mathf.Clamp(sprintTimer / sprintDuration, 0f, 1f);
                Vector3 pos = Vector3.Lerp(runStart, runEnd, t);
                bobPhase += runBobFrequency * Mathf.PI * Time.deltaTime;
                pos.y += Mathf.Abs(Mathf.Sin(bobPhase)) * runBobAmplitude;
                body.transform.position = pos;
                body.transform.rotation = Quaternion.Euler(runLeanAngle, sprintYaw, 0f);
            }

            if (sprintTimer <= 0f)
            {
                sprinting = false;
                StopFootsteps();

                if (DoorController.IsDoorClosed(DoorSide.Left))
                {
                    // 门关了：撞门 3 秒（与敲门音对齐），期间把门打开=跳杀，敲完直接离开
                    bangRoutine = StartCoroutine(BangThenLeave());
                }
                else
                {
                    AttackPlayer();
                }
            }
        }

        [Tooltip("撞门时长（秒）＝敲门音长度（3 秒），期间开门=跳杀")]
        [SerializeField] private float bangDuration = 3f;
        private Coroutine bangRoutine; // 撞门协程句柄：跨夜/退场时取消，防止 6AM 后还在敲

        /// <summary>撞门 3 秒（与敲门音对齐）：期间左门被打开=跳杀；敲完直接撤退回海盗湾，不再滞留。</summary>
        private System.Collections.IEnumerator BangThenLeave()
        {
            GameEvents.RaiseFoxyBanged(true);
            float t = 0f;
            while (t < bangDuration)
            {
                t += Time.deltaTime;
                // 撞门中把门打开：狐狸当场跳杀
                if (!DoorController.IsDoorClosed(DoorSide.Left))
                {
                    GameEvents.RaiseFoxyBanged(false);
                    bangRoutine = null;
                    AttackPlayer();
                    yield break;
                }
                yield return null;
            }
            GameEvents.RaiseFoxyBanged(false);
            bangRoutine = null;

            if (!active) yield break; // 撞门期间夜已结束（6AM/跳杀），不再撤退动画
            FinishDoorHold(); // 敲完门就走：直接回海盗湾（阶段 0）休整
        }

        /// <summary>滞留结束：狐狸不贴窗，直接退回海盗湾（阶段 0）休整。</summary>
        protected override void FinishDoorHold()
        {
            atDoor = false;
            Stage = 0;
            cooldownTimer = retreatCooldown;
            MoveToRoom(RoomIds.PirateCove, 0);
        }

        private void UpdateCurtains()
        {
            if (curtainLeft == null || curtainRight == null) return;

            float target = CurtainXByStage[Mathf.Clamp(Stage, 0, CurtainXByStage.Length - 1)];
            SlideCurtain(curtainLeft, -target);
            SlideCurtain(curtainRight, target);
        }

        private void SlideCurtain(Transform curtain, float targetX)
        {
            Vector3 p = curtain.localPosition;
            if (Mathf.Approximately(p.x, targetX)) return;
            p.x = Mathf.MoveTowards(p.x, targetX, curtainSlideSpeed * Time.deltaTime);
            curtain.localPosition = p;
        }

        private void StopFootsteps()
        {
            if (footstepSource != null && footstepSource.isPlaying) footstepSource.Stop();
        }

        protected override bool IsBeingWatched()
        {
            // 狐狸的"被注视"只看海盗湾这一个房间
            return MonitorController.Instance != null
                && MonitorController.Instance.IsRoomOnFeed(RoomIds.PirateCove);
        }

        // 狐狸不走标准门袭流程
        protected override void OnEnterDoorAttack(DoorSide side) { }
        protected override void OnRetreatFromDoor() { }
    }
}
