using System.IO;
using UnityEditor;
using UnityEngine;
using FNAF.Data;

namespace FNAF.EditorTools
{
    /// <summary>
    /// 夜晚配置工厂：生成第 1-6 夜的 NightConfig 资产。
    /// AI 等级参考原版 FNAF 1 的难度曲线，电话字幕第 1-4 夜有内容。
    /// </summary>
    public static class NightConfigFactory
    {
        private const string OutputDir = "Assets/FNAF/Configs";

        public static void EnsureAll()
        {
            if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);

            for (int night = 1; night <= 6; night++)
            {
                string path = $"{OutputDir}/Night{night}.asset";
                NightConfig config = AssetDatabase.LoadAssetAtPath<NightConfig>(path);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<NightConfig>();
                    AssetDatabase.CreateAsset(config, path);
                }
                ApplyNight(config, night);
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[NightConfigFactory] 已确保 6 个夜晚配置存在（正篇 6 夜，自定义夜独立）");
        }

        private static void ApplyNight(NightConfig config, int night)
        {
            config.NightNumber = night;
            config.HourDuration = 75f;
            config.StartingPower = 99f;
            config.BaseDrainPerSecond = 0.104f;

            // (Freddy, Bonnie, Chica, Foxy) 参考原版难度曲线
            // 附加耗电间隔参考原版反编译：第 2 夜起每 6/5/4/3 秒多扣 0.1%（第 1 夜无附加）
            switch (night)
            {
                case 1:
                    config.NightlyDrainIntervalSeconds = 0f;
                    config.FreddyAI = 0; config.BonnieAI = 1; config.ChicaAI = 1; config.FoxyAI = 0;
                    break;
                case 2:
                    config.NightlyDrainIntervalSeconds = 6f;
                    config.FreddyAI = 0; config.BonnieAI = 3; config.ChicaAI = 2; config.FoxyAI = 1;
                    break;
                case 3:
                    config.NightlyDrainIntervalSeconds = 5f;
                    config.FreddyAI = 1; config.BonnieAI = 4; config.ChicaAI = 5; config.FoxyAI = 2;
                    break;
                case 4:
                    config.NightlyDrainIntervalSeconds = 4f;
                    config.FreddyAI = 2; config.BonnieAI = 5; config.ChicaAI = 7; config.FoxyAI = 4;
                    break;
                case 5:
                    config.NightlyDrainIntervalSeconds = 3f;
                    config.FreddyAI = 3; config.BonnieAI = 7; config.ChicaAI = 9; config.FoxyAI = 5;
                    break;
                default: // 第 6 夜 = 强化夜（原版 Night 6 曲线：10/12/4/6）
                    config.NightlyDrainIntervalSeconds = 3f;
                    config.FreddyAI = 4; config.BonnieAI = 10; config.ChicaAI = 12; config.FoxyAI = 6;
                    break;
            }
        }
    }
}
