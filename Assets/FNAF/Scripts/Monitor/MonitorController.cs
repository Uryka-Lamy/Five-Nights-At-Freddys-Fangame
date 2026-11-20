using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;

namespace FNAF.Monitor
{
    /// <summary>
    /// 监控器总开关：控制监控画面与办公室画面的切换（摄像机/画布双向开关），
    /// 并把监控器状态广播到事件总线（电力计算、狐狸AI都依赖此状态）。
    /// 交互方式与 FNAF 原版一致：鼠标滑入屏幕底部中央的箭头标识热区 → 切换开/关；空格键同样可切换。
    /// </summary>
    public class MonitorController : MonoBehaviour
    {
        public static MonitorController Instance { get; private set; }

        public bool IsUp { get; private set; }

        [Header("开关对象")]
        [Tooltip("办公室场景摄像机（关监控时使用）")]
        [SerializeField] private GameObject officeCameraObject;

        [Tooltip("监控室摄像机（开监控时使用）")]
        [SerializeField] private GameObject monitorCameraObject;

        [Tooltip("监控画面根节点（监控UI+噪点层，不含世界物体）")]
        [SerializeField] private GameObject monitorRoot;

        [Header("输入")]
        [Tooltip("拉起/放下监控器的按键")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Space;

        [Tooltip("屏幕底部热区高度比例：鼠标滑入即切换监控器")]
        [Range(0.01f, 0.4f)] [SerializeField] private float bottomHoverThreshold = 0.05f;

        [Tooltip("底部热区宽度比例（以屏幕中线居中，对应界面底部的箭头标识）")]
        [Range(0.1f, 1f)] [SerializeField] private float hoverZoneWidth = 0.4f;

        [Header("底部切换标识")]
        [Tooltip("办公室视角的▲标识（鼠标悬停时高亮）")]
        [SerializeField] private CanvasGroup openBarGroup;

        [Tooltip("监控视角的▼标识（鼠标悬停时高亮）")]
        [SerializeField] private CanvasGroup closeBarGroup;

        [Tooltip("监控器抬起/放下的过渡时长（秒），期间输入被锁定")]
        [SerializeField] private float transitionDuration = 0.25f;

        private float transitionTimer;
        private bool inTransition;
        private bool inHoverZone;
        private CameraFeedController feed;
        private bool inputLocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            feed = GetComponent<CameraFeedController>();
        }

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.PowerOut += OnPowerOut;
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.PowerOut -= OnPowerOut;
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnNightStarted(int night) => ResetState();
        private void OnPowerOut()
        {
            inputLocked = true;
            SetMonitor(false, true); // 断电强制放下监控器
        }
        private void OnNightCompleted(int night)
        {
            inputLocked = true;
            SetMonitor(false, true); // 6AM 强制放下：结算画面背后不该是监控画面
        }
        private void OnPlayerCaught(string enemyId) => inputLocked = true;
        private void OnMenuReturned()
        {
            inputLocked = false;
            SetMonitor(false, true); // ESC 退出本夜：监控对象显隐复位
        }

        private void ResetState()
        {
            inputLocked = false;
            inTransition = false;
            inHoverZone = false;
            SetMonitor(false, true);
        }

        private void Update()
        {
            // 只在游戏中响应监控输入：主菜单/结算阶段鼠标滑过底部热区不该触发监控开关（及其音效）
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Phase != GameManager.GamePhase.Playing)
            {
                inHoverZone = false;
                return;
            }

            UpdateToggleBarHighlight();

            if (inTransition)
            {
                transitionTimer += Time.deltaTime;
                if (transitionTimer >= transitionDuration) inTransition = false;
                return;
            }

            if (inputLocked) return;

            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
                return;
            }

            bool atBottom = InHoverZone();

            if (atBottom && !inHoverZone)
            {
                // 鼠标滑入底部热区：切换监控器，并锁存直到鼠标离开热区
                inHoverZone = true;
                Toggle();
            }
            else if (!atBottom)
            {
                inHoverZone = false;
            }
        }

        /// <summary>鼠标是否在屏幕底部中央的热区内（与界面箭头标识位置对应）。</summary>
        private bool InHoverZone()
        {
            Vector3 m = Input.mousePosition;
            return m.y <= Screen.height * bottomHoverThreshold
                && Mathf.Abs(m.x - Screen.width * 0.5f) <= Screen.width * hoverZoneWidth * 0.5f;
        }

        /// <summary>当前视角对应的箭头标识：鼠标在热区内高亮，离开变暗。</summary>
        private void UpdateToggleBarHighlight()
        {
            CanvasGroup bar = IsUp ? closeBarGroup : openBarGroup;
            if (bar == null) return;
            bar.alpha = InHoverZone() ? 1f : 0.65f;
        }

        /// <summary>切换监控器开/关。</summary>
        public void Toggle() => SetMonitor(!IsUp, false);

        /// <summary>UI 放下按钮调用。</summary>
        public void PutDown() => SetMonitor(false, false);

        /// <summary>指定房间是否正在被监控画面观看（敌人 AI 的"被注视则冻结"判定）。</summary>
        public bool IsRoomOnFeed(string roomId)
        {
            return IsUp && feed != null && feed.CurrentRoomId == roomId;
        }

        private void SetMonitor(bool up, bool force)
        {
            if (!force && IsUp == up) return;

            bool stateChanged = IsUp != up;
            IsUp = up;
            inTransition = true;
            transitionTimer = 0f;

            if (officeCameraObject != null) officeCameraObject.SetActive(!up);
            if (monitorCameraObject != null) monitorCameraObject.SetActive(up);
            if (monitorRoot != null) monitorRoot.SetActive(up);

            if (up && feed != null && !feed.HasActiveFeed)
            {
                feed.SelectFirstFeed();
            }

            // 状态没变（强制重置路径）只同步显隐，不广播事件——避免每夜开场凭空响一声"放下监控"
            if (stateChanged) GameEvents.RaiseMonitorToggled(up);
        }
    }
}
