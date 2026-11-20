using UnityEngine;

namespace FNAF.Office
{
    /// <summary>
    /// 桌面风扇：FNAF 办公室的标志性摆件，叶片持续旋转（纯代码驱动，无动画剪辑）。
    /// </summary>
    public class FanSpinner : MonoBehaviour
    {
        [Tooltip("风扇叶片节点")]
        [SerializeField] private Transform fanBlades;

        [Tooltip("旋转速度（度/秒）")]
        [SerializeField] private float spinSpeed = 720f;

        private void Update()
        {
            if (fanBlades != null)
            {
                fanBlades.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            }
        }
    }
}
