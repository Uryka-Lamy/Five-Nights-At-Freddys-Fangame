using UnityEngine;

namespace FNAF.Core
{
    /// <summary>
    /// 存档系统：记录已解锁的最高夜晚（PlayerPrefs 本地存档）。
    /// 夜晚通关后解锁下一夜；正篇共 6 夜（第 6 夜=强化夜），自定义夜独立不占编号。
    /// </summary>
    public static class SaveSystem
    {
        private const string UnlockedNightKey = "fnaf.unlockedNight";
        public const int MaxNight = 6;

        /// <summary>当前已解锁的最高夜晚（默认 1 = 只能玩第 1 夜）</summary>
        public static int GetHighestUnlockedNight()
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(UnlockedNightKey, 1), 1, MaxNight);
        }

        public static bool IsNightUnlocked(int night)
        {
            return night >= 1 && night <= GetHighestUnlockedNight();
        }

        /// <summary>自定义夜是否已解锁：通关第 5 夜后出现（原版同款），此前菜单不显示该选项。</summary>
        public static bool IsCustomNightUnlocked()
        {
            return GetHighestUnlockedNight() >= 6;
        }

        /// <summary>夜晚通关时调用：解锁下一夜并保存。</summary>
        public static void MarkNightCompleted(int night)
        {
            int unlocked = GetHighestUnlockedNight();
            if (night >= unlocked)
            {
                PlayerPrefs.SetInt(UnlockedNightKey, Mathf.Clamp(night + 1, 1, MaxNight));
            }
            PlayerPrefs.Save();
        }

        /// <summary>清空进度（主菜单的“重置存档”按钮用）。</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(UnlockedNightKey);
            PlayerPrefs.DeleteKey(StarsMaskKey);
            PlayerPrefs.Save();
        }

        // ---------- 评星（主菜单标题下显示）：通第 5 夜、通第 6 夜、自定义夜全 20 各得 1 颗，共 3 颗 ----------

        private const string StarsMaskKey = "fnaf.starsMask";

        /// <summary>已获得的评星数（0-3）。每位星只记一次，重复通关不叠加。</summary>
        public static int GetStars()
        {
            int mask = PlayerPrefs.GetInt(StarsMaskKey, 0);
            int count = 0;
            while (mask != 0) { count += mask & 1; mask >>= 1; }
            return count;
        }

        /// <summary>授予第 index 颗星（0-3，幂等：已有则忽略）。</summary>
        public static void TryAwardStar(int index)
        {
            if (index < 0 || index > 3) return;
            int mask = PlayerPrefs.GetInt(StarsMaskKey, 0);
            if ((mask & (1 << index)) != 0) return;
            PlayerPrefs.SetInt(StarsMaskKey, mask | (1 << index));
            PlayerPrefs.Save();
        }
    }
}
