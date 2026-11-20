using UnityEngine;

namespace FNAF.Enemies
{
    /// <summary>
    /// 积木玩偶的身体控制器：由场景生成器用基本几何体拼装出躯干/头/眼睛/四肢，
    /// 本组件持有各部件引用，提供"看镜头"（眼睛发亮）与"待机"（熄灭）两种视觉状态，
    /// 以及跳杀时专用的"扑脸"姿态入口。不含任何 AI 决策逻辑。
    /// </summary>
    public class AnimatronicBody : MonoBehaviour
    {
        [Header("部件引用（场景生成器填充）")]
        [Tooltip("头部节点（跳杀时朝镜头俯冲）")]
        [SerializeField] private Transform head;

        [Tooltip("自发光眼睛组（看镜头时点亮）")]
        [SerializeField] private Renderer[] eyeRenderers;

        [Tooltip("眼睛点亮的自发光颜色")]
        [SerializeField] private Color eyeGlowColor = new Color(1f, 0.85f, 0.6f);

        [Tooltip("整体可见性根（隐形时整个玩偶消失）")]
        [SerializeField] private GameObject visualRoot;

        [Tooltip("眼睛伴生点光（黑暗中断电处刑的'真实发光'光源，材质自发光失效时的保险）")]
        [SerializeField] private Light eyeGlowLight;

        public bool IsVisible => visualRoot == null || visualRoot.activeSelf;

        /// <summary>头部节点（图鉴跳杀预览用它对准镜头光轴）。</summary>
        public Transform Head => head;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public void SetVisible(bool visible)
        {
            if (visualRoot != null && visualRoot.activeSelf != visible)
                visualRoot.SetActive(visible);
        }

        /// <summary>点亮/熄灭自发光眼睛（被监控看时点亮，制造"它在看你"的恐怖感）。</summary>
        public void SetEyesGlowing(bool glowing)
        {
            // 伴生点光独立于材质：即使自发光变体没生效，黑暗里眼睛依然是真实发光体
            if (eyeGlowLight != null && eyeGlowLight.enabled != glowing)
                eyeGlowLight.enabled = glowing;
            if (eyeRenderers == null) return;
            foreach (Renderer eye in eyeRenderers)
            {
                if (eye == null) continue;
                // 编辑模式（场景生成器点亮相位）用 sharedMaterial 直接改：材质由工厂按玩偶独立创建，无串染；
                // .material 在编辑模式会实例化泄漏材质并刷警告。运行时保持 .material 的按渲染器实例化语义。
                Material mat = Application.isPlaying ? eye.material : eye.sharedMaterial;
                if (mat == null) continue;
                if (mat.HasProperty(EmissionColorId))
                {
                    // Standard 材质必须显式启用 _EMISSION 关键字自发光才可见
                    if (glowing) mat.EnableKeyword("_EMISSION");
                    mat.SetColor(EmissionColorId, glowing ? eyeGlowColor * 2.2f : Color.black);
                }
            }
        }

        /// <summary>跳杀前的"扑脸"姿态：头部猛地凑近镜头方向。</summary>
        public void SnapHeadToward(Transform target)
        {
            if (head == null || target == null) return;
            Vector3 dir = target.position - head.position;
            dir.x = 0f; // 保持身体直立，只做俯仰朝向
            if (dir.sqrMagnitude > 0.0001f)
            {
                head.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        /// <summary>跳杀抖动：整体随机晃动（由跳杀控制器每帧调用）。</summary>
        public void Shake(float intensity)
        {
            Vector3 offset = new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0f);
            transform.localPosition += offset;
        }
    }
}
