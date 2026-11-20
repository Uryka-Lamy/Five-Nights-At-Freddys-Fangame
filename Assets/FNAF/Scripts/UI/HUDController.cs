using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;

namespace FNAF.UI
{
    /// <summary>
    /// 游戏 HUD：右上角时间与夜数、左下角电量百分比与 1-5 格用电量条。
    /// 全部数据来自事件总线（小时变更/电力变更/夜晚开始），无轮询。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("引用（场景生成器填充）")]
        [Tooltip("时间文字，如 12 AM")]
        [SerializeField] private Text timeText;

        [Tooltip("夜数文字，如 第 1 夜")]
        [SerializeField] private Text nightText;

        [Tooltip("电量文字，如 剩余电量: 99%")]
        [SerializeField] private Text powerText;

        [Tooltip("用电量条（5 格，从低到高）")]
        [SerializeField] private Image[] usageBars;

        [Tooltip("夜晚时钟（读取小时显示）")]
        [SerializeField] private NightClock nightClock;

        private static readonly Color[] ActiveColors =
        {
            new Color(0.35f, 0.9f, 0.35f), new Color(0.35f, 0.9f, 0.35f),
            new Color(0.95f, 0.85f, 0.25f), new Color(0.95f, 0.6f, 0.15f),
            new Color(0.95f, 0.2f, 0.15f)
        };

        private static readonly Color InactiveColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        private void OnEnable()
        {
            GameEvents.HourChanged += OnHourChanged;
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.PowerChanged += OnPowerChanged;
        }

        private void OnDisable()
        {
            GameEvents.HourChanged -= OnHourChanged;
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.PowerChanged -= OnPowerChanged;
        }

        private void OnHourChanged(int hour)
        {
            if (timeText != null)
                timeText.text = hour == 0 ? "12 AM" : $"{hour} AM";
        }

        private void OnNightStarted(int night)
        {
            if (nightText != null) nightText.text = $"第 {night} 夜"; // 自定义夜=第 7 夜（原版同款显示）
            if (timeText != null && nightClock != null) timeText.text = nightClock.CurrentHourDisplay;
        }

        private void OnPowerChanged(float power, int usage)
        {
            if (powerText != null)
                powerText.text = $"剩余电量: {Mathf.Ceil(power)}%";
            if (usageBars == null) return;
            for (int i = 0; i < usageBars.Length; i++)
            {
                if (usageBars[i] != null)
                    usageBars[i].color = i < usage ? ActiveColors[Mathf.Clamp(i, 0, ActiveColors.Length - 1)] : InactiveColor;
            }
        }
    }
}
