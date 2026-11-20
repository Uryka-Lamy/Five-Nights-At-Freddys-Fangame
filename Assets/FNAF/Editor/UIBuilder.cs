using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FNAF.Monitor;
using FNAF.UI;

namespace FNAF.EditorTools
{
    /// <summary>监控地图条目：房间 ID + 摄像头编号 + 世界坐标（用于绘制平面图）。</summary>
    internal class MapEntry
    {
        public string Id;
        public string CamLabel;
        public Vector2 MapCenter; // 世界坐标 (x, z)
        public Vector2 MapSize;   // 世界尺寸 (宽, 深)
    }

    /// <summary>菜单展示台引用（SceneBuilder 构建，UIBuilder 消费：图鉴画面的 RT 与玩偶接线）。</summary>
    internal class MenuStageRefs
    {
        public RenderTexture ViewerRT;
        public Transform ViewerCamera;
        public GameObject[] ViewBodies = new GameObject[4];
    }

    /// <summary>UIBuilder 产出的运行时引用，交由 SceneBuilder 继续接线。</summary>
    internal class UIBuilderResult
    {
        public MainMenuUI MainMenu;
        public HUDController Hud;
        public AnimatronicViewerUI Viewer;
        public CustomNightUI CustomNight;
        public PhoneCallUI Phone;
        public GameObject HudPanel;
        public GameObject MonitorCanvasRoot;
        public RawImage StaticOverlay;
            public GameObject AudioOnlyNotice;
            public Image BlackOverlay;
            public GameObject ItsMeOverlay;
            public Text ItsMeText;
            public GameObject[] ItsMeFaces;
            public CanvasGroup OpenBarGroup;
            public CanvasGroup CloseBarGroup;
    }

    /// <summary>
    /// UI 构建器：程序化生成三大画布——主菜单（含片头/结算/跳杀覆盖层）、
    /// HUD（时间/电力/字幕）、监控画面（噪点层/房间标签/地图按钮）。
    /// 全部使用内置字体，无外部资源依赖。
    /// </summary>
    internal static class UIBuilder
    {
        private static Font font;

        // 平面图换算：世界坐标 → 地图像素（等比缩放，还原 FNAF 1 式楼层布局）
        private const float MapScale = 12f;   // 每世界单位对应的地图像素
        private const float MapOriginZ = 20f; // 世界 z=20 映射到地图垂直中心

        internal static UIBuilderResult Build(CameraFeedController feed, List<MapEntry> mapEntries, MenuStageRefs menuStage)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UIBuilderResult result = new UIBuilderResult();

            BuildHudCanvas(result);
            BuildMainMenuCanvas(result, menuStage);
            BuildMonitorCanvas(result, feed, mapEntries);

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            return result;
        }

        // ---------- 画布 1：主菜单 + 片头 + 结算 ----------

        private static void BuildMainMenuCanvas(UIBuilderResult result, MenuStageRefs menuStage)
        {
            Canvas canvas = CanvasRoot("UI_MainMenu", 30, true);
            MainMenuUI menu = canvas.gameObject.AddComponent<MainMenuUI>();
            NightEndUI end = canvas.gameObject.AddComponent<NightEndUI>();
            AnimatronicViewerUI viewer = canvas.gameObject.AddComponent<AnimatronicViewerUI>();
            CustomNightUI customNight = canvas.gameObject.AddComponent<CustomNightUI>();
            result.MainMenu = menu;
            result.Viewer = viewer;
            result.CustomNight = customNight;

            // 主菜单面板（FNAF 1 原版风格：全屏雪花 + 左上竖排标题 + 纯文字菜单项）
            GameObject menuPanel = StretchImage("MenuPanel", canvas.transform, Color.black).gameObject;
            RawImage staticImage = StretchRawImage("StaticImage", menuPanel.transform);

            // 注意：Point 辅助的 pivot 是中心点，左对齐元素需把 x 加上半个宽度，否则文字溢出左边界
            PointText("TitleLine1", menuPanel.transform, "Five", 108, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(542f, -108f), new Vector2(900f, 132f));
            PointText("TitleLine2", menuPanel.transform, "Nights", 108, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(542f, -240f), new Vector2(900f, 132f));
            PointText("TitleLine3", menuPanel.transform, "at", 72, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(542f, -352f), new Vector2(900f, 92f));
            PointText("TitleLine4", menuPanel.transform, "Freddy's", 108, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(542f, -460f), new Vector2(900f, 132f));

            // 标题下的评星：通第 5 夜、通第 6 夜、自定义夜全 20 各一颗白色五角星（运行时 menu.RefreshStars 填充）
            Text menuStars = PointText("MenuStars", menuPanel.transform, "", 48, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(500f, -560f), new Vector2(480f, 60f));
            Wire.Ref(menu, "menuStarsText", menuStars);

            Button newGameBtn = TextMenuButton("NewGameBtn", menuPanel.transform, "新游戏", 56,
                new Vector2(0f, 1f), new Vector2(336f, -656f), new Vector2(480f, 80f));
            UnityEventTools.AddVoidPersistentListener(newGameBtn.onClick, menu.OnNewGame);
            Button menuContinueBtn = TextMenuButton("ContinueBtn", menuPanel.transform, "继续", 56,
                new Vector2(0f, 1f), new Vector2(336f, -748f), new Vector2(480f, 80f));
            UnityEventTools.AddVoidPersistentListener(menuContinueBtn.onClick, menu.OnContinue);
            Button customBtn = TextMenuButton("CustomNightBtn", menuPanel.transform, "自定义夜", 56,
                new Vector2(0f, 1f), new Vector2(336f, -840f), new Vector2(480f, 80f));
            UnityEventTools.AddVoidPersistentListener(customBtn.onClick, menu.OnCustomNight);
            Button viewBtn = TextMenuButton("ViewAnimatronicsBtn", menuPanel.transform, "查看玩偶", 56,
                new Vector2(0f, 1f), new Vector2(336f, -932f), new Vector2(480f, 80f));
            UnityEventTools.AddVoidPersistentListener(viewBtn.onClick, menu.OnViewAnimatronics);

            // 保留的原有功能：左下角小号暗色入口
            Button resetBtn = TextMenuButton("ResetBtn", menuPanel.transform, "重置存档", 30,
                new Vector2(0f, 1f), new Vector2(238f, -1004f), new Vector2(300f, 52f));
            UnityEventTools.AddVoidPersistentListener(resetBtn.onClick, menu.ResetProgress);
            Button quitBtn = TextMenuButton("QuitBtn", menuPanel.transform, "退出游戏", 30,
                new Vector2(0f, 1f), new Vector2(560f, -1004f), new Vector2(300f, 52f));
            UnityEventTools.AddVoidPersistentListener(quitBtn.onClick, menu.QuitGame);

            // 菜单雪花氛围音（挂在菜单面板上，面板隐藏即自动停）
            AudioSource staticAudio = menuPanel.AddComponent<AudioSource>();
            staticAudio.clip = Clip("static_loop");
            staticAudio.loop = true;
            staticAudio.volume = 0.12f;
            staticAudio.playOnAwake = false;

            // ---------- 玩偶图鉴面板 ----------
            GameObject viewerPanel = StretchImage("ViewerPanel", canvas.transform, Color.black).gameObject;
            viewerPanel.SetActive(false);

            PointText("ViewerTitle", viewerPanel.transform, "玩 偶 图 鉴", 52, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(700f, 70f));

            // 左列切换按钮（FNAF 式纯文字菜单项）
            string[] viewerNames = { "弗 雷 迪", "邦 尼", "奇 卡", "狐 狸" };
            for (int i = 0; i < viewerNames.Length; i++)
            {
                Button selectBtn = TextMenuButton($"SelectBtn{i}", viewerPanel.transform, viewerNames[i], 34,
                    new Vector2(0f, 0.5f), new Vector2(186f, 225f - i * 130f), new Vector2(260f, 60f));
                UnityEventTools.AddIntPersistentListener(selectBtn.onClick, viewer.SelectAnimatronic, i);
            }

            // 中央展示台画面（ViewerCamera 的 RenderTexture）：居中偏右——
            // 1920 宽参考下中心 x=1000（屏幕中心 960 略右），左侧留给切换按钮、右侧留给信息栏
            RectTransform viewerImageRt = NewRect("ViewerImage", viewerPanel.transform);
            Point(viewerImageRt, new Vector2(0.5f, 0.5f), new Vector2(40f, -24f), new Vector2(700f, 700f));
            RawImage viewerImage = viewerImageRt.gameObject.AddComponent<RawImage>();
            viewerImage.texture = menuStage.ViewerRT;
            viewerImage.raycastTarget = false;

            Text nameText = PointText("ViewerNameText", viewerPanel.transform, "弗 雷 迪", 52, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(-300f, 296f), new Vector2(440f, 76f));
            // 名字底边在 y=798（540+296-38），描述顶部须低于它：y=74、高 360 → 顶部 794，留 4px 间隙
            Text descText = PointText("ViewerDescText", viewerPanel.transform, "舞台的主角，谨慎的猎手。", 26,
                new Color(0.75f, 0.75f, 0.78f), TextAnchor.UpperLeft, FontStyle.Normal,
                new Vector2(1f, 0.5f), new Vector2(-300f, 74f), new Vector2(440f, 360f));

            Button jumpscareBtn = PointButton("JumpscareBtn", viewerPanel.transform, "播放跳杀", 30,
                new Color(0.34f, 0.09f, 0.09f, 0.96f), Color.white,
                new Vector2(1f, 0.5f), new Vector2(-300f, -214f), new Vector2(320f, 76f));
            UnityEventTools.AddVoidPersistentListener(jumpscareBtn.onClick, viewer.PlayJumpscare);
            Button viewerBackBtn = PointButton("ViewerBackBtn", viewerPanel.transform, "返回主菜单", 26,
                new Color(0.12f, 0.12f, 0.14f, 0.96f), new Color(0.8f, 0.8f, 0.84f),
                new Vector2(1f, 0.5f), new Vector2(-300f, -326f), new Vector2(320f, 64f));
            UnityEventTools.AddVoidPersistentListener(viewerBackBtn.onClick, viewer.Close);

            // ---------- 自定义夜面板 ----------
            GameObject customPanel = StretchImage("CustomNightPanel", canvas.transform, Color.black).gameObject;
            customPanel.SetActive(false);

            PointText("CustomTitle", customPanel.transform, "自 定 义 夜", 52, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(700f, 70f));
            PointText("CustomSubTitle", customPanel.transform, "为每只玩偶设置 AI 等级（ 0 - 20，越高越凶；0 = 沉睡，整点苏醒一格后开始行动 ）", 26,
                new Color(0.6f, 0.6f, 0.64f), TextAnchor.MiddleCenter, FontStyle.Normal,
                new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(900f, 44f));

            Text[] levelTexts = new Text[4];
            string[] customNames = { "弗 雷 迪", "邦 尼", "奇 卡", "狐 狸" };
            for (int i = 0; i < customNames.Length; i++)
            {
                float rowY = 150f - i * 112f;
                PointText($"CustomName{i}", customPanel.transform, customNames[i], 36, Color.white,
                    TextAnchor.MiddleRight, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(-252f, rowY), new Vector2(240f, 58f));
                Button minusBtn = PointButton($"MinusBtn{i}", customPanel.transform, "-", 40,
                    new Color(0.2f, 0.2f, 0.23f, 0.96f), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(42f, rowY), new Vector2(76f, 76f));
                UnityEventTools.AddIntPersistentListener(minusBtn.onClick, customNight.OnMinus, i);
                levelTexts[i] = PointText($"LevelText{i}", customPanel.transform, "5", 52, Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(178f, rowY), new Vector2(120f, 76f));
                Button plusBtn = PointButton($"PlusBtn{i}", customPanel.transform, "+", 40,
                    new Color(0.2f, 0.2f, 0.23f, 0.96f), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(316f, rowY), new Vector2(76f, 76f));
                UnityEventTools.AddIntPersistentListener(plusBtn.onClick, customNight.OnPlus, i);
            }

            Button startCustomBtn = PointButton("StartCustomBtn", customPanel.transform, "开始自定义夜", 32,
                new Color(0.14f, 0.32f, 0.16f, 0.97f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(420f, 84f));
            UnityEventTools.AddVoidPersistentListener(startCustomBtn.onClick, customNight.StartCustomNight);
            Button customBackBtn = PointButton("CustomBackBtn", customPanel.transform, "返回主菜单", 26,
                new Color(0.12f, 0.12f, 0.14f, 0.96f), new Color(0.8f, 0.8f, 0.84f),
                new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(300f, 64f));
            UnityEventTools.AddVoidPersistentListener(customBackBtn.onClick, customNight.Close);

            // 夜晚片头面板
            GameObject introPanel = StretchImage("IntroPanel", canvas.transform, Color.black).gameObject;
            introPanel.SetActive(false);
            Text introText = PointText("IntroText", introPanel.transform, "第 1 夜", 96, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 130f));

            // 6AM 胜利面板
            GameObject victoryPanel = StretchImage("VictoryPanel", canvas.transform,
                new Color(0f, 0f, 0f, 0.92f)).gameObject;
            victoryPanel.SetActive(false);
            PointText("SixAMText", victoryPanel.transform, "6 AM", 130, new Color(0.91f, 0.76f, 0.29f),
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(800f, 150f));
            Text victorySub = PointText("VictorySubText", victoryPanel.transform, "第 1 夜 · 完成！", 40, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 60f));
            Button continueBtn = PointButton("ContinueBtn", victoryPanel.transform, "继  续", 32,
                new Color(0.16f, 0.24f, 0.14f, 0.95f), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(360f, 74f));
            UnityEventTools.AddVoidPersistentListener(continueBtn.onClick, end.OnContinueClicked);

            // 游戏结束面板
            GameObject gameOverPanel = StretchImage("GameOverPanel", canvas.transform,
                new Color(0f, 0f, 0f, 0.94f)).gameObject;
            gameOverPanel.SetActive(false);
            PointText("CaughtText", gameOverPanel.transform, "GAME OVER", 88, new Color(0.72f, 0.12f, 0.12f),
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1300f, 120f));
            Button retryBtn = PointButton("RetryBtn", gameOverPanel.transform, "重试本夜", 30,
                new Color(0.2f, 0.12f, 0.12f, 0.95f), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(360f, 72f));
            UnityEventTools.AddVoidPersistentListener(retryBtn.onClick, end.RetryNight);
            Button backBtn = PointButton("BackBtn", gameOverPanel.transform, "返回主菜单", 28,
                new Color(0.12f, 0.12f, 0.14f, 0.95f), new Color(0.78f, 0.78f, 0.8f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(360f, 72f));
            UnityEventTools.AddVoidPersistentListener(backBtn.onClick, end.BackToMenu);

            // 第 5 夜通关支票画面（原版同款：Fazbear 周薪支票 $120.50，按任意键返回主菜单）
            GameObject paycheckPanel = StretchImage("PaycheckPanel", canvas.transform, Color.black).gameObject;
            paycheckPanel.SetActive(false);
            RectTransform chequeBorderRt = NewRect("ChequeBorder", paycheckPanel.transform);
            Point(chequeBorderRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(984f, 444f));
            chequeBorderRt.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.3f, 0.23f);
            RectTransform chequeRt = NewRect("ChequePaper", paycheckPanel.transform);
            Point(chequeRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(960f, 420f));
            chequeRt.gameObject.AddComponent<Image>().color = new Color(0.93f, 0.91f, 0.8f);
            chequeRt.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
            Color inkBlue = new Color(0.13f, 0.2f, 0.38f);
            PointText("ChequeBrand", chequeRt, "FREDDY FAZBEAR'S PIZZA", 26, inkBlue,
                TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(-436f, -26f), new Vector2(520f, 36f));
            PointText("ChequeNo", chequeRt, "NO. 000701", 22, inkBlue,
                TextAnchor.UpperRight, FontStyle.Normal, new Vector2(1f, 1f), new Vector2(436f, -28f), new Vector2(240f, 32f));
            PointText("ChequeDate", chequeRt, "DATE 11-12-92", 22, inkBlue,
                TextAnchor.UpperRight, FontStyle.Normal, new Vector2(1f, 1f), new Vector2(436f, -66f), new Vector2(240f, 32f));
            PointText("ChequePayLabel", chequeRt, "PAY TO THE ORDER OF", 20, inkBlue,
                TextAnchor.MiddleLeft, FontStyle.Normal, new Vector2(0f, 0.5f), new Vector2(-436f, 34f), new Vector2(320f, 30f));
            PointText("ChequePayee", chequeRt, "Mike Schmidt", 34, inkBlue,
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.5f), new Vector2(-436f, -12f), new Vector2(420f, 44f));
            PointText("ChequeAmount", chequeRt, "$120.50", 44, new Color(0.72f, 0.1f, 0.1f),
                TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(432f, -12f), new Vector2(280f, 56f));
            PointText("ChequeAmountWords", chequeRt, "ONE HUNDRED TWENTY AND 50/100 DOLLARS", 18, inkBlue,
                TextAnchor.MiddleLeft, FontStyle.Italic, new Vector2(0f, 0.5f), new Vector2(-436f, -72f), new Vector2(700f, 30f));
            PointText("ChequeMemo", chequeRt, "MEMO: 五夜周薪 · 干得漂亮，夜班保安", 20, inkBlue,
                TextAnchor.LowerLeft, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(-436f, 28f), new Vector2(520f, 32f));
            PointText("ChequeSign", chequeRt, "Fazbear Entertainment", 30, inkBlue,
                TextAnchor.LowerRight, FontStyle.Italic, new Vector2(1f, 0f), new Vector2(432f, 56f), new Vector2(380f, 42f));
            PointText("ChequeHint", paycheckPanel.transform, "按任意键继续…", 30, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(600f, 48f));

            // 跳杀覆盖层（最后创建 = 画布最顶层）
            result.BlackOverlay = StretchImage("BlackOverlay", canvas.transform, Color.black);
            result.BlackOverlay.raycastTarget = false;
            result.BlackOverlay.gameObject.SetActive(false);

            // GAME OVER 面板必须渲染在跳杀黑幕之上（否则被黑幕遮住，黑屏卡死的根源）
            gameOverPanel.transform.SetAsLastSibling();

            // 接线
            Wire.Ref(menu, "menuPanel", menuPanel);
            Wire.Ref(menu, "introPanel", introPanel);
            Wire.Ref(menu, "introText", introText);
            Wire.Ref(menu, "hudPanel", result.HudPanel);
            Wire.Ref(menu, "staticImage", staticImage);
            Wire.Ref(menu, "staticAudio", staticAudio);
            Wire.Ref(menu, "animatronicViewer", viewer);
            Wire.Ref(menu, "customNight", customNight);
            Wire.Ref(menu, "customNightButton", customBtn.gameObject);
            Wire.Ref(menu, "viewAnimatronicsButton", viewBtn.gameObject);

            Wire.Ref(viewer, "viewerPanel", viewerPanel);
            Wire.Ref(viewer, "viewerImage", viewerImage);
            Wire.Ref(viewer, "nameText", nameText);
            Wire.Ref(viewer, "descText", descText);
            Wire.Ref(viewer, "viewerCamera", menuStage.ViewerCamera);
            Wire.RefArray(viewer, "viewBodies", menuStage.ViewBodies);

            Wire.Ref(customNight, "panel", customPanel);
            Wire.RefArray(customNight, "levelTexts", levelTexts);

            Wire.Ref(end, "victoryPanel", victoryPanel);
            Wire.Ref(end, "victorySubText", victorySub);
            Wire.Ref(end, "gameOverPanel", gameOverPanel);
            Wire.Ref(end, "paycheckPanel", paycheckPanel);
            Wire.Ref(end, "jumpscareBlack", result.BlackOverlay.gameObject);
        }

        // ---------- 画布 2：HUD + 电话字幕 ----------

        private static void BuildHudCanvas(UIBuilderResult result)
        {
            Canvas canvas = CanvasRoot("UI_HUD", 10, true);
            HUDController hud = canvas.gameObject.AddComponent<HUDController>();
            PhoneCallUI phone = canvas.gameObject.AddComponent<PhoneCallUI>();
            result.Hud = hud;
            result.Phone = phone;

            GameObject hudPanel = NewRect("HUDPanel", canvas.transform).gameObject;
            Stretch((RectTransform)hudPanel.transform, Vector2.zero, Vector2.zero);
            hudPanel.SetActive(false);
            result.HudPanel = hudPanel;

            Text timeText = PointText("TimeText", hudPanel.transform, "12 AM", 46, Color.white,
                TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(1f, 1f), new Vector2(-150f, -64f), new Vector2(280f, 60f));
            Text nightText = PointText("NightText", hudPanel.transform, "第 1 夜", 30, new Color(0.85f, 0.85f, 0.88f),
                TextAnchor.MiddleRight, FontStyle.Normal, new Vector2(1f, 1f), new Vector2(-150f, -126f), new Vector2(280f, 44f));

            Text powerText = PointText("PowerText", hudPanel.transform, "剩余电量: 99%", 30, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(210f, 66f), new Vector2(430f, 48f));
            PointText("UsageLabel", hudPanel.transform, "用电:", 24, new Color(0.85f, 0.85f, 0.88f),
                TextAnchor.MiddleLeft, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(150f, 28f), new Vector2(90f, 36f));

            Image[] bars = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                RectTransform rt = NewRect($"UsageBar{i}", hudPanel.transform);
                Point(rt, new Vector2(0f, 0f), new Vector2(230f + i * 38f, 28f), new Vector2(28f, 40f));
                bars[i] = rt.gameObject.AddComponent<Image>();
                bars[i].color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            }

            // （右下角操作提示文字已按需求移除）

            // 底部中央▲标识：FNAF 1 式，鼠标滑入即拉起监控（热区由 MonitorController 判定）
            result.OpenBarGroup = BuildMonitorToggleBar(hudPanel.transform, "▲");

            // 挂断电话按钮：游玩界面左上角，电话开播 60 秒内显示（窗口逻辑在 PhoneCallUI）
            Button hangupBtn = PointButton("HangupBtn", hudPanel.transform, "挂断电话", 26,
                new Color(0.32f, 0.1f, 0.08f, 0.95f), Color.white,
                new Vector2(0f, 1f), new Vector2(120f, -44f), new Vector2(210f, 64f));
            hangupBtn.gameObject.SetActive(false);
            UnityEventTools.AddVoidPersistentListener(hangupBtn.onClick, phone.OnHangupClicked);

            // IT'S ME 覆盖层（金熊彩蛋：闪现幻觉文字；位置/字号由 GoldenFreddyController 随机改）
            Image itsMeBlack = StretchImage("ItsMeOverlay", canvas.transform, Color.black);
            itsMeBlack.raycastTarget = false;
            itsMeBlack.gameObject.SetActive(false);
            Text itsMeText = PointText("ItsMeText", itsMeBlack.transform, "IT'S ME", 100, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 300f));
            itsMeText.raycastTarget = false;
            result.ItsMeOverlay = itsMeBlack.gameObject;
            result.ItsMeText = itsMeText;

            // 四只玩偶的简笔正脸（金熊幻觉时随机闪现，默认隐藏）
            result.ItsMeFaces = new[]
            {
                BuildItsMeFace(itsMeBlack.transform, "FaceFreddy", 0, new Color(0.42f, 0.27f, 0.14f), new Color(0.56f, 0.43f, 0.24f)),
                BuildItsMeFace(itsMeBlack.transform, "FaceBonnie", 1, new Color(0.35f, 0.25f, 0.56f), new Color(0.5f, 0.38f, 0.76f)),
                BuildItsMeFace(itsMeBlack.transform, "FaceChica", 2, new Color(0.85f, 0.74f, 0.16f), new Color(0.83f, 0.47f, 0.1f)),
                BuildItsMeFace(itsMeBlack.transform, "FaceFoxy", 3, new Color(0.55f, 0.16f, 0.12f), new Color(0.73f, 0.31f, 0.21f)),
            };

            Wire.Ref(hud, "timeText", timeText);
            Wire.Ref(hud, "nightText", nightText);
            Wire.Ref(hud, "powerText", powerText);
            Wire.RefArray(hud, "usageBars", bars);

            Wire.Ref(phone, "hangupButton", hangupBtn.gameObject);

            // ESC 退出确认画布（order 30：盖过监控与跳杀黑幕，任意游玩状态下弹窗都可见）
            Canvas quitCanvas = CanvasRoot("UI_Quit", 30, true);
            EscapeQuitUI quit = quitCanvas.gameObject.AddComponent<EscapeQuitUI>();
            GameObject quitPanel = StretchImage("QuitConfirmPanel", quitCanvas.transform,
                new Color(0f, 0f, 0f, 0.88f)).gameObject;
            quitPanel.SetActive(false);
            PointText("QuitTitle", quitPanel.transform, "退 出 本 夜 ？", 56, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 100f));
            PointText("QuitWarn", quitPanel.transform, "退出后不会保留本夜进度", 30, new Color(0.78f, 0.78f, 0.82f),
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(900f, 50f));
            Button quitResumeBtn = PointButton("QuitResumeBtn", quitPanel.transform, "继续游玩", 30,
                new Color(0.16f, 0.24f, 0.14f, 0.95f), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(360f, 72f));
            UnityEventTools.AddVoidPersistentListener(quitResumeBtn.onClick, quit.Resume);
            Button quitConfirmBtn = PointButton("QuitConfirmBtn", quitPanel.transform, "退出本夜", 30,
                new Color(0.2f, 0.12f, 0.12f, 0.95f), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(360f, 72f));
            UnityEventTools.AddVoidPersistentListener(quitConfirmBtn.onClick, quit.QuitToMenu);
            Wire.Ref(quit, "confirmPanel", quitPanel);
        }

        // ---------- 画布 3：监控画面 ----------

        private static void BuildMonitorCanvas(UIBuilderResult result, CameraFeedController feed, List<MapEntry> mapEntries)
        {
            Canvas canvas = CanvasRoot("UI_Monitor", 20, false);
            result.MonitorCanvasRoot = canvas.gameObject;

            RectTransform staticRt = NewRect("StaticOverlay", canvas.transform);
            Stretch(staticRt, Vector2.zero, Vector2.zero);
            result.StaticOverlay = staticRt.gameObject.AddComponent<RawImage>();
            result.StaticOverlay.color = Color.white;

            Text roomLabel = PointText("RoomLabel", canvas.transform, "表演舞台", 44, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1100f, 64f));
            Wire.Ref(feed, "roomNameLabel", roomLabel);

            RectTransform noticeRt = NewRect("AudioOnlyNotice", canvas.transform);
            Point(noticeRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 90f));
            result.AudioOnlyNotice = noticeRt.gameObject;
            PointText("NoticeText", noticeRt, "—— 信 号 不 良 · 仅 音 频 ——", 42,
                new Color(0.85f, 0.8f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 90f));
            noticeRt.gameObject.SetActive(false);

            // 监控地图面板：按房间真实比例绘制的平面图，CAM 按钮统一大小居中放在房间块上
            // 背景半透明：不遮挡身后的监控画面
            RectTransform mapRt = NewRect("MapPanel", canvas.transform);
            Point(mapRt, new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(510f, 780f));
            Image mapBg = mapRt.gameObject.AddComponent<Image>();
            mapBg.color = new Color(0.04f, 0.05f, 0.07f, 0.45f);
            MonitorMapUI mapUi = mapRt.gameObject.AddComponent<MonitorMapUI>();

            PointText("MapTitle", mapRt, "监 控 地 图", 32, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0f, 350f), new Vector2(400f, 50f));
            PointText("RecText", mapRt, "● REC", 26, new Color(0.9f, 0.15f, 0.15f),
                TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(-165f, 350f), new Vector2(170f, 40f));

            // 走廊连接块（纯装饰，让平面图结构连贯）
            MapCorridor(mapRt, new Vector2(-10f, 22f), new Vector2(4f, 8f));  // 餐厅→西走廊
            MapCorridor(mapRt, new Vector2(10f, 22f), new Vector2(4f, 8f));   // 餐厅→东走廊
            MapCorridor(mapRt, new Vector2(-6.75f, 3f), new Vector2(6.5f, 2f)); // 西门口→办公室
            MapCorridor(mapRt, new Vector2(6.75f, 3f), new Vector2(6.5f, 2f));  // 东门口→办公室

            // 房间块 + 摄像头按钮（按钮统一大小，居中放在对应房间块的平面位置上）
            foreach (MapEntry entry in mapEntries)
            {
                Vector2 center = WorldToMap(entry.MapCenter);
                Vector2 size = entry.MapSize * MapScale;

                RectTransform roomRt = NewRect($"MapRoom_{entry.Id}", mapRt);
                Point(roomRt, new Vector2(0.5f, 0.5f), center, size);
                Image roomBg = roomRt.gameObject.AddComponent<Image>();
                roomBg.color = new Color(0.10f, 0.12f, 0.17f, 0.72f);

                Button btn = PointButton($"MapBtn_{entry.Id}", roomRt, entry.CamLabel, 16,
                    new Color(0.14f, 0.18f, 0.24f, 0.95f), Color.white,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 38f));
                UnityEventTools.AddStringPersistentListener(btn.onClick, feed.SelectRoom, entry.Id);
                mapUi.Register(entry.Id, btn.image);
            }

            // 办公室（玩家位置标记，非监控点）
            RectTransform officeRt = NewRect("MapOffice", mapRt);
            Point(officeRt, new Vector2(0.5f, 0.5f), WorldToMap(Vector2.zero), new Vector2(6.4f, 4.4f) * MapScale);
            Image officeBg = officeRt.gameObject.AddComponent<Image>();
            officeBg.color = new Color(0.13f, 0.24f, 0.14f, 0.72f);
            PointText("MapYou", officeRt, "你在这", 17, new Color(0.7f, 0.95f, 0.7f),
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 52f));

            // 切房高亮接线：feed.mapHighlight → 地图组件
            SerializedObject feedSo = new SerializedObject(feed);
            feedSo.FindProperty("mapHighlight").objectReferenceValue = mapUi;
            feedSo.ApplyModifiedPropertiesWithoutUndo();

            // 底部中央▼标识：监控视角下鼠标滑入即放下监控（噪点层之后创建，渲染在其上）
            result.CloseBarGroup = BuildMonitorToggleBar(canvas.transform, "▼");
        }

        /// <summary>
        /// FNAF 1 式底部切换标识：白描边黑芯横条 + 居中箭头，贴屏幕底部中央。
        /// raycastTarget 全关（不挡 3D 场景里墙上按钮的 OnMouseDown 点击）。
        /// </summary>
        private static CanvasGroup BuildMonitorToggleBar(Transform parent, string arrow)
        {
            RectTransform rt = NewRect("MonitorToggleBar", parent);
            Point(rt, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(760f, 54f));
            Image frame = rt.gameObject.AddComponent<Image>();
            frame.color = new Color(1f, 1f, 1f, 0.9f);
            frame.raycastTarget = false;

            RectTransform inner = NewRect("BarInner", rt);
            Point(inner, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(754f, 48f));
            Image innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.color = new Color(0f, 0f, 0f, 0.82f);
            innerImg.raycastTarget = false;

            Text arrowText = PointText("BarArrow", inner, arrow, 34, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 46f));
            arrowText.raycastTarget = false;

            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.65f;
            return group;
        }

        // ---------- 通用 UI 辅助 ----------

        /// <summary>世界坐标 (x, z) → 地图面板像素坐标（+x 向右，+z 向上；办公室在下方、海盗湾在左，FNAF 1 式）。</summary>
        private static Vector2 WorldToMap(Vector2 world)
        {
            return new Vector2(world.x * MapScale, (world.y - MapOriginZ) * MapScale);
        }

        /// <summary>平面图上的装饰性走廊块（不拦截点击）。</summary>
        private static void MapCorridor(Transform mapRt, Vector2 worldCenter, Vector2 worldSize)
        {
            RectTransform rt = NewRect("MapCorridor", mapRt);
            Point(rt, new Vector2(0.5f, 0.5f), WorldToMap(worldCenter), worldSize * MapScale);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.07f, 0.09f, 0.12f, 0.55f);
            img.raycastTarget = false;
        }

        /// <summary>幻觉正脸的组成色块（不拦截点击）。</summary>
        private static Image FacePart(Transform parent, string name, Vector2 pos, Vector2 size, Color color, float zRot = 0f)
        {
            RectTransform rt = NewRect(name, parent);
            Point(rt, new Vector2(0.5f, 0.5f), pos, size);
            if (Mathf.Abs(zRot) > 0.01f) rt.localEulerAngles = new Vector3(0f, 0f, zRot);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 构建金熊幻觉用的玩偶简笔正脸（UI 色块拼成，默认隐藏，由 GoldenFreddyController 控制闪现）。
        /// style：0 弗雷迪（圆耳+礼帽）、1 邦尼（长耳）、2 奇卡（上下喙）、3 福克斯（尖耳+眼罩）。
        /// </summary>
        private static GameObject BuildItsMeFace(Transform parent, string faceName, int style, Color main, Color accent)
        {
            GameObject root = new GameObject(faceName);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = new Vector2(520f, 560f);

            Color dark = new Color(0.05f, 0.05f, 0.05f);

            // 头顶特征（画在头后面）
            switch (style)
            {
                case 0: // 弗雷迪圆耳
                    FacePart(root.transform, "EarL", new Vector2(-118f, 92f), new Vector2(72f, 72f), main);
                    FacePart(root.transform, "EarR", new Vector2(118f, 92f), new Vector2(72f, 72f), main);
                    break;
                case 1: // 邦尼长耳
                    FacePart(root.transform, "EarL", new Vector2(-80f, 200f), new Vector2(62f, 180f), main);
                    FacePart(root.transform, "EarR", new Vector2(80f, 200f), new Vector2(62f, 180f), main);
                    break;
                case 2: // 奇卡头顶呆毛
                    FacePart(root.transform, "Tuft", new Vector2(0f, 122f), new Vector2(48f, 44f), main);
                    break;
                default: // 福克斯尖耳 + 眼罩带
                    FacePart(root.transform, "EarL", new Vector2(-112f, 108f), new Vector2(58f, 86f), main, 28f);
                    FacePart(root.transform, "EarR", new Vector2(112f, 108f), new Vector2(58f, 86f), main, -28f);
                    FacePart(root.transform, "Strap", new Vector2(0f, 66f), new Vector2(250f, 14f), dark);
                    break;
            }

            // 头 + 黑空眼窝
            FacePart(root.transform, "Head", new Vector2(0f, -10f), new Vector2(300f, 260f), main);
            FacePart(root.transform, "EyeL", new Vector2(-62f, 28f), new Vector2(46f, 56f), dark);
            FacePart(root.transform, "EyeR", new Vector2(62f, 28f), new Vector2(46f, 56f), dark);

            // 口鼻特征（画在最上层）
            switch (style)
            {
                case 0: // 弗雷迪：礼帽 + 圆吻
                    FacePart(root.transform, "HatBrim", new Vector2(0f, 118f), new Vector2(156f, 16f), dark);
                    FacePart(root.transform, "HatCrown", new Vector2(0f, 154f), new Vector2(100f, 72f), dark);
                    FacePart(root.transform, "Snout", new Vector2(0f, -66f), new Vector2(130f, 64f), accent);
                    FacePart(root.transform, "Nose", new Vector2(0f, -40f), new Vector2(36f, 22f), dark);
                    break;
                case 1: // 邦尼：圆吻
                    FacePart(root.transform, "Snout", new Vector2(0f, -66f), new Vector2(130f, 64f), accent);
                    FacePart(root.transform, "Nose", new Vector2(0f, -40f), new Vector2(36f, 22f), dark);
                    break;
                case 2: // 奇卡：上下喙
                    FacePart(root.transform, "BeakTop", new Vector2(0f, -58f), new Vector2(150f, 44f), accent);
                    FacePart(root.transform, "BeakBottom", new Vector2(0f, -92f), new Vector2(110f, 28f),
                        new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f));
                    break;
                default: // 福克斯：长吻 + 眼罩盖住右眼
                    FacePart(root.transform, "Snout", new Vector2(0f, -76f), new Vector2(170f, 58f), accent);
                    FacePart(root.transform, "Nose", new Vector2(0f, -48f), new Vector2(34f, 22f), dark);
                    FacePart(root.transform, "Patch", new Vector2(66f, 28f), new Vector2(74f, 64f), dark);
                    break;
            }
            return root;
        }

        private static Canvas CanvasRoot(string name, int sortOrder, bool active)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void Point(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image StretchImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.zero);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static RawImage StretchRawImage(string name, Transform parent)
        {
            RectTransform rt = NewRect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.zero);
            return rt.gameObject.AddComponent<RawImage>();
        }

        /// <summary>FNAF 式纯文字菜单项：无底图无描边，常态暗白、悬停提亮为纯白。</summary>
        private static Button TextMenuButton(string name, Transform parent, string label, int fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform rt = NewRect(name, parent);
            Point(rt, anchor, pos, size);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = label;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = true;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = t;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.68f, 0.68f, 0.68f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.45f, 0.45f, 0.45f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f);
            btn.colors = colors;
            return btn;
        }

        /// <summary>取音频：Audio/Recorded/ 下有同名用户录音则优先（与 SceneBuilder.Clip 同规则），否则用程序化合成版。</summary>
        private static AudioClip Clip(string name)
        {
            AudioClip recorded = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/FNAF/Audio/Recorded/{name}.wav");
            if (recorded != null) return recorded;
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/FNAF/Audio/{name}.wav");
        }

        private static Text PointText(string name, Transform parent, string content, int size, Color color,
            TextAnchor align, FontStyle style, Vector2 anchor, Vector2 pos, Vector2 rectSize)
        {
            RectTransform rt = NewRect(name, parent);
            Point(rt, anchor, pos, rectSize);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static Button PointButton(string name, Transform parent, string label, int fontSize,
            Color bg, Color labelColor, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform rt = NewRect(name, parent);
            Point(rt, anchor, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            Button btn = rt.gameObject.AddComponent<Button>();
            PointText("Label", rt, label, fontSize, labelColor,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return btn;
        }
    }
}
