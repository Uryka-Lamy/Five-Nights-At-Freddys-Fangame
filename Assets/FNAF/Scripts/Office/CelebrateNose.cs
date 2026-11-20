using UnityEngine;
using FNAF.Core;

namespace FNAF.Office
{
    /// <summary>
    /// 办公室"Celebrate!"三人海报彩蛋：点击海报中间弗雷迪的鼻子，
    /// 播放一小段欢快的游戏机叮当——复刻原版点鼻子彩蛋。
    /// 挂在带碰撞体的鼻子物体上（由 SceneBuilder 创建并接线音源）。
    /// </summary>
    public class CelebrateNose : MonoBehaviour
    {
        [Tooltip("点击鼻子时播放的音效")]
        [SerializeField] private AudioSource source;

        [Tooltip("第二音源（与主音源同开叠加响度，突破单源 1.0 音量上限）")]
        [SerializeField] private AudioSource source2;

        private float cooldownUntil; // 防连点把音效叠成一团

        /// <summary>只在游戏中响应（主菜单下办公室渲染在背后，点鼻子不能出声）。</summary>
        private static bool InGame =>
            GameManager.Instance != null && GameManager.Instance.Phase == GameManager.GamePhase.Playing;

        private void OnMouseDown()
        {
            if (!InGame) return;
            if (source == null || source.clip == null) return;
            if (Time.unscaledTime < cooldownUntil) return;
            cooldownUntil = Time.unscaledTime + source.clip.length;
            source.Play();
            if (source2 != null) source2.Play();
        }
    }
}
