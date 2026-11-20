using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;
using FNAF.Data;
using FNAF.Enemies;

namespace FNAF.UI
{
    /// <summary>
    /// 自定义夜面板：FNAF 1 式 AI 等级设置（每只玩偶 - / + 调节，0-20；0=沉睡，
    /// 每个整点苏醒一格、到 1 后开始行动），
    /// 开始后用运行时临时 NightConfig（独立第 7 入口，正篇 6 夜之外）开局，不写入存档。
    /// 隐藏彩蛋：四只等级拨成 1/9/8/7（87 年咬人事件年份）→ 金熊跳杀并"崩溃"游戏。
    /// </summary>
    public class CustomNightUI : MonoBehaviour
    {
        [Header("面板（场景生成器填充）")]
        [Tooltip("自定义夜面板")]
        [SerializeField] private GameObject panel;

        [Tooltip("四只玩偶的等级数字显示（弗雷迪/邦尼/奇卡/狐狸）")]
        [SerializeField] private Text[] levelTexts;

        [Tooltip("金熊彩蛋控制器（1/9/8/7 检测用）")]
        [SerializeField] private GoldenFreddyController goldenFreddy;

        [Header("规则")]
        [Tooltip("自定义夜 AI 等级下限：0 = 沉睡（每个整点苏醒一格，到 1 后开始行动）")]
        [SerializeField] private int minLevel = 0;

        [Tooltip("最高 AI 等级")]
        [SerializeField] private int maxLevel = 20;

        [Tooltip("打开面板时的默认 AI 等级")]
        [SerializeField] private int defaultLevel = 5;

        private readonly int[] levels = new int[4];

        private void Awake()
        {
            for (int i = 0; i < levels.Length; i++) levels[i] = defaultLevel;
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            RefreshAll();
        }

        /// <summary>返回主菜单（面板按钮持久化监听入口）。</summary>
        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.ShowMenu();
        }

        /// <summary>调高指定玩偶等级（按钮持久化监听入口，参数 0-3）。</summary>
        public void OnPlus(int index)
        {
            levels[index] = Mathf.Clamp(levels[index] + 1, minLevel, maxLevel);
            Refresh(index);
        }

        /// <summary>调低指定玩偶等级（按钮持久化监听入口，参数 0-3）。</summary>
        public void OnMinus(int index)
        {
            levels[index] = Mathf.Clamp(levels[index] - 1, minLevel, maxLevel);
            Refresh(index);
        }

        /// <summary>上一次自定义夜是否为全 20 AI（通关后用于授予第 4 颗星）。</summary>
        public static bool LastRunWasAll20 { get; private set; }

        /// <summary>按当前设置开始自定义夜（按钮持久化监听入口）。</summary>
        public void StartCustomNight()
        {
            // 金熊彩蛋：1/9/8/7 = "87 年咬人事件"的年份，不开局，直接跳杀崩溃
            if (goldenFreddy != null
                && levels[0] == 1 && levels[1] == 9 && levels[2] == 8 && levels[3] == 7)
            {
                if (panel != null) panel.SetActive(false);
                goldenFreddy.Trigger1987Crash();
                return;
            }

            NightConfig config = ScriptableObject.CreateInstance<NightConfig>();
            LastRunWasAll20 = levels[0] == 20 && levels[1] == 20 && levels[2] == 20 && levels[3] == 20;
            config.NightNumber = 7;
            config.HourDuration = 75f;
            config.StartingPower = 99f;
            config.BaseDrainPerSecond = 0.104f;
            config.NightlyDrainIntervalSeconds = 3f; // 自定义夜 = 原版最快附加耗电档
            config.FreddyAI = levels[0];
            config.BonnieAI = levels[1];
            config.ChicaAI = levels[2];
            config.FoxyAI = levels[3];

            if (panel != null) panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.SetCustomNightConfig(config);
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.StartNight(7);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < levels.Length; i++) Refresh(i);
        }

        private void Refresh(int index)
        {
            if (levelTexts != null && index < levelTexts.Length && levelTexts[index] != null)
                levelTexts[index].text = levels[index].ToString();
        }
    }
}
