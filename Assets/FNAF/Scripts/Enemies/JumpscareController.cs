using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;
using FNAF.Monitor;

namespace FNAF.Enemies
{
    /// <summary>
    /// 跳杀演出：监听 PlayerCaught 事件，把凶手玩偶瞬移到办公室镜头正前方
    /// （与玩偶图鉴的跳杀预览同款：头部对准镜头光轴 + 原地高频抖动 + 机器尖叫，无闪屏），
    /// 持续约 1.2 秒后切入黑屏（游戏结束 UI 由第 3 部分接管）。
    /// </summary>
    public class JumpscareController : MonoBehaviour
    {
        [Header("演出参数")]
        [Tooltip("跳杀演出总时长（秒）")]
        [SerializeField] private float duration = 1.2f;

        [Tooltip("玩偶脸部离镜头的距离（米，越小越贴脸）")]
        [SerializeField] private float faceDistance = 0.55f;

        [Tooltip("抖动强度（米）")]
        [SerializeField] private float shakeIntensity = 0.05f;

        [Header("引用")]
        [Tooltip("办公室摄像机（跳杀在办公室视角发生）")]
        [SerializeField] private Camera officeCamera;

        [Tooltip("全屏黑幕 Image（跳杀结束后淡入）")]
        [SerializeField] private Image blackOverlay;

        [Tooltip("跳杀专用灯（办公室灯在怼脸的背面，无此灯脸是黑的）")]
        [SerializeField] private GameObject jumpscareLight;

        [Tooltip("跳杀音效源（第 3 部分接尖叫声 wav）")]
        [SerializeField] private AudioSource screamSource;

        [Tooltip("跳杀结束黑幕时的低沉死亡音")]
        [SerializeField] private AudioSource deathSource;

        [Tooltip("全部玩偶，按 ID 索引身体（场景生成器填充）")]
        [SerializeField] private AnimatronicBody[] bodies;

        [Tooltip("与 bodies 对应的玩偶 ID（Freddy/Bonnie/Chica/Foxy）")]
        [SerializeField] private string[] bodyIds;

        private readonly Dictionary<string, AnimatronicBody> bodyById = new Dictionary<string, AnimatronicBody>();
        private AnimatronicBody activeBody;
        private float timer;
        private bool playing;
        private Vector3 shakeBase;
        private Vector3 shakeRight, shakeUp;

        private void Awake()
        {
            for (int i = 0; i < bodies.Length && i < bodyIds.Length; i++)
            {
                if (bodies[i] != null && !string.IsNullOrEmpty(bodyIds[i]))
                    bodyById[bodyIds[i]] = bodies[i];
            }
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnNightStarted(int night)
        {
            // 新夜开始时清掉上一局的黑幕/跳杀灯残留，并掐断还没放完的死亡音
            playing = false;
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(false);
            if (jumpscareLight != null) jumpscareLight.SetActive(false);
            if (deathSource != null && deathSource.isPlaying) deathSource.Stop();
        }

        private void OnMenuReturned()
        {
            // 回主菜单（含 GAME OVER 3 秒自动返回）时掐断还没放完的死亡音
            if (deathSource != null && deathSource.isPlaying) deathSource.Stop();
        }

        private void OnPlayerCaught(string enemyId)
        {
            if (playing) return; // 同帧双跳杀守卫：一次演出即可，避免二次传送/重复协程

            // 跳杀发生在办公室视角：若玩家正开着监控则强制放下
            if (MonitorController.Instance != null && MonitorController.Instance.IsUp)
                MonitorController.Instance.PutDown();

            // 断电弗雷迪没有身体 ID 时退化为通用演出
            activeBody = bodyById.TryGetValue(enemyId, out AnimatronicBody body) ? body : null;
            if (activeBody == null && bodies.Length > 0) activeBody = bodies[0];

            if (activeBody != null)
            {
                activeBody.SetVisible(true);
                activeBody.SetEyesGlowing(true);

                Transform cam = officeCamera != null
                    ? officeCamera.transform
                    : (Camera.main != null ? Camera.main.transform : null);
                if (cam == null)
                {
                    Debug.LogWarning("[JumpscareController] 找不到办公室摄像机，跳过怼脸构图");
                }
                else
                {
                // 怼脸构图（与图鉴预览同款）：把"头部"对准镜头光轴。
                // 若只摆根节点（脚），头会高出画面顶端，只能拍到悬空躯干。
                    Vector3 headLocal = activeBody.Head != null
                        ? activeBody.transform.InverseTransformPoint(activeBody.Head.position)
                        : new Vector3(0f, 1.9f, 0.1f);
                    Vector3 headTarget = cam.position + cam.forward * faceDistance + Vector3.up * 0.08f;
                    activeBody.transform.rotation = Quaternion.LookRotation(cam.position - headTarget, Vector3.up);
                    activeBody.transform.position += headTarget - activeBody.transform.TransformPoint(headLocal);
                    activeBody.SnapHeadToward(cam);

                    shakeBase = activeBody.transform.position;
                    shakeRight = cam.right;
                    shakeUp = cam.up;
                }
            }

            if (screamSource != null && screamSource.clip != null) screamSource.Play();
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(false);
            if (jumpscareLight != null) jumpscareLight.SetActive(true);

            timer = duration;
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;

            timer -= Time.deltaTime;

            if (activeBody != null)
            {
                // 原地高频抖动（与图鉴预览同款）：每帧从基准点重新随机，不累积漂移；
                // 抖动沿镜头右/上方向，任意视角下都读作屏幕平面内的晃动
                Vector2 jitter = Random.insideUnitCircle * shakeIntensity;
                activeBody.transform.position = shakeBase + shakeRight * jitter.x + shakeUp * jitter.y;
            }

            if (timer <= 0f)
            {
                playing = false;
                if (blackOverlay != null) blackOverlay.gameObject.SetActive(true);
                if (jumpscareLight != null) jumpscareLight.SetActive(false);
                if (activeBody != null) activeBody.SetVisible(false);
                if (deathSource != null && deathSource.clip != null) deathSource.Play();
            }
        }
    }
}
