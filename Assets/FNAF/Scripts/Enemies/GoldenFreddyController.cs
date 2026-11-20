using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;
using FNAF.Data;
using FNAF.Monitor;
using FNAF.UI;

namespace FNAF.Enemies
{
    /// <summary>
    /// 金熊彩蛋（复刻 FNAF 1 机制）：每次切到 CAM 2B（西走廊角落）时掷骰，
    /// 小概率把墙上的弗雷迪海报换成金熊脸特写并响起女孩笑声；
    /// 此时放下监控 → 金熊瘫坐在办公室地上，屏幕闪现"IT'S ME"幻觉，
    /// 数秒后跳杀并"崩溃"游戏（直接退出，没有 GAME OVER 界面）。
    /// 唯一防御：在金熊动手前重新拉起监控；门/灯/电力对他全部无效。
    /// 另：自定义夜把四只玩偶等级拨成 1/9/8/7，开始时立即触发同款跳杀崩溃。
    /// </summary>
    public class GoldenFreddyController : MonoBehaviour
    {
        [Header("引用（场景生成器填充）")]
        [Tooltip("瘫坐办公室的金熊身体（初始隐藏）")]
        [SerializeField] private GameObject body;

        [Tooltip("金熊头部（跳杀时对准镜头光轴）")]
        [SerializeField] private Transform head;

        [Tooltip("西走廊角落：平常的弗雷迪海报")]
        [SerializeField] private GameObject posterNormal;

        [Tooltip("西走廊角落：金熊脸特写海报（初始隐藏）")]
        [SerializeField] private GameObject posterGolden;

        [Tooltip("IT'S ME 全屏覆盖层（HUD 画布，初始隐藏）")]
        [SerializeField] private GameObject itsMeOverlay;

        [Tooltip("IT'S ME 文字（闪烁时随机改位置/字号）")]
        [SerializeField] private Text itsMeText;

        [Tooltip("四只玩偶的简笔正脸（闪烁时随机替换 IT'S ME 出现）")]
        [SerializeField] private GameObject[] itsMeFaces;

        [Tooltip("女孩笑声源")]
        [SerializeField] private AudioSource laughSource;

        [Tooltip("IT'S ME 闪图音（循环：金熊现身办公室后的幻觉期间持续播放）")]
        [SerializeField] private AudioSource itsMeSource;

        [Tooltip("跳杀尖叫源（金熊专用，与常规跳杀不同）")]
        [SerializeField] private AudioSource screamSource;

        [Tooltip("跳杀补光灯（与常规跳杀共用，怼脸不背光）")]
        [SerializeField] private GameObject jumpscareLight;

        [Tooltip("办公室摄像机（1987 彩蛋在菜单阶段也要怼脸）")]
        [SerializeField] private Camera officeCamera;

        [Tooltip("主菜单控制器（1987 彩蛋先藏掉全部菜单视觉）")]
        [SerializeField] private MainMenuUI mainMenu;

        [Header("规则")]
        [Tooltip("每次切到 CAM 2B 时海报变化的概率（原版约 1/32768，这里放宽到实际能遇见）")]
        [SerializeField] private float posterChance = 0.005f;

        [Tooltip("金熊现身办公室后到动手的秒数（唯一防御窗口）")]
        [SerializeField] private float killDelay = 5f;

        [Tooltip("跳杀时脸部离镜头的距离（米）")]
        [SerializeField] private float faceDistance = 0.55f;

        [Tooltip("跳杀演出时长（秒）")]
        [SerializeField] private float jumpscareDuration = 1.2f;

        private enum GFState { Idle, Armed, InOffice, Killing }
        private GFState state = GFState.Idle;

        private float killTimer;
        private float flashTimer;
        private bool wasFeedOn2B;

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.NightCompleted += OnAnyNightEnd;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.NightCompleted -= OnAnyNightEnd;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnNightStarted(int night) => ResetAll();
        private void OnAnyNightEnd(int night) => ResetAll();
        private void OnMenuReturned() => ResetAll(); // ESC 退出本夜：金熊不能在主菜单里完成处刑

        // 其他玩偶先动手时清场，避免金熊演出与常规 GAME OVER 流程打架
        private void OnPlayerCaught(string enemyId)
        {
            if (state != GFState.Killing) ResetAll();
        }

        private void Update()
        {
            if (state == GFState.Killing) return;

            GameManager gm = GameManager.Instance;
            MonitorController mon = MonitorController.Instance;

            switch (state)
            {
                case GFState.Idle:
                    if (gm == null || mon == null || gm.Phase != GameManager.GamePhase.Playing) break;
                    if (mon.IsRoomOnFeed(RoomIds.WestHallCorner) && !wasFeedOn2B
                        && Random.value < posterChance)
                    {
                        state = GFState.Armed;
                        if (posterGolden != null) posterGolden.SetActive(true);
                        if (posterNormal != null) posterNormal.SetActive(false);
                        if (laughSource != null && laughSource.clip != null) laughSource.Play();
                    }
                    break;

                case GFState.Armed:
                    // 看到金熊海报后放下监控 → 金熊现身办公室
                    if (mon != null && !mon.IsUp)
                    {
                        state = GFState.InOffice;
                        killTimer = killDelay;
                        flashTimer = 0f;
                        if (body != null) body.SetActive(true);
                        if (laughSource != null && laughSource.clip != null && !laughSource.isPlaying)
                            laughSource.Play();
                        // 幻觉期间持续播放 IT'S ME 闪图音（放下监控的瞬间开始）
                        if (itsMeSource != null && itsMeSource.clip != null) itsMeSource.Play();
                    }
                    break;

                case GFState.InOffice:
                    // 唯一防御：重新拉起监控，金熊消失
                    if (mon != null && mon.IsUp)
                    {
                        ResetAll();
                        break;
                    }
                    killTimer -= Time.deltaTime;
                    FlashItsMe();
                    if (killTimer <= 0f)
                    {
                        state = GFState.Killing;
                        StartCoroutine(KillRoutine());
                    }
                    break;
            }

            if (mon != null)
                wasFeedOn2B = mon.IsRoomOnFeed(RoomIds.WestHallCorner);
        }

        /// <summary>自定义夜 1/9/8/7 彩蛋入口：藏掉菜单后直接跳杀"崩溃"。</summary>
        public void Trigger1987Crash()
        {
            if (state == GFState.Killing) return;
            state = GFState.Killing;
            if (mainMenu != null) mainMenu.HideForGoldenCrash();
            StartCoroutine(KillRoutine());
        }

        private void ResetAll()
        {
            state = GFState.Idle;
            if (posterGolden != null) posterGolden.SetActive(false);
            if (posterNormal != null) posterNormal.SetActive(true);
            if (body != null) body.SetActive(false);
            if (itsMeOverlay != null) itsMeOverlay.SetActive(false);
            HideFaces();
            if (itsMeText != null) itsMeText.gameObject.SetActive(true);
            if (laughSource != null && laughSource.isPlaying) laughSource.Stop();
            if (itsMeSource != null && itsMeSource.isPlaying) itsMeSource.Stop();
        }

        private void FlashItsMe()
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer > 0f) return;
            flashTimer = Random.Range(0.06f, 0.16f);

            bool show = Random.value < 0.65f;
            if (itsMeOverlay != null) itsMeOverlay.SetActive(show);
            if (!show) return;

            // 幻觉内容：约七成闪 IT'S ME 文字，其余闪一张随机玩偶正脸
            bool face = itsMeFaces != null && itsMeFaces.Length > 0 && Random.value < 0.3f;
            if (itsMeText != null) itsMeText.gameObject.SetActive(!face);
            HideFaces();
            if (face)
            {
                GameObject f = itsMeFaces[Random.Range(0, itsMeFaces.Length)];
                if (f == null) return;
                f.SetActive(true);
                // 每次闪现的大小/位置都随机，营造贴脸错乱感
                if (f.transform is RectTransform rt)
                {
                    rt.anchoredPosition = new Vector2(Random.Range(-180f, 180f), Random.Range(-100f, 100f));
                    float s = Random.Range(0.8f, 1.6f);
                    rt.localScale = new Vector3(s, s, 1f);
                }
                return;
            }

            if (itsMeText == null) return;
            RectTransform textRt = itsMeText.rectTransform;
            textRt.anchoredPosition = new Vector2(Random.Range(-640f, 640f), Random.Range(-340f, 340f));
            itsMeText.fontSize = Random.Range(70, 150);
            itsMeText.color = Random.value < 0.3f
                ? new Color(0.9f, 0.75f, 0.2f)
                : Color.white;
        }

        /// <summary>隐藏全部玩偶正脸。</summary>
        private void HideFaces()
        {
            if (itsMeFaces == null) return;
            foreach (GameObject f in itsMeFaces)
                if (f != null && f.activeSelf) f.SetActive(false);
        }

        private IEnumerator KillRoutine()
        {
            // 跳杀开始：闪图音停，让金熊专属尖叫主导
            if (itsMeSource != null && itsMeSource.isPlaying) itsMeSource.Stop();
            if (body != null) body.SetActive(true);
            PlaceBodyInFrontOfCamera();

            // 先黑一瞬，再露出怼脸——复刻原版"屏幕一黑→金熊脸"的节奏
            if (itsMeOverlay != null)
            {
                itsMeOverlay.SetActive(true);
                if (itsMeText != null) itsMeText.gameObject.SetActive(false);
                HideFaces();
            }
            yield return new WaitForSeconds(0.18f);

            if (jumpscareLight != null) jumpscareLight.SetActive(true);
            if (screamSource != null && screamSource.clip != null) screamSource.Play();
            if (itsMeOverlay != null) itsMeOverlay.SetActive(false);

            // 金熊跳杀：直接怼脸定住——复刻原版"突然出现在屏幕正中"的静止特写，没有其他玩偶那种抽搐抖动
            yield return new WaitForSeconds(jumpscareDuration);

            if (jumpscareLight != null) jumpscareLight.SetActive(false);
            if (itsMeOverlay != null) itsMeOverlay.SetActive(true);
            yield return new WaitForSeconds(0.6f);

            // 原版行为：金熊杀你 = 游戏直接关闭，没有 GAME OVER
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlaceBodyInFrontOfCamera()
        {
            if (body == null || head == null) return;
            Transform cam = officeCamera != null ? officeCamera.transform : Camera.main.transform;

            // 怼脸构图（与常规跳杀/图鉴预览同款）：头部对准镜头光轴
            Vector3 headLocal = body.transform.InverseTransformPoint(head.position);
            Vector3 headTarget = cam.position + cam.forward * faceDistance + Vector3.up * 0.08f;
            body.transform.rotation = Quaternion.LookRotation(cam.position - headTarget, Vector3.up);
            body.transform.position += headTarget - body.transform.TransformPoint(headLocal);
        }
    }
}
