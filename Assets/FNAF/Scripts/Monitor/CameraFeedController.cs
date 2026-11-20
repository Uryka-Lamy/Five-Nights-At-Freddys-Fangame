using UnityEngine;
using FNAF.Core;
using FNAF.Data;
using FNAF.UI;

namespace FNAF.Monitor
{
    /// <summary>
    /// 摄像头画面切换：把监控摄像机瞬移到选中房间的机位，
    /// 每次切换播放一段强静态噪点（随机像素纹理逐帧刷新），平时保留轻微噪点（FNAF 风格）。
    /// 厨房为仅音频房间：只显示“音频干扰”提示，不显示画面。
    /// 本组件挂在监控根节点下（噪点覆盖层随监控器开关整体显隐）。
    /// </summary>
    public class CameraFeedController : MonoBehaviour
    {
        [Tooltip("房间注册表（房间机位数据源）")]
        [SerializeField] private RoomRegistry roomRegistry;

        [Tooltip("执行切换的监控摄像机")]
        [SerializeField] private Camera feedCamera;

        [Tooltip("噪点覆盖层（RawImage，逐帧刷随机噪声纹理）")]
        [SerializeField] private UnityEngine.UI.RawImage staticOverlay;

        [Tooltip("当前房间名标签（切换时更新）")]
        [SerializeField] private UnityEngine.UI.Text roomNameLabel;

        [Tooltip("仅音频房间的提示文本（如厨房显示“—— 音频干扰 ——”）")]
        [SerializeField] private GameObject audioOnlyNotice;

        [Tooltip("监控地图高亮组件（切房时点亮对应 CAM 按钮）")]
        [SerializeField] private MonitorMapUI mapHighlight;

        [Tooltip("每次切换摄像头时强噪点持续秒数")]
        [SerializeField] private float staticDuration = 0.35f;

        [Header("玩偶移动雪花（原版：切镜头玩偶已换位）")]
        [Tooltip("移动雪花最短持续秒数")]
        [SerializeField] private float movementStaticMin = 0.9f;

        [Tooltip("移动雪花最长持续秒数")]
        [SerializeField] private float movementStaticMax = 1.8f;

        [Tooltip("移动雪花出现时的突发静电音（无声则不播）")]
        [SerializeField] private AudioSource staticBurstSource;

        [Tooltip("切换摄像头时的咔哒声")]
        [SerializeField] private AudioSource camSwitchSource;

        [Header("机位左右往复旋转（走廊式摄像头）")]
        [Tooltip("旋转速度（度/秒）")]
        [SerializeField] private float panSpeed = 6f;

        [Tooltip("转到最左/最右后的停留时长（秒）")]
        [SerializeField] private float panHoldDuration = 2f;

        // 噪点遮罩浓度：切换瞬间的强噪点 / 常态轻微颗粒感 / 玩偶移动的遮蔽雪花
        private const float BurstAlpha = 0.55f;
        private const float GrainAlpha = 0.05f;
        private const float MovementStaticAlpha = 0.92f;

        public string CurrentRoomId { get; private set; }
        public bool HasActiveFeed { get; private set; }

        private float staticTimer;
        private float movementStaticTimer;
        private Texture2D noiseTexture;
        private readonly Color32[] noiseColors = new Color32[128 * 128];

        // 机位旋转状态：以选房时的朝向为基准，在房间各自的 [panMin, panMax] 内偏航往复（FNAF 1 式缓慢摇头）
        private bool panning;
        private Quaternion baseRotation;
        private float panAngle;
        private float panMin = -12f;
        private float panMax = 12f;
        private float panDirection = 1f;
        private float panHoldTimer;

        private void Awake()
        {
            noiseTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            if (staticOverlay != null)
            {
                staticOverlay.texture = noiseTexture;
                staticOverlay.gameObject.SetActive(true);
            }
            if (audioOnlyNotice != null) audioOnlyNotice.SetActive(false);
            RefreshNoise();
            SetOverlayAlpha(GrainAlpha);
        }

        private void OnDestroy()
        {
            if (noiseTexture != null) Destroy(noiseTexture);
        }

        private void OnEnable()
        {
            GameEvents.AnimatronicMoved += OnAnimatronicMoved;
        }

        private void OnDisable()
        {
            GameEvents.AnimatronicMoved -= OnAnimatronicMoved;
        }

        private void OnAnimatronicMoved(string enemyId, string fromRoomId, string toRoomId)
        {
            // 原版体验：你正看着的房间里玩偶"离开"或"进来"都会雪花遮蔽，掩盖移动瞬间；
            // 别处玩偶移动与当前画面无关，不打扰
            if (fromRoomId != CurrentRoomId && toRoomId != CurrentRoomId) return;
            // 监控没拉起时画面根本没在看，不触发雪花（放下监控凭空响静电音/空跑噪点计时）
            if (MonitorController.Instance == null || !MonitorController.Instance.IsUp) return;
            PlayMovementStatic();
        }

        /// <summary>玩偶移动触发的强噪点突发：随机 0.9~1.8 秒几乎全遮蔽，恢复后玩偶已换位（伴随突发静电音）。</summary>
        public void PlayMovementStatic()
        {
            movementStaticTimer = Random.Range(movementStaticMin, movementStaticMax);
            staticTimer = 0f;
            if (staticBurstSource != null && staticBurstSource.clip != null) staticBurstSource.Play();
        }

        /// <summary>监控器拉起时若没有画面，自动切到第一个房间。</summary>
        public void SelectFirstFeed()
        {
            if (roomRegistry == null || roomRegistry.Rooms.Count == 0) return;
            SelectRoom(roomRegistry.Rooms[0].RoomId, false); // 自动切首画面不播咔哒声（已有抬起音）
        }

        /// <summary>切到指定房间（UI 地图按钮调用，房间按钮会传房间 ID）。重复点击当前正在看的房间会被忽略。</summary>
        public void SelectRoom(string roomId) => SelectRoom(roomId, true);

        private void SelectRoom(string roomId, bool playSwitchSound)
        {
            if (roomId == CurrentRoomId && HasActiveFeed) return; // 已经在看这个房间：忽略重复点击

            if (roomRegistry == null)
            {
                Debug.LogWarning("[CameraFeedController] 未指定 RoomRegistry");
                return;
            }

            RoomDefinition room = roomRegistry.GetById(roomId);
            if (room == null)
            {
                Debug.LogWarning($"[CameraFeedController] 未知房间 ID: {roomId}");
                return;
            }

            CurrentRoomId = roomId;
            HasActiveFeed = true;
            staticTimer = staticDuration;
            if (playSwitchSound && camSwitchSource != null && camSwitchSource.clip != null)
                camSwitchSource.Play();
            if (roomNameLabel != null) roomNameLabel.text = room.DisplayName;

            bool audioOnly = room.AudioOnly || room.CameraAnchor == null;
            if (audioOnlyNotice != null) audioOnlyNotice.SetActive(audioOnly);

            if (feedCamera != null && room.CameraAnchor != null)
            {
                feedCamera.transform.SetPositionAndRotation(
                    room.CameraAnchor.position, room.CameraAnchor.rotation);
            }

            // 机位旋转：厨房（仅音频）与固定机位房间（杂物间/海盗湾）不转；范围取房间各自的配置
            panning = room.CameraPans && !audioOnly && feedCamera != null && room.CameraAnchor != null;
            baseRotation = room.CameraAnchor != null ? room.CameraAnchor.rotation : Quaternion.identity;
            panMin = room.CameraPanMin;
            panMax = room.CameraPanMax;
            panAngle = Mathf.Clamp(0f, panMin, panMax);
            panHoldTimer = 0f;
            // 起始方向：贴着某一端时只能向内转，否则随机
            if (panAngle <= panMin + 0.001f) panDirection = 1f;
            else if (panAngle >= panMax - 0.001f) panDirection = -1f;
            else panDirection = Random.value < 0.5f ? -1f : 1f;

            if (mapHighlight != null) mapHighlight.Highlight(roomId);
        }

        private void Update()
        {
            if (movementStaticTimer > 0f)
            {
                // 玩偶移动雪花：几乎不透明+逐帧刷新，彻底遮住移动瞬间
                movementStaticTimer -= Time.deltaTime;
                RefreshNoise();
                SetOverlayAlpha(MovementStaticAlpha);
                if (movementStaticTimer <= 0f && staticBurstSource != null) staticBurstSource.Stop();
            }
            else if (staticTimer > 0f)
            {
                staticTimer -= Time.deltaTime;
                RefreshNoise();
                SetOverlayAlpha(BurstAlpha);
            }
            else
            {
                // 轻微持续噪点：低频率刷新，还原老式监控的颗粒感
                if (Random.value < 0.12f) RefreshNoise();
                SetOverlayAlpha(GrainAlpha);
            }

            UpdatePan();
        }

        /// <summary>
        /// 机位在房间配置的 [panMin, panMax] 范围内左右缓慢往复：
        /// 转到一端 → 停留 → 反方向转到底 → 停留 → 循环。
        /// 偏航角叠加在选房时的基准朝向上，位置不动（原地摇头）。
        /// </summary>
        private void UpdatePan()
        {
            if (!panning || feedCamera == null) return;

            if (panHoldTimer > 0f)
            {
                panHoldTimer -= Time.deltaTime;
            }
            else
            {
                panAngle += panDirection * panSpeed * Time.deltaTime;
                if (panAngle >= panMax)
                {
                    panAngle = panMax;
                    panDirection = -1f;
                    panHoldTimer = panHoldDuration;
                }
                else if (panAngle <= panMin)
                {
                    panAngle = panMin;
                    panDirection = 1f;
                    panHoldTimer = panHoldDuration;
                }
            }

            feedCamera.transform.rotation = baseRotation * Quaternion.Euler(0f, panAngle, 0f);
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (staticOverlay == null) return;
            Color c = staticOverlay.color;
            c.a = alpha;
            staticOverlay.color = c;
        }

        private void RefreshNoise()
        {
            for (int i = 0; i < noiseColors.Length; i++)
            {
                // 约 40% 像素透明化：噪点呈散落颗粒而非整片灰幕，场景从颗粒缝隙中透出
                if (Random.value < 0.4f)
                {
                    noiseColors[i] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    byte v = (byte)Random.Range(20, 235);
                    noiseColors[i] = new Color32(v, v, v, 255);
                }
            }
            noiseTexture.SetPixels32(noiseColors);
            noiseTexture.Apply(false);
        }
    }
}
