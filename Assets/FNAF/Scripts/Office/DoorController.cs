using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Office
{
    /// <summary>
    /// 一扇安全门：金属门板下滑关闭/上收回开，门灯照亮门外走廊。
    /// 门与灯的开关方法设计为 public，由场景生成器接线到 UI 按钮（Button.onClick 持久化调用）。
    /// 断电后门强制打开、按钮失效（FNAF 1 规则）。
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        [Tooltip("这扇门在哪一侧")]
        [SerializeField] private DoorSide side;

        [Tooltip("滑动的金属门板（在场景中摆成关闭状态，运行时会先抬到打开位）")]
        [SerializeField] private Transform doorPanel;

        [Tooltip("门打开时相对关闭位置向上收回的距离")]
        [SerializeField] private float openOffsetY = 3.4f;

        [Tooltip("门板滑动速度（单位/秒）")]
        [SerializeField] private float moveSpeed = 10f;

        [Tooltip("门灯照明组（点光源+发光面板，点亮门外区域）")]
        [SerializeField] private GameObject doorLightObject;

        public DoorSide Side => side;
        public bool IsClosed { get; private set; }
        public bool IsLightOn { get; private set; }

        /// <summary>左右门的静态注册（敌人 AI 用 IsDoorClosed 查询门状态，无需场景引用）</summary>
        public static DoorController LeftDoor { get; private set; }
        public static DoorController RightDoor { get; private set; }

        public static bool IsDoorClosed(DoorSide side)
        {
            DoorController door = side == DoorSide.Left ? LeftDoor : RightDoor;
            return door != null && door.IsClosed;
        }

        private Vector3 closedPosition;
        private Vector3 openPosition;
        private bool powerLocked;

        private void Awake()
        {
            if (side == DoorSide.Left) LeftDoor = this;
            else RightDoor = this;

            if (doorPanel == null)
            {
                Debug.LogWarning($"[DoorController] {name} 未指定 doorPanel");
                enabled = false;
                return;
            }

            closedPosition = doorPanel.localPosition;
            openPosition = closedPosition + new Vector3(0f, openOffsetY, 0f);
            // 开局门是打开的
            doorPanel.localPosition = openPosition;
        }

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.PowerOut += OnPowerOut;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.PowerOut -= OnPowerOut;
        }

        private void OnNightStarted(int night) => ResetState();

        private void OnPowerOut()
        {
            powerLocked = true;
            SetDoor(false);
            SetLight(false);
        }

        private void ResetState()
        {
            powerLocked = false;
            SetDoor(false);
            SetLight(false);
        }

        /// <summary>UI 按钮：切换门的开/关。</summary>
        public void ToggleDoor() => SetDoor(!IsClosed);

        /// <summary>UI 按钮：切换门灯亮/灭。</summary>
        public void ToggleLight() => SetLight(!IsLightOn);

        public void SetDoor(bool closed)
        {
            if (closed == IsClosed) return;
            if (powerLocked && closed) return; // 断电后无法关门，但可以被系统强制打开

            IsClosed = closed;
            GameEvents.RaiseDoorToggled(side, closed);
        }

        public void SetLight(bool on)
        {
            if (on == IsLightOn) return;
            if (powerLocked) return;

            IsLightOn = on;
            if (doorLightObject != null) doorLightObject.SetActive(on);
            GameEvents.RaiseDoorLightToggled(side, on);
        }

        private void Update()
        {
            if (doorPanel == null) return;

            Vector3 target = IsClosed ? closedPosition : openPosition;
            doorPanel.localPosition = Vector3.MoveTowards(
                doorPanel.localPosition, target, moveSpeed * Time.deltaTime);
        }
    }
}
