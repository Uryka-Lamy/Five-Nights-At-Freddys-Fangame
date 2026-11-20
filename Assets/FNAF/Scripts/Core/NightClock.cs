using UnityEngine;
using FNAF.Data;

namespace FNAF.Core
{
    /// <summary>
    /// 夜晚时钟：12 AM → 6 AM 推进。每个游戏小时耗费 NightConfig.HourDuration 秒，
    /// 到 6 AM 时宣布夜晚胜利。
    /// </summary>
    public class NightClock : MonoBehaviour
    {
        /// <summary>当前小时：0 = 12AM，1-5 = 1AM-5AM</summary>
        public int CurrentHour { get; private set; }

        public string CurrentHourDisplay => CurrentHour == 0 ? "12 AM" : $"{CurrentHour} AM";

        /// <summary>本小时已经过的秒数（0-1 归一化），供 UI 画进度用</summary>
        public float HourProgress { get; private set; }

        [Header("作弊（原版 FNAF1 同款：同时按住 C+D+小键盘+ 直接跳到 6AM）")]
        [Tooltip("三键同按立即通关当前夜（自定义夜不生效）")]
        [SerializeField] private bool cheatEnabled = true;

        private float hourDuration = 75f;
        private float hourTimer;
        private bool running;
        private bool cheatAllowed;

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnPlayerCaught(string enemyId) => running = false;
        private void OnMenuReturned() => running = false; // ESC 退出本夜：菜单下时钟必须停表

        private void OnNightStarted(int night)
        {
            NightConfig config = GameManager.Instance != null
                ? GameManager.Instance.GetNightConfig(night)
                : null;

            if (config == null)
            {
                Debug.LogWarning("[NightClock] 拿不到夜晚配置，时钟不启动");
                running = false;
                return;
            }

            hourDuration = Mathf.Max(1f, config.HourDuration);
            CurrentHour = 0;
            hourTimer = 0f;
            HourProgress = 0f;
            cheatAllowed = night < 7; // 自定义夜（7）不能跳过
            running = true;
            GameEvents.RaiseHourChanged(CurrentHour);
        }

        private void Update()
        {
            if (!running) return;

            // 原版同款作弊：同时按住 C + D + 小键盘+ 直接跳到 6AM，走正常通关流程（胜利动画→下一夜）；自定义夜无效
            if (cheatEnabled && cheatAllowed
                && Input.GetKey(KeyCode.C)
                && Input.GetKey(KeyCode.D)
                && Input.GetKey(KeyCode.KeypadPlus))
            {
                CompleteNight();
                return;
            }

            hourTimer += Time.deltaTime;
            HourProgress = Mathf.Clamp01(hourTimer / hourDuration);

            if (hourTimer >= hourDuration)
            {
                hourTimer = 0f;
                CurrentHour++;
                HourProgress = 0f;

                if (CurrentHour >= 6)
                {
                    CompleteNight();
                }
                else
                {
                    GameEvents.RaiseHourChanged(CurrentHour);
                }
            }
        }

        /// <summary>通关本夜：先广播 6AM（HUD 显示 6 AM），再发夜结束。仅 Playing 阶段生效，避免与同帧跳杀竞态。</summary>
        private void CompleteNight()
        {
            running = false;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Phase != GameManager.GamePhase.Playing) return; // 跳杀已抢先结算，不写通关存档
            GameEvents.RaiseHourChanged(6);
            int night = gm != null ? gm.CurrentNight : 1;
            GameEvents.RaiseNightCompleted(night);
        }
    }
}
