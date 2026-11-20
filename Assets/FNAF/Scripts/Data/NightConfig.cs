using UnityEngine;

namespace FNAF.Data
{
    /// <summary>
    /// 单个夜晚的全部配置数据（数据驱动：难度/时长/电力/字幕都在这里改，不动代码）。
    /// 通过右键 Create → FNAF → Night Config 创建，每晚会生成一个资产。
    /// </summary>
    [CreateAssetMenu(fileName = "NightConfig", menuName = "FNAF/Night Config")]
    public class NightConfig : ScriptableObject
    {
        [Header("夜晚基本信息")]
        [Tooltip("夜晚编号（1-6 为正篇，7 为自定义夜）")]
        public int NightNumber = 1;

        [Tooltip("每个游戏小时对应的现实秒数（75 秒，一夜 6 小时共 7 分半）")]
        public float HourDuration = 75f;

        [Header("电力")]
        [Tooltip("开局电力百分比（原版为 99）")]
        public float StartingPower = 99f;

        [Tooltip("使用等级 1 时的每秒耗电百分比（原版约 0.104，即每 9.6 秒掉 1%）")]
        public float BaseDrainPerSecond = 0.104f;

        [Tooltip("逐夜附加耗电：每隔 N 秒额外扣 0.1%，与用电格数无关（原版第 2/3/4/5 夜起 = 6/5/4/3 秒）；0 = 无附加")]
        public float NightlyDrainIntervalSeconds = 0f;

        [Header("玩偶 AI 等级（0-20，越高越活跃）")]
        [Tooltip("弗雷迪：从表演舞台沿东侧推进")]
        [Range(0, 20)] public int FreddyAI = 0;
        [Tooltip("邦尼：从表演舞台沿西侧推进，出现在左门")]
        [Range(0, 20)] public int BonnieAI = 2;
        [Tooltip("奇卡：从表演舞台沿东侧推进，出现在右门")]
        [Range(0, 20)] public int ChicaAI = 1;
        [Tooltip("狐狸：蹲守海盗湾，监控看得少就会冲刺西走廊")]
        [Range(0, 20)] public int FoxyAI = 1;
    }
}
