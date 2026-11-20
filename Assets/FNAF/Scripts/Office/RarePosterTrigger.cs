using UnityEngine;

namespace FNAF.Office
{
    /// <summary>
    /// 稀有海报彩蛋：每夜开始时小概率把办公室北墙海报换成"鬼脸海报"（原版 rare screens 的致意）。
    /// 挂在隐藏的鬼脸海报组上，场景生成器接线。
    /// </summary>
    public class RarePosterTrigger : MonoBehaviour
    {
        [Tooltip("鬼脸海报组（平时隐藏）")]
        [SerializeField] private GameObject ghostPoster;

        [Tooltip("出现概率（0-1）")]
        [Range(0f, 1f)] [SerializeField] private float chance = 0.08f;

        private void OnEnable()
        {
            if (ghostPoster != null)
                ghostPoster.SetActive(Random.value < chance);
        }

        private void OnDisable()
        {
            if (ghostPoster != null) ghostPoster.SetActive(false);
        }
    }
}
