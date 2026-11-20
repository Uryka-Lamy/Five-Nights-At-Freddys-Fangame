using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Office
{
    /// <summary>
    /// 窗光联动：办公室窗外凹室平时只有一丝冷光（隐约轮廓），
    /// 对应侧门灯点亮时把窗内照亮——玩偶贴在窗外时按门灯才能看清那张脸（原版体验）。
    /// 挂在窗光上，由场景生成器接线。
    /// </summary>
    public class WindowLightLink : MonoBehaviour
    {
        [Tooltip("受控的窗光")]
        [SerializeField] private Light windowLight;

        [Tooltip("对应哪一侧的门灯（0=Left 左，1=Right 右）")]
        [SerializeField] private int side;

        [Tooltip("平时微光强度")]
        [SerializeField] private float idleIntensity = 0.06f;

        [Tooltip("门灯点亮时的强度")]
        [SerializeField] private float litIntensity = 1.1f;

        private void OnEnable()
        {
            GameEvents.DoorLightToggled += OnDoorLightToggled;
            if (windowLight != null) windowLight.intensity = idleIntensity;
        }

        private void OnDisable()
        {
            GameEvents.DoorLightToggled -= OnDoorLightToggled;
        }

        private void OnDoorLightToggled(DoorSide toggledSide, bool on)
        {
            if ((int)toggledSide != side || windowLight == null) return;
            windowLight.intensity = on ? litIntensity : idleIntensity;
        }
    }
}
