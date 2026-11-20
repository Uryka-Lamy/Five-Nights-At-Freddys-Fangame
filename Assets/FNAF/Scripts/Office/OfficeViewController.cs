using UnityEngine;

namespace FNAF.Office
{
    /// <summary>
    /// 办公室视角（混合控制）：鼠标在屏幕中间区域时比例控制——鼠标横移多少镜头转多少；
    /// 鼠标进入左右边缘判定区时直接朝该方向转向（匀速转到底）；
    /// 鼠标移回中间区域后恢复比例控制。旋转范围限制在 [minPanAngle, maxPanAngle]。
    /// </summary>
    public class OfficeViewController : MonoBehaviour
    {
        [Tooltip("被旋转的相机节点")]
        [SerializeField] private Transform cameraRig;

        [Tooltip("向左最大旋转角（度，负值）")]
        [SerializeField] private float minPanAngle = -30f;

        [Tooltip("向右最大旋转角（度，正值）")]
        [SerializeField] private float maxPanAngle = 30f;

        [Tooltip("镜头旋转的最大角速度（度/秒）：中间区跟随鼠标的速度与边缘区的转向速度")]
        [SerializeField] private float panSpeed = 500f;

        [Tooltip("屏幕左右多少比例内算作“边缘转向区”（0-0.5）")]
        [Range(0.01f, 0.5f)] [SerializeField] private float edgeThreshold = 0.45f;

        /// <summary>当前视角归一化：-1 = 最左，+1 = 最右，0 = 正中</summary>
        public float NormalizedPan { get; private set; }

        private float yaw;

        private void Update()
        {
            if (cameraRig == null) return;

            float mx = Input.mousePosition.x / Screen.width;
            float targetYaw;

            if (mx <= edgeThreshold)
            {
                // 左边缘区：直接向左转到底
                targetYaw = minPanAngle;
            }
            else if (mx >= 1f - edgeThreshold)
            {
                // 右边缘区：直接向右转到底
                targetYaw = maxPanAngle;
            }
            else
            {
                // 中间区域：鼠标横向位置按比例映射为视角偏角（判定区边界=最大角，衔接无缝）
                float t = Mathf.InverseLerp(edgeThreshold, 1f - edgeThreshold, mx);
                targetYaw = Mathf.Lerp(minPanAngle, maxPanAngle, t);
            }

            yaw = Mathf.MoveTowards(yaw, targetYaw, panSpeed * Time.deltaTime);
            cameraRig.localRotation = Quaternion.Euler(0f, yaw, 0f);

            float span = Mathf.Max(0.01f, maxPanAngle - minPanAngle);
            NormalizedPan = ((yaw - minPanAngle) / span) * 2f - 1f;
        }
    }
}
