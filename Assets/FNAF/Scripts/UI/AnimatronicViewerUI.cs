using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FNAF.Enemies;

namespace FNAF.UI
{
    /// <summary>
    /// 玩偶图鉴：左列切换四只玩偶，中央 RenderTexture 展示台（玩偶缓慢自转），
    /// "播放跳杀"按钮预览贴脸跳杀演出（瞬移到图鉴相机前抖动 + 一声机器尖叫，不做闪屏）。
    /// </summary>
    public class AnimatronicViewerUI : MonoBehaviour
    {
        [Header("面板与引用（场景生成器填充）")]
        [Tooltip("图鉴面板")]
        [SerializeField] private GameObject viewerPanel;

        [Tooltip("展示台画面（ViewerCamera 的 RenderTexture）")]
        [SerializeField] private RawImage viewerImage;

        [Tooltip("玩偶名字标题")]
        [SerializeField] private Text nameText;

        [Tooltip("玩偶简介文字")]
        [SerializeField] private Text descText;

        [Tooltip("跳杀预览专用尖叫声音源")]
        [SerializeField] private AudioSource screamSource;

        [Tooltip("图鉴相机（跳杀扑脸的参照）")]
        [SerializeField] private Transform viewerCamera;

        [Tooltip("四只展示玩偶：弗雷迪/邦尼/奇卡/狐狸")]
        [SerializeField] private GameObject[] viewBodies;

        [Header("跳杀演出参数")]
        [Tooltip("演出时长（秒）")]
        [SerializeField] private float duration = 1.2f;

        [Tooltip("脸部离镜头距离（米）")]
        [SerializeField] private float faceDistance = 0.55f;

        [Tooltip("抖动强度（米）")]
        [SerializeField] private float shakeIntensity = 0.05f;

        [Header("展示台")]
        [Tooltip("玩偶自转速度（度/秒）")]
        [SerializeField] private float spinSpeed = 45f;

        private static readonly string[] Names = { "弗 雷 迪", "邦 尼", "奇 卡", "狐 狸" };
        private static readonly string[] Descriptions =
        {
            "舞台的主角，谨慎的猎手。被监控注视时一动不动——但别让电力耗尽，他会亲自上门。",
            "西线常客，路线最飘忽。从舞台一路游走到左门，别让他堵在门口。",
            "东线常客，爱在厨房翻锅碗瓢盆。听到声响时，该看一眼右门了。",
            "海盗湾的蹲守者。偷懒不看他就会冲刺西走廊——听到脚步声，立刻关左门！"
        };

        private AnimatronicBody selected;
        private bool jumpscaring;

        public void Show()
        {
            if (viewerPanel != null) viewerPanel.SetActive(true);
            SelectAnimatronic(0);
        }

        /// <summary>返回主菜单（面板按钮持久化监听入口）。</summary>
        public void Close()
        {
            StopAllCoroutines();
            jumpscaring = false;
            ResetSelected();
            if (viewerPanel != null) viewerPanel.SetActive(false);
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.ShowMenu();
        }

        /// <summary>切换展示玩偶（左列按钮持久化监听入口，参数 0-3）。</summary>
        public void SelectAnimatronic(int index)
        {
            if (jumpscaring || viewBodies == null || viewBodies.Length == 0) return;
            int i = Mathf.Clamp(index, 0, viewBodies.Length - 1);
            for (int b = 0; b < viewBodies.Length; b++)
                if (viewBodies[b] != null) viewBodies[b].SetActive(b == i);
            selected = viewBodies[i] != null ? viewBodies[i].GetComponent<AnimatronicBody>() : null;
            ResetSelected();
            if (selected != null) selected.SetEyesGlowing(false);
            if (nameText != null) nameText.text = Names[i];
            if (descText != null) descText.text = Descriptions[i];
        }

        /// <summary>播放跳杀预览（按钮持久化监听入口）。</summary>
        public void PlayJumpscare()
        {
            if (!jumpscaring) StartCoroutine(JumpscareRoutine());
        }

        private IEnumerator JumpscareRoutine()
        {
            if (selected == null || viewerCamera == null) yield break;
            jumpscaring = true;
            selected.SetEyesGlowing(true);

            // 怼脸构图：把"头部"对准镜头光轴。若只摆根节点（脚），头会高出画面顶端只能拍到躯干
            Vector3 headLocal = selected.Head != null
                ? selected.transform.InverseTransformPoint(selected.Head.position)
                : new Vector3(0f, 1.9f, 0.1f);
            Vector3 headTarget = viewerCamera.position + viewerCamera.forward * faceDistance
                + Vector3.up * 0.08f;
            selected.transform.rotation = Quaternion.LookRotation(viewerCamera.position - headTarget, Vector3.up);
            selected.transform.position += headTarget - selected.transform.TransformPoint(headLocal);
            selected.SnapHeadToward(viewerCamera);

            if (screamSource != null && screamSource.clip != null) screamSource.Play();

            Vector3 baseLocal = selected.transform.localPosition;
            float timer = duration;
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                // 原地高频抖动：每帧从基准点重新随机，不累积漂移（避免玩偶越抖越滑向一侧）
                Vector2 jitter = Random.insideUnitCircle * shakeIntensity;
                selected.transform.localPosition = baseLocal + new Vector3(jitter.x, jitter.y, 0f);
                yield return null;
            }

            ResetSelected();
            selected.SetEyesGlowing(false);
            jumpscaring = false;
        }

        private void Update()
        {
            if (viewerPanel == null || !viewerPanel.activeSelf) return;
            if (jumpscaring || selected == null) return;
            selected.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void ResetSelected()
        {
            if (selected == null) return;
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;
        }
    }
}
