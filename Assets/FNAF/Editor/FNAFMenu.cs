using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FNAF.EditorTools
{
    /// <summary>FNAF 工具菜单：一键生成全部游戏内容（音效/夜晚配置/场景）。</summary>
    public static class FNAFMenu
    {
        private const string ConfirmText =
            "音效、夜晚配置与游戏场景已全部生成。\n\n" +
            "场景 FNAF_Main 已保存并加入 Build Settings。\n" +
            "双击打开该场景，点 ▶ Play 即可从主菜单开始游戏。";

        [MenuItem("FNAF/一键完成全部（音效+配置+场景）", false, 0)]
        public static void BuildEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 音效与配置由 SceneBuilder.Build 内部的 EnsureAssets 自足生成，这里不再重复跑
            // （重复跑会让 WavGenerator 用随机数重写合成 wav，触发无谓的重导入）
            SceneBuilder.Build();

            EditorUtility.DisplayDialog("FNAF 生成完成", ConfirmText, "开始游玩");
        }

        [MenuItem("FNAF/仅重新生成音效与夜晚配置", false, 20)]
        public static void GenerateAssets()
        {
            WavGenerator.GenerateAll();
            NightConfigFactory.EnsureAll();
            EditorUtility.DisplayDialog("FNAF", "音效与夜晚配置已重新生成。", "好");
        }

        [MenuItem("FNAF/仅重新生成游戏场景", false, 21)]
        public static void GenerateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneBuilder.Build();
            EditorUtility.DisplayDialog("FNAF", "游戏场景已重新生成并保存。", "好");
        }
    }
}
