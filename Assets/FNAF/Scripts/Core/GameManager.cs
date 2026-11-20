using UnityEngine;
using FNAF.Data;

namespace FNAF.Core
{
    /// <summary>
    /// 游戏主管理器：负责夜晚的开始/结束流程、阶段状态与夜晚配置查询。
    /// 不负责具体玩法逻辑——时钟、电力、门等都各自独立工作，通过 GameEvents 通信。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GamePhase
        {
            Menu,          // 主菜单
            Playing,       // 夜晚进行中
            PowerOut,      // 电力耗尽，等待弗雷迪处刑
            Caught,        // 被跳杀，游戏结束
            NightComplete  // 撑到 6 AM，夜晚胜利
        }

        [Tooltip("夜晚配置数组，下标 0 = 第 1 夜（场景生成器自动填充）")]
        [SerializeField] private NightConfig[] nightConfigs;

        [Tooltip("运行时自定义夜配置（自定义夜面板注入，night=7 时生效）")]
        private NightConfig customNightConfig;

        public GamePhase Phase { get; private set; } = GamePhase.Menu;
        public int CurrentNight { get; private set; } = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.PowerOut += OnPowerOut;
        }

        private void OnDisable()
        {
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.PowerOut -= OnPowerOut;
        }

        public NightConfig GetNightConfig(int night)
        {
            // 第 7 夜 = 自定义夜（独立于正篇），配置由面板运行时注入；正篇共 6 夜
            if (night == 7)
            {
                if (customNightConfig != null) return customNightConfig;
                // 注入缺失绝不静默回落第 6 夜（数值错配还零告警）
                Debug.LogError("[GameManager] 开了第 7 夜但自定义夜配置未注入（应先 SetCustomNightConfig 再 StartNight(7)）");
                return null;
            }
            if (nightConfigs == null || nightConfigs.Length == 0)
            {
                Debug.LogWarning("[GameManager] 未配置任何 NightConfig");
                return null;
            }
            int index = Mathf.Clamp(night - 1, 0, nightConfigs.Length - 1);
            return nightConfigs[index];
        }

        /// <summary>注入自定义夜配置（配合 StartNight(7) 使用，配置只存内存不落盘）。</summary>
        public void SetCustomNightConfig(NightConfig config) => customNightConfig = config;

        /// <summary>由主菜单或调试入口调用，开始指定夜晚。</summary>
        public void StartNight(int night)
        {
            if (GetNightConfig(night) == null) return;
            CurrentNight = night;
            Phase = GamePhase.Playing;
            GameEvents.RaiseNightStarted(night);
        }

        /// <summary>回到主菜单阶段（不清空存档）。</summary>
        public void ReturnToMenu()
        {
            Phase = GamePhase.Menu;
            GameEvents.RaiseMenuReturned(); // 通知音频等系统掐断跨阶段残留（钟声/欢呼/跳杀音）
        }

        private void OnNightCompleted(int night)
        {
            Phase = GamePhase.NightComplete;
            // 自定义夜（7）不写存档——不解锁任何正篇夜晚
            if (night <= 6) SaveSystem.MarkNightCompleted(night);

            // 评星：通第 5/6 夜、自定义夜全 20 各 1 颗（幂等，重复通关不叠加）
            if (night == 5) SaveSystem.TryAwardStar(0);
            else if (night == 6) SaveSystem.TryAwardStar(1);
            else if (night == 7 && FNAF.UI.CustomNightUI.LastRunWasAll20) SaveSystem.TryAwardStar(2);
        }

        private void OnPlayerCaught(string enemyId)
        {
            Phase = GamePhase.Caught;
        }

        private void OnPowerOut()
        {
            Phase = GamePhase.PowerOut;
        }
    }
}
