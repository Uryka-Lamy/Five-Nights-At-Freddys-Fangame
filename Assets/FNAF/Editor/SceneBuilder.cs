using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FNAF.Core;
using FNAF.Data;
using FNAF.Enemies;
using FNAF.Monitor;
using FNAF.Office;
using FNAF.UI;

namespace FNAF.EditorTools
{
    /// <summary>
    /// 场景构建器：一键生成完整游戏场景——11 个监控房间 + 左右门外隔间 + 办公室
    /// （门/按钮/风扇/张望相机）+ 四只积木玩偶 + 监控系统 + 三大 UI 画布 + 程序化音频，
    /// 全部字段接线完成后保存 FNAF_Main 场景并加入 Build Settings。
    /// </summary>
    internal static class SceneBuilder
    {
        private const string ScenePath = "Assets/FNAF/Scenes/FNAF_Main.unity";
        private const string AudioDir = "Assets/FNAF/Audio";
        private const string ConfigDir = "Assets/FNAF/Configs";

        internal class RoomSpec
        {
            public string Id;
            public string Name;
            public string CamLabel;
            public Vector3 Center;
            public float W, D;
            public bool AudioOnly;
            public bool ShowOnMonitor = true;
            public bool SkipEastWall;
            public bool SkipWestWall;
            public bool AnchorFaceSouth; // 机位放南边（正面视角：海盗湾看帘、舞台看玩偶正面）
            public bool HasCurtain;
            public bool CameraPans = true; // 监控机位左右缓慢往复旋转（杂物间/海盗湾固定）
            public float CameraPanMin = -12f; // 往复旋转最小偏角（负=向左）
            public float CameraPanMax = 12f;  // 往复旋转最大偏角（正=向右）
            public string[] Adjacent = new string[0];
        }

        internal class BuiltRoom
        {
            public RoomSpec Spec;
            public GameObject Root;
            public Transform Anchor;
            public List<Transform> Stops = new List<Transform>();
            public Transform CurtainL, CurtainR;
        }

        internal class OfficeRefs
        {
            public Camera OfficeCamera;
            public DoorController LeftDoor;
            public DoorController RightDoor;
            public GameObject OfficeLight;
            public GameObject JumpscareLight;
        }

        internal class DecorMats
        {
            public Material PosterCream, PosterBlue, PosterGreen, Frame;
            public Material BuntingA, BuntingB, BuntingC;
            public Material Wood, Crate, Gold, Bone, DarkGray, Pink;
            public Material Porcelain, Mirror, StagePurple, StarYellow, Bristle, Cloth;
        }

        private static readonly RoomSpec[] Specs =
        {
            new RoomSpec { Id = RoomIds.ShowStage, Name = "表演舞台", CamLabel = "CAM 1A", Center = new Vector3(0, 0, 40), W = 10, D = 6, AnchorFaceSouth = true, CameraPanMin = 0f, CameraPanMax = 18f, Adjacent = new[] { RoomIds.DiningArea, RoomIds.Backstage } },
            new RoomSpec { Id = RoomIds.DiningArea, Name = "餐厅", CamLabel = "CAM 1B", Center = new Vector3(0, 0, 30), W = 14, D = 8, Adjacent = new[] { RoomIds.ShowStage, RoomIds.PirateCove, RoomIds.WestHall, RoomIds.EastHall, RoomIds.Restrooms, RoomIds.Kitchen } },
            new RoomSpec { Id = RoomIds.PirateCove, Name = "海盗湾", CamLabel = "CAM 1C", Center = new Vector3(-15, 0, 30), W = 6, D = 6, AnchorFaceSouth = true, HasCurtain = true, CameraPans = false, Adjacent = new[] { RoomIds.DiningArea } },
            new RoomSpec { Id = RoomIds.WestHall, Name = "西走廊", CamLabel = "CAM 2A", Center = new Vector3(-10, 0, 14), W = 4, D = 8, CameraPanMin = 0f, CameraPanMax = 18f, Adjacent = new[] { RoomIds.WestHallCorner, RoomIds.DiningArea } },
            new RoomSpec { Id = RoomIds.WestHallCorner, Name = "西走廊角落", CamLabel = "CAM 2B", Center = new Vector3(-10, 0, 6), W = 4, D = 4, Adjacent = new[] { RoomIds.WestHall, RoomIds.LeftDoor } },
            new RoomSpec { Id = RoomIds.SupplyCloset, Name = "杂物间", CamLabel = "CAM 3", Center = new Vector3(-16, 0, 14), W = 4, D = 4, CameraPans = false, Adjacent = new[] { RoomIds.WestHall } },
            new RoomSpec { Id = RoomIds.EastHall, Name = "东走廊", CamLabel = "CAM 4A", Center = new Vector3(10, 0, 14), W = 4, D = 8, CameraPanMin = -6f, CameraPanMax = 6f, Adjacent = new[] { RoomIds.EastHallCorner, RoomIds.DiningArea } },
            new RoomSpec { Id = RoomIds.EastHallCorner, Name = "东走廊角落", CamLabel = "CAM 4B", Center = new Vector3(10, 0, 6), W = 4, D = 4, CameraPanMin = -12f, CameraPanMax = 6f, Adjacent = new[] { RoomIds.EastHall, RoomIds.RightDoor } },
            new RoomSpec { Id = RoomIds.Backstage, Name = "后台", CamLabel = "CAM 5", Center = new Vector3(-15, 0, 40), W = 6, D = 6, CameraPanMin = 9f, CameraPanMax = 24f, Adjacent = new[] { RoomIds.ShowStage } },
            new RoomSpec { Id = RoomIds.Kitchen, Name = "厨房", CamLabel = "CAM 6", Center = new Vector3(15, 0, 40), W = 6, D = 6, AudioOnly = true, Adjacent = new[] { RoomIds.DiningArea, RoomIds.Restrooms } },
            new RoomSpec { Id = RoomIds.Restrooms, Name = "洗手间", CamLabel = "CAM 7", Center = new Vector3(15, 0, 30), W = 6, D = 6, CameraPanMin = -12f, CameraPanMax = 6f, Adjacent = new[] { RoomIds.DiningArea, RoomIds.Kitchen } },
            new RoomSpec { Id = RoomIds.LeftDoor, Name = "西门外", CamLabel = null, Center = new Vector3(-4.75f, 0, 0), W = 3, D = 4, ShowOnMonitor = false, SkipEastWall = true, Adjacent = new[] { RoomIds.WestHallCorner } },
            new RoomSpec { Id = RoomIds.RightDoor, Name = "东门外", CamLabel = null, Center = new Vector3(4.75f, 0, 0), W = 3, D = 4, ShowOnMonitor = false, SkipWestWall = true, Adjacent = new[] { RoomIds.EastHallCorner } },
        };

        internal static void Build()
        {
            EnsureAssets();
            // 抓取旧场景中各音源的音量：用户在编辑器里调的音量优先于代码默认值（调完保存场景即可，重跑一键不丢）
            // 版本号机制：代码改默认音量时把 VolumeDefaultsVersion +1，本次 Build 忽略旧记忆、采用新默认值（之后恢复记忆）
            Dictionary<string, float> prevVolumes =
                EditorPrefs.GetInt(VolumeVersionKey) == VolumeDefaultsVersion
                    ? CollectSourceVolumes()
                    : null;
            EditorPrefs.SetInt(VolumeVersionKey, VolumeDefaultsVersion);
            // 这两个源的音量按用户反馈调整代码默认值（用户尚未手动调过，忽略旧场景值让新默认生效）
            prevVolumes?.Remove("Src_CamSwitch");
            prevVolumes?.Remove("Src_Ambient");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            pendingVolumes = prevVolumes;

            // 材质
            Material floorMat = AnimatronicFactory.StandardMat(new Color(0.10f, 0.10f, 0.12f), 0.08f);
            Material wallMat = AnimatronicFactory.StandardMat(new Color(0.14f, 0.14f, 0.17f), 0.05f);
            Material ceilingMat = AnimatronicFactory.StandardMat(new Color(0.08f, 0.08f, 0.10f), 0.05f);
            Material doorMat = AnimatronicFactory.StandardMat(new Color(0.34f, 0.36f, 0.40f), 0.6f);
            Material deskMat = AnimatronicFactory.StandardMat(new Color(0.23f, 0.17f, 0.12f), 0.2f);
            Material curtainMat = AnimatronicFactory.StandardMat(new Color(0.28f, 0.15f, 0.36f), 0.3f);
            Material buttonRedMat = AnimatronicFactory.StandardMat(new Color(0.55f, 0.14f, 0.14f), 0.4f);
            Material buttonPaleMat = AnimatronicFactory.StandardMat(new Color(0.82f, 0.82f, 0.82f), 0.4f);
            Material metalMat = AnimatronicFactory.StandardMat(new Color(0.50f, 0.53f, 0.58f), 0.55f);

            // 系统根
            GameObject systems = new GameObject("FNAF_Systems");
            RoomRegistry registry = systems.AddComponent<RoomRegistry>();
            GameManager gameManager = systems.AddComponent<GameManager>();
            NightClock clock = systems.AddComponent<NightClock>();
            systems.AddComponent<PowerManager>();
            GameAudioController audioController = systems.AddComponent<GameAudioController>();
            JumpscareController jumpscare = systems.AddComponent<JumpscareController>();

            // 房间
            DecorMats decor = CreateDecorMats();
            Transform roomsParent = new GameObject("Rooms").transform;
            List<BuiltRoom> builtRooms = new List<BuiltRoom>();
            foreach (RoomSpec spec in Specs)
                builtRooms.Add(BuildRoom(spec, roomsParent, floorMat, wallMat, ceilingMat, curtainMat, decor));

            // 办公室
            OfficeRefs office = BuildOffice(floorMat, wallMat, ceilingMat, doorMat, deskMat,
                buttonRedMat, buttonPaleMat, metalMat);

            // 玩偶（初始站位：弗雷迪/邦尼/奇卡在舞台，狐狸在海盗湾帘后）
            Transform enemiesParent = new GameObject("Animatronics").transform;
            BuiltRoom showStage = builtRooms.Find(r => r.Spec.Id == RoomIds.ShowStage);
            BuiltRoom pirateCove = builtRooms.Find(r => r.Spec.Id == RoomIds.PirateCove);

            AnimatronicBody freddyBody = AnimatronicFactory.BuildBody(enemiesParent, "Freddy",
                AnimatronicFactory.Kind.Bear, new Color(0.42f, 0.27f, 0.14f), new Color(0.54f, 0.42f, 0.23f));
            AnimatronicBody bonnieBody = AnimatronicFactory.BuildBody(enemiesParent, "Bonnie",
                AnimatronicFactory.Kind.Rabbit, new Color(0.35f, 0.25f, 0.56f), new Color(0.48f, 0.36f, 0.74f));
            AnimatronicBody chicaBody = AnimatronicFactory.BuildBody(enemiesParent, "Chica",
                AnimatronicFactory.Kind.Bird, new Color(0.85f, 0.75f, 0.15f), new Color(0.82f, 0.47f, 0.09f));
            AnimatronicBody foxyBody = AnimatronicFactory.BuildBody(enemiesParent, "Foxy",
                AnimatronicFactory.Kind.Fox, new Color(0.63f, 0.19f, 0.16f), new Color(0.75f, 0.28f, 0.22f));

            PlaceAt(freddyBody, showStage.Stops[0]);
            PlaceAt(bonnieBody, showStage.Stops[1]);
            PlaceAt(chicaBody, showStage.Stops[2]);
            PlaceAt(foxyBody, pirateCove.Stops[0]);

            FreddyAI freddyAI = AnimatronicFactory.AttachAI<FreddyAI>(freddyBody.gameObject, "Freddy", freddyBody, registry);
            Wire.Bool(freddyAI, "freezeWhenWatched", true);
            Wire.Float(freddyAI, "freddyDoorAttackWindow", 5f); // 与鸡/兔一致：门口 5 秒不关门即跳杀
            Wire.Float(freddyAI, "powerOutKillMinDelay", 8f);
            Wire.Float(freddyAI, "powerOutKillMaxDelay", 20f);

            BonnieAI bonnieAI = AnimatronicFactory.AttachAI<BonnieAI>(bonnieBody.gameObject, "Bonnie", bonnieBody, registry);
            Wire.Float(bonnieAI, "doorAttackWindow", 6f);

            ChicaAI chicaAI = AnimatronicFactory.AttachAI<ChicaAI>(chicaBody.gameObject, "Chica", chicaBody, registry);
            Wire.Float(chicaAI, "doorAttackWindow", 6f);

            FoxyAI foxyAI = AnimatronicFactory.AttachAI<FoxyAI>(foxyBody.gameObject, "Foxy", foxyBody, registry);
            Wire.Ref(foxyAI, "curtainLeft", pirateCove.CurtainL);
            Wire.Ref(foxyAI, "curtainRight", pirateCove.CurtainR);
            Wire.Float(foxyAI, "sprintDuration", 6f);
            Wire.Float(foxyAI, "watchedCooldown", 4f);
            Wire.Float(foxyAI, "retreatCooldown", 8f);

            // 监控系统（先建组件与摄像机，UI 之后再接线）
            GameObject monitorSystem = new GameObject("MonitorSystem");
            MonitorController monitor = monitorSystem.AddComponent<MonitorController>();
            CameraFeedController feed = monitorSystem.AddComponent<CameraFeedController>();

            GameObject monitorCamGo = new GameObject("MonitorCamera");
            monitorCamGo.transform.SetPositionAndRotation(showStage.Anchor.position, showStage.Anchor.rotation);
            Camera monitorCam = monitorCamGo.AddComponent<Camera>();
            monitorCam.clearFlags = CameraClearFlags.SolidColor;
            monitorCam.backgroundColor = Color.black;
            monitorCam.fieldOfView = 62f;
            monitorCamGo.AddComponent<AudioListener>();

            // 随镜头移动的补光灯：镜头切到哪个房间就照亮哪间，保证监控画面始终可见
            GameObject feedLightGo = new GameObject("FeedFillLight");
            feedLightGo.transform.SetParent(monitorCamGo.transform, false);
            Light feedLight = feedLightGo.AddComponent<Light>();
            feedLight.type = LightType.Point;
            feedLight.color = new Color(0.85f, 0.9f, 1f);
            feedLight.intensity = 0.7f;
            feedLight.range = 16f;
            feedLight.shadows = LightShadows.None;

            monitorCamGo.SetActive(false);

            // 菜单展示台（主菜单半脸 + 图鉴展示玩偶，独立 RenderTexture 相机）
            MenuStageRefs menuStage = BuildMenuStage();

            // UI（地图按钮需要 feed 实例）
            List<MapEntry> mapEntries = BuildMapEntries(builtRooms);
            UIBuilderResult ui = UIBuilder.Build(feed, mapEntries, menuStage);
            Wire.Ref(ui.Hud, "nightClock", clock);

            Wire.Ref(monitor, "officeCameraObject", office.OfficeCamera.gameObject);
            Wire.Ref(monitor, "monitorCameraObject", monitorCamGo);
            Wire.Ref(monitor, "monitorRoot", ui.MonitorCanvasRoot);
            Wire.KeyCodeField(monitor, "toggleKey", KeyCode.Space);
            Wire.Float(monitor, "bottomHoverThreshold", 0.05f);
            Wire.Float(monitor, "hoverZoneWidth", 0.4f);
            Wire.Ref(monitor, "openBarGroup", ui.OpenBarGroup);
            Wire.Ref(monitor, "closeBarGroup", ui.CloseBarGroup);
            Wire.Float(monitor, "transitionDuration", 0.25f);

            Wire.Ref(feed, "roomRegistry", registry);
            Wire.Ref(feed, "feedCamera", monitorCam);
            Wire.Ref(feed, "staticOverlay", ui.StaticOverlay);
            Wire.Ref(feed, "audioOnlyNotice", ui.AudioOnlyNotice);
            Wire.Float(feed, "staticDuration", 0.35f);

            // 音频源（Audio/Recorded/ 下有用户录音的同名 wav 会自动优先于程序化合成版）
            AudioSource ambientSrc = CreateSource(systems.transform, "Src_Ambient", Clip("ambient_recorded"), true, 0.3f);
            AudioSource doorSrc = CreateSource(systems.transform, "Src_Door", Clip("door_close"), false, 0.9f);
            AudioSource lightSrc = CreateSource(systems.transform, "Src_Light", Clip("light_click"), true, 0.7f);
            AudioSource lightSpottedSrc = CreateSource(systems.transform, "Src_LightSpotted", Clip("light_spotted"), false, 0.9f);
            AudioSource monitorUpSrc = CreateSource(systems.transform, "Src_MonitorUp", Clip("monitor_up"), false, 0.6f);
            AudioSource monitorDownSrc = CreateSource(systems.transform, "Src_MonitorDown", Clip("monitor_down"), false, 0.6f);
            AudioSource camSwitchSrc = CreateSource(systems.transform, "Src_CamSwitch", Clip("cam_switch"), false, 1f);
            AudioSource stepsSrc = CreateSource(systems.transform, "Src_Steps", Clip("anim_steps"), false, 0.85f);
            AudioSource burstSrc = CreateSource(systems.transform, "Src_StaticBurst", Clip("static_burst"), false, 0.85f);
            AudioSource powerOutSrc = CreateSource(systems.transform, "Src_PowerOut", Clip("power_down"), false, 0.9f);
            AudioSource chimeSrc = CreateSource(systems.transform, "Src_Chime", Clip("chime_6am"), false, 0.8f);
            AudioSource screamSrc = CreateSource(systems.transform, "Src_Scream", Clip("scream"), false, 1f);
            AudioSource deathSrc = CreateSource(systems.transform, "Src_Death", Clip("death"), false, 0.9f);
            AudioSource foxyRunSrc = CreateSource(systems.transform, "Src_FoxyRun", Clip("foxy_run"), true, 0.9f);
            // 氛围还原音源：弗雷迪深笑 / 厨房锅碗 / 6AM 欢呼 / 狐狸砸门
            AudioSource freddyLaughSrc = CreateSource(systems.transform, "Src_FreddyLaugh", Clip("freddy_laugh"), false, 0.9f);
            AudioSource potsSrc = CreateSource(systems.transform, "Src_Pots", Clip("pots_clatter"), true, 0.6f);
            AudioSource cheerSrc = CreateSource(systems.transform, "Src_Cheer", Clip("cheer_kids"), false, 0.7f);
            AudioSource knockSrc = CreateSource(systems.transform, "Src_Knock", Clip("window_knock"), true, 0.9f);
            AudioSource foxySongSrc = CreateSource(systems.transform, "Src_FoxySong", Clip("foxy_song"), true, 0.65f);
            AudioSource powerMusicSrc = CreateSource(systems.transform, "Src_PowerMusic", Clip("power_out_music"), true, 0.85f);
            // 兔/鸡靠近办公室提示音（走廊角落/门口到达时响一声）
            AudioSource nearOfficeSrc = CreateSource(systems.transform, "Src_NearOffice", Clip("near_office"), false, 0.9f);
            // 金熊专属音效：跳杀尖叫（与常规跳杀不同）+ IT'S ME 闪图音（幻觉期间循环）
            AudioSource goldenScreamSrc = CreateSource(systems.transform, "Src_GoldenScream", Clip("golden_scream"), false, 1f);
            AudioSource itsMeSrc = CreateSource(systems.transform, "Src_ItsMe", Clip("its_me"), true, 0.8f);

            Wire.Ref(audioController, "ambientSource", ambientSrc);
            Wire.Ref(audioController, "doorSource", doorSrc);
            Wire.Ref(audioController, "lightSource", lightSrc);
            Wire.Ref(audioController, "lightSpottedSource", lightSpottedSrc);
            Wire.Ref(audioController, "monitorUpSource", monitorUpSrc);
            Wire.Ref(audioController, "monitorDownSource", monitorDownSrc);
            Wire.Ref(audioController, "stepsSource", stepsSrc);
            Wire.Ref(audioController, "powerOutSource", powerOutSrc);
            Wire.Ref(audioController, "chimeSource", chimeSrc);
            Wire.Ref(audioController, "screamSource", screamSrc);
            Wire.Ref(audioController, "freddyLaughSource", freddyLaughSrc);
            Wire.Ref(audioController, "potsSource", potsSrc);
            Wire.Ref(audioController, "cheerSource", cheerSrc);
            Wire.Ref(audioController, "knockSource", knockSrc);
            Wire.Ref(audioController, "foxySongSource", foxySongSrc);
            Wire.Ref(audioController, "powerOutMusicSource", powerMusicSrc);
            Wire.Ref(audioController, "nearOfficeSource", nearOfficeSrc);
            // 弗雷迪移动笑声二选一（用户录音：熊笑声1 / 熊笑声2）
            Wire.RefArray(audioController, "freddyLaughClips", new Object[]
            {
                Clip("freddy_laugh_1"),
                Clip("freddy_laugh_2"),
            });
            // 五夜电话语音（PhoneCallUI 按夜数播放，第 6/7/自定义夜无电话）
            Wire.RefArray(ui.Phone, "nightVoiceClips", new Object[]
            {
                Clip("phone_night1"),
                Clip("phone_night2"),
                Clip("phone_night3"),
                Clip("phone_night4"),
                Clip("phone_night5"),
            });
            Wire.Ref(ui.Viewer, "screamSource", screamSrc); // 图鉴跳杀预览复用同一条尖叫音源
            // 监控雪花突发音 + 切换摄像头音：直接挂在 CameraFeedController 上（只在"看着的房间被离开"时响）
            Wire.Ref(feed, "staticBurstSource", burstSrc);
            Wire.Ref(feed, "camSwitchSource", camSwitchSrc);
            // 跳杀结束黑幕时的死亡音：挂在 JumpscareController 上
            Wire.Ref(jumpscare, "deathSource", deathSrc);
            Wire.Ref(foxyAI, "footstepSource", foxyRunSrc);
            Wire.Ref(audioController, "officeLightObject", office.OfficeLight);

            // 管理器接线
            NightConfig[] configs = new NightConfig[6];
            for (int n = 1; n <= 6; n++)
                configs[n - 1] = AssetDatabase.LoadAssetAtPath<NightConfig>($"{ConfigDir}/Night{n}.asset");
            Wire.RefArray(gameManager, "nightConfigs", configs);

            Wire.Ref(jumpscare, "officeCamera", office.OfficeCamera);
            Wire.Ref(jumpscare, "blackOverlay", ui.BlackOverlay);
            Wire.Ref(jumpscare, "jumpscareLight", office.JumpscareLight);
            Wire.Ref(jumpscare, "screamSource", screamSrc);
            Wire.RefArray(jumpscare, "bodies", new Object[] { freddyBody, bonnieBody, chicaBody, foxyBody });
            Wire.StrArray(jumpscare, "bodyIds", new[] { "Freddy", "Bonnie", "Chica", "Foxy" });

            // ---- 金熊彩蛋（Golden Freddy，复刻 FNAF 1）----
            // 西走廊角落（CAM 2B）海报对：平常是弗雷迪海报，触发后切换成金熊脸特写
            BuiltRoom westCorner = builtRooms.Find(r => r.Spec.Id == RoomIds.WestHallCorner);
            GameObject posterNormal = BuildFreddyPoster(westCorner.Root.transform);
            GameObject posterGolden = BuildGoldenPoster(westCorner.Root.transform);
            posterGolden.SetActive(false);

            // 瘫坐金熊（初始隐藏，摆在办公室桌前空地，面向玩家）
            Transform gfHead;
            GameObject goldenBody = AnimatronicFactory.BuildGoldenFreddy(systems.transform, out gfHead);
            goldenBody.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0.45f), Quaternion.Euler(0f, 180f, 0f));
            goldenBody.SetActive(false);

            AudioSource girlLaughSrc = CreateSource(systems.transform, "Src_GirlLaugh", Clip("girl_laugh"), false, 0.8f);

            GoldenFreddyController golden = systems.AddComponent<GoldenFreddyController>();
            Wire.Ref(golden, "body", goldenBody);
            Wire.Ref(golden, "head", gfHead);
            Wire.Ref(golden, "posterNormal", posterNormal);
            Wire.Ref(golden, "posterGolden", posterGolden);
            Wire.Ref(golden, "itsMeOverlay", ui.ItsMeOverlay);
            Wire.Ref(golden, "itsMeText", ui.ItsMeText);
            Wire.RefArray(golden, "itsMeFaces", ui.ItsMeFaces);
            Wire.Ref(golden, "laughSource", girlLaughSrc);
            Wire.Ref(golden, "screamSource", goldenScreamSrc); // 金熊跳杀用专属尖叫，与常规跳杀分开
            Wire.Ref(golden, "itsMeSource", itsMeSrc);
            Wire.Ref(golden, "jumpscareLight", office.JumpscareLight);
            Wire.Ref(golden, "officeCamera", office.OfficeCamera);
            Wire.Ref(golden, "mainMenu", ui.MainMenu);
            Wire.Ref(ui.CustomNight, "goldenFreddy", golden); // 自定义夜 1/9/8/7 检测
            Wire.Float(golden, "posterChance", 0.005f);
            Wire.Float(golden, "killDelay", 5f);
            Wire.Float(golden, "faceDistance", 0.55f);
            Wire.Float(golden, "jumpscareDuration", 1.2f);

            // 房间注册表（数据在最后统一写入）
            FillRegistry(registry, builtRooms);

            // 环境氛围
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 55f;
            RenderSettings.fogColor = new Color(0.012f, 0.012f, 0.02f);
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.05f); // 近零环境光：门房残余"微亮"就是环境光均匀洒的，压掉后全黑；办公室亮度由 OfficeLight 补
            RenderSettings.skybox = null;

            GameObject moonGo = new GameObject("Moonlight");
            moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.5f, 0.56f, 0.7f);
            moon.intensity = 0.1f;
            moon.shadows = LightShadows.Hard; // 硬阴影：软阴影会从薄墙边缘渗光；配合挡光板让室内/门房全黑可控

            // 保存
            pendingVolumes = null; // 音量沿用完成，清空（下次 Build 重新从旧场景抓取）
            Directory.CreateDirectory("Assets/FNAF/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[SceneBuilder] 场景生成完成: {ScenePath}");
        }

        // ---------- 房间 ----------

        private static BuiltRoom BuildRoom(RoomSpec spec, Transform parent,
            Material floorMat, Material wallMat, Material ceilingMat, Material curtainMat, DecorMats decor)
        {
            GameObject root = new GameObject(spec.Id);
            root.transform.SetParent(parent, false);
            root.transform.position = spec.Center;

            SBox("Floor", root.transform, new Vector3(0, -0.05f, 0), new Vector3(spec.W, 0.1f, spec.D), floorMat);
            SBox("Ceiling", root.transform, new Vector3(0, 3.05f, 0), new Vector3(spec.W, 0.1f, spec.D), ceilingMat);
            SBox("WallN", root.transform, new Vector3(0, 1.5f, spec.D / 2 + 0.1f), new Vector3(spec.W, 3, 0.2f), wallMat);
            SBox("WallS", root.transform, new Vector3(0, 1.5f, -(spec.D / 2 + 0.1f)), new Vector3(spec.W, 3, 0.2f), wallMat);
            if (!spec.SkipWestWall)
                SBox("WallW", root.transform, new Vector3(-(spec.W / 2 + 0.1f), 1.5f, 0), new Vector3(0.2f, 3, spec.D), wallMat);
            if (!spec.SkipEastWall)
                SBox("WallE", root.transform, new Vector3(spec.W / 2 + 0.1f, 1.5f, 0), new Vector3(0.2f, 3, spec.D), wallMat);

            Transform curtainL = null, curtainR = null;
            if (spec.HasCurtain)
            {
                // 初始就是"关严"档（阶段 0）：两帘在中央合拢，狐狸藏在帘后看不见
                curtainL = SBox("CurtainL", root.transform, new Vector3(-1.15f, 1.5f, 0.6f), new Vector3(2.3f, 3, 0.2f), curtainMat).transform;
                curtainR = SBox("CurtainR", root.transform, new Vector3(1.15f, 1.5f, 0.6f), new Vector3(2.3f, 3, 0.2f), curtainMat).transform;
            }

            // 门外常暗（左右门房基础灯压到近零：不开门灯时就是全黑剪影，开灯才看得见）
            PointLight(root.transform, "RoomLight", new Vector3(0, 2.7f, 0),
                new Color(0.78f, 0.82f, 0.95f),
                spec.Id == RoomIds.LeftDoor || spec.Id == RoomIds.RightDoor ? 0.03f : 1.0f,
                Mathf.Sqrt(spec.W * spec.W + spec.D * spec.D) + 2f);

            DecorateRoom(spec, root.transform, decor);

            BuiltRoom room = new BuiltRoom { Spec = spec, Root = root, CurtainL = curtainL, CurtainR = curtainR };

            if (spec.ShowOnMonitor)
            {
                float zSide = spec.AnchorFaceSouth ? -1f : 1f;
                Transform anchor = new GameObject("CameraAnchor").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(spec.W / 2 - 0.7f, 2.9f, zSide * (spec.D / 2 - 0.7f));
                // 视线落在离地 1 米（玩偶躯干高度）：俯角约 21°，构图中景而非俯拍地板
                anchor.rotation = Quaternion.LookRotation(
                    (root.transform.position + Vector3.up * 1f) - anchor.position);
                room.Anchor = anchor;
            }

            switch (spec.Id)
            {
                case RoomIds.ShowStage:
                    room.Stops.Add(CreateStop(root.transform, new Vector3(-1.2f, 0, 1.0f), 180f));
                    room.Stops.Add(CreateStop(root.transform, new Vector3(0f, 0, 1.0f), 180f));
                    room.Stops.Add(CreateStop(root.transform, new Vector3(1.2f, 0, 1.0f), 180f));
                    break;
                case RoomIds.PirateCove:
                    room.Stops.Add(CreateStop(root.transform, new Vector3(0, 0, 1.8f), 180f)); // 帘后隐藏（关严时完全看不见）
                    room.Stops.Add(CreateStop(root.transform, new Vector3(0, 0, 1.1f), 180f)); // 微开帘缝后探头
                    room.Stops.Add(CreateStop(root.transform, new Vector3(0, 0, -0.3f), 180f)); // 帘全开，门口将出未出
                    break;
                case RoomIds.LeftDoor:
                    room.Stops.Add(CreateStop(root.transform, Vector3.zero, 90f)); // 面朝办公室（门口）
                    room.Stops.Add(CreateStop(root.transform, new Vector3(1.0f, 0f, 1.55f), 90f)); // 左窗贴脸位（世界 x-3.75，脸距办公室外墙 0.2m）
                    break;
                case RoomIds.RightDoor:
                    room.Stops.Add(CreateStop(root.transform, Vector3.zero, -90f));
                    room.Stops.Add(CreateStop(root.transform, new Vector3(-1.0f, 0f, 1.55f), -90f)); // 右窗贴脸位（世界 x+3.75）
                    break;
                default:
                    room.Stops.Add(CreateStop(root.transform, Vector3.zero, 180f));
                    break;
            }

            return room;
        }

        private static void FillRegistry(RoomRegistry registry, List<BuiltRoom> builtRooms)
        {
            SerializedObject so = new SerializedObject(registry);
            SerializedProperty rooms = so.FindProperty("rooms");
            rooms.arraySize = builtRooms.Count;

            for (int i = 0; i < builtRooms.Count; i++)
            {
                BuiltRoom room = builtRooms[i];
                SerializedProperty el = rooms.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("RoomId").stringValue = room.Spec.Id;
                el.FindPropertyRelative("DisplayName").stringValue = room.Spec.Name;
                el.FindPropertyRelative("CameraAnchor").objectReferenceValue = room.Anchor;
                el.FindPropertyRelative("AudioOnly").boolValue = room.Spec.AudioOnly;
                el.FindPropertyRelative("CameraPans").boolValue = room.Spec.CameraPans;
                el.FindPropertyRelative("CameraPanMin").floatValue = room.Spec.CameraPanMin;
                el.FindPropertyRelative("CameraPanMax").floatValue = room.Spec.CameraPanMax;
                el.FindPropertyRelative("ShowOnMonitor").boolValue = room.Spec.ShowOnMonitor;

                SerializedProperty stops = el.FindPropertyRelative("EnemyStops");
                stops.arraySize = room.Stops.Count;
                for (int s = 0; s < room.Stops.Count; s++)
                    stops.GetArrayElementAtIndex(s).objectReferenceValue = room.Stops[s];

                SerializedProperty adj = el.FindPropertyRelative("AdjacentRoomIds");
                adj.arraySize = room.Spec.Adjacent.Length;
                for (int a = 0; a < room.Spec.Adjacent.Length; a++)
                    adj.GetArrayElementAtIndex(a).stringValue = room.Spec.Adjacent[a];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<MapEntry> BuildMapEntries(List<BuiltRoom> builtRooms)
        {
            List<MapEntry> entries = new List<MapEntry>();
            foreach (BuiltRoom room in builtRooms)
            {
                if (!room.Spec.ShowOnMonitor) continue;
                entries.Add(new MapEntry
                {
                    Id = room.Spec.Id,
                    CamLabel = room.Spec.AudioOnly ? room.Spec.CamLabel + " ♪" : room.Spec.CamLabel,
                    MapCenter = new Vector2(room.Spec.Center.x, room.Spec.Center.z),
                    MapSize = new Vector2(room.Spec.W, room.Spec.D)
                });
            }
            return entries;
        }

        // ---------- 办公室 ----------

        private static OfficeRefs BuildOffice(Material floorMat, Material wallMat, Material ceilingMat,
            Material doorMat, Material deskMat, Material buttonRedMat, Material buttonPaleMat, Material metalMat)
        {
            GameObject root = new GameObject("Office");
            OfficeRefs refs = new OfficeRefs();

            SBox("Floor", root.transform, new Vector3(0, -0.05f, 0), new Vector3(6.4f, 0.1f, 4.4f), floorMat);
            SBox("Ceiling", root.transform, new Vector3(0, 3.05f, 0), new Vector3(6.4f, 0.1f, 4.4f), ceilingMat);
            SBox("WallN", root.transform, new Vector3(0, 1.5f, 2.2f), new Vector3(6.4f, 3, 0.2f), wallMat);
            SBox("WallS", root.transform, new Vector3(0, 1.5f, -2.2f), new Vector3(6.4f, 3, 0.2f), wallMat);
            foreach (float sx in new[] { -1f, 1f })
            {
                string tag = sx < 0 ? "W" : "E";
                // North 段拆成上下+两侧四块，中间留出窗洞（窗在门旁，原版布局）
                SBox($"Wall{tag}_NT", root.transform, new Vector3(sx * 3.2f, 2.7f, 1.55f), new Vector3(0.2f, 0.6f, 1.3f), wallMat);
                SBox($"Wall{tag}_NB", root.transform, new Vector3(sx * 3.2f, 0.5f, 1.55f), new Vector3(0.2f, 1.0f, 1.3f), wallMat);
                SBox($"Wall{tag}_NS", root.transform, new Vector3(sx * 3.2f, 1.5f, 0.975f), new Vector3(0.2f, 3f, 0.15f), wallMat);
                SBox($"Wall{tag}_NN", root.transform, new Vector3(sx * 3.2f, 1.5f, 2.125f), new Vector3(0.2f, 3f, 0.15f), wallMat);
                SBox($"Wall{tag}_S", root.transform, new Vector3(sx * 3.2f, 1.5f, -1.55f), new Vector3(0.2f, 3, 1.3f), wallMat);
                SBox($"Wall{tag}_Top", root.transform, new Vector3(sx * 3.2f, 2.7f, 0), new Vector3(0.2f, 0.6f, 1.8f), wallMat);

                // 窗：金属框分两格（窗外就是门外房间实体——门房深度已扩到 z±2 与窗洞连通，贴窗玩偶站在窗外 0.2m 处）
                SBox($"WinFrame{tag}_Top", root.transform, new Vector3(sx * 3.2f, 2.43f, 1.55f), new Vector3(0.24f, 0.08f, 1.12f), metalMat);
                SBox($"WinFrame{tag}_Bot", root.transform, new Vector3(sx * 3.2f, 0.97f, 1.55f), new Vector3(0.24f, 0.08f, 1.12f), metalMat);
                SBox($"WinFrame{tag}_L", root.transform, new Vector3(sx * 3.2f, 1.7f, 1.02f), new Vector3(0.24f, 1.5f, 0.08f), metalMat);
                SBox($"WinFrame{tag}_R", root.transform, new Vector3(sx * 3.2f, 1.7f, 2.08f), new Vector3(0.24f, 1.5f, 0.08f), metalMat);
                SBox($"WinFrame{tag}_Mid", root.transform, new Vector3(sx * 3.2f, 1.7f, 1.55f), new Vector3(0.18f, 1.4f, 0.06f), metalMat);
                Light winLight = PointLight(root.transform, $"WindowLight{(sx < 0 ? "W" : "E")}", new Vector3(sx * 3.38f, 1.7f, 1.55f),
                    new Color(0.55f, 0.65f, 0.8f), 0.06f, 2.5f);
                WindowLightLink winLink = winLight.gameObject.AddComponent<WindowLightLink>();
                Wire.Ref(winLink, "windowLight", winLight);
                Wire.Int(winLink, "side", sx < 0 ? 0 : 1); // DoorSide：0=Left 1=Right
            }

            // 光密封挡板（MeshRenderer.shadowCastingMode = ShadowsOnly：对相机完全隐藏、阴影照投）
            // ——封死办公室→门房的一切开口（窗洞+门洞），门开着也不透光，门房内彻底全黑，
            // 只有门灯/窗灯亮起才能看见门外（原版核心体验）。
            // 注意不能用 Fade alpha0 材质：Standard 着色器 Fade 模式的阴影是抖动渐隐，alpha0=无阴影=挡不住光
            foreach (float sx in new[] { -1f, 1f })
            {
                string sealTag = sx < 0 ? "W" : "E";
                GameObject sealWin = SBox($"LightSeal{sealTag}_Win", root.transform, new Vector3(sx * 3.2f, 1.7f, 1.55f), new Vector3(0.02f, 1.5f, 1.24f), wallMat);
                GameObject sealDoor = SBox($"LightSeal{sealTag}_Door", root.transform, new Vector3(sx * 3.2f, 1.25f, 0f), new Vector3(0.02f, 2.7f, 1.9f), wallMat);
                sealWin.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                sealDoor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            // 桌子
            SBox("DeskTop", root.transform, new Vector3(0, 0.95f, 1.35f), new Vector3(3.2f, 0.12f, 0.9f), deskMat);
            SBox("DeskL", root.transform, new Vector3(-1.45f, 0.45f, 1.35f), new Vector3(0.15f, 0.9f, 0.8f), deskMat);
            SBox("DeskR", root.transform, new Vector3(1.45f, 0.45f, 1.35f), new Vector3(0.15f, 0.9f, 0.8f), deskMat);

            // 风扇
            GameObject fan = new GameObject("Fan");
            fan.transform.SetParent(root.transform, false);
            fan.transform.localPosition = new Vector3(0.9f, 1.01f, 1.35f);
            Cylinder("FanBase", fan.transform, new Vector3(0, 0.02f, 0), new Vector3(0.34f, 0.03f, 0.34f), metalMat);
            Cylinder("FanPole", fan.transform, new Vector3(0, 0.16f, 0), new Vector3(0.05f, 0.16f, 0.05f), metalMat);
            GameObject blades = new GameObject("Blades");
            blades.transform.SetParent(fan.transform, false);
            blades.transform.localPosition = new Vector3(0, 0.33f, 0);
            SBox("Blade1", blades.transform, Vector3.zero, new Vector3(0.9f, 0.02f, 0.12f), metalMat);
            SBox("Blade2", blades.transform, Vector3.zero, new Vector3(0.12f, 0.02f, 0.9f), metalMat);
            FanSpinner spinner = fan.AddComponent<FanSpinner>();
            Wire.Ref(spinner, "fanBlades", blades.transform);
            Wire.Float(spinner, "spinSpeed", 720f);

            // 相机 rig（固定在南端，面朝桌子；鼠标移屏幕边缘左右旋转张望）
            GameObject rig = new GameObject("OfficeCameraRig");
            rig.transform.SetParent(root.transform, false);
            rig.transform.localPosition = new Vector3(0, 1.65f, -2f);
            GameObject camGo = new GameObject("OfficeCamera");
            camGo.transform.SetParent(rig.transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.035f);
            cam.fieldOfView = 60f; // 用户定稿：60（±30 平移）
            // 近裁剪面收窄：跳杀时玩偶吻部尖距镜头仅约 0.25m，默认 0.3 会把脸切穿
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            refs.OfficeCamera = cam;

            OfficeViewController viewController = rig.AddComponent<OfficeViewController>();
            Wire.Ref(viewController, "cameraRig", rig.transform);
            Wire.Float(viewController, "minPanAngle", -33f);
            Wire.Float(viewController, "maxPanAngle", 33f);
            Wire.Float(viewController, "panSpeed", 280f); // 用户体感调低：原 500，玩家自调 360 后再降
            Wire.Float(viewController, "edgeThreshold", 0.4f);

            Light officeLight = PointLight(root.transform, "OfficeLight", new Vector3(0, 2.85f, 0.3f),
                new Color(1f, 0.85f, 0.6f), 0.7f, 10f);
            // 硬阴影：软阴影会从 0.2m 薄墙边缘渗光；配合隐形挡光板，门外彻底全黑
            officeLight.shadows = LightShadows.Hard;
            refs.OfficeLight = officeLight.gameObject;

            // 跳杀专用灯（平时关闭）：怼脸时玩偶脸朝 -z 的相机，而 OfficeLight 在脸背面，
            // 无此灯去掉白闪后跳杀就是一张纯黑的脸。摆在相机侧上方，参数与图鉴展示灯一致
            Light jumpscareLight = PointLight(root.transform, "JumpscareLight", new Vector3(0f, 2.2f, -1.3f),
                new Color(0.92f, 0.94f, 1f), 1.4f, 5f);
            jumpscareLight.gameObject.SetActive(false);
            refs.JumpscareLight = jumpscareLight.gameObject;

            refs.LeftDoor = BuildDoor(root.transform, "Left", new Vector3(-3.15f, 0, 0), new Vector3(-4.1f, 2.4f, 0), 0, doorMat);
            refs.RightDoor = BuildDoor(root.transform, "Right", new Vector3(3.15f, 0, 0), new Vector3(4.1f, 2.4f, 0), 1, doorMat);

            BuildWallButtons(root.transform, refs.LeftDoor, -1, buttonRedMat, buttonPaleMat);
            BuildWallButtons(root.transform, refs.RightDoor, 1, buttonRedMat, buttonPaleMat);

            // ---- 氛围精细化：桌面杂物 / 垃圾桶 / 吊灯罩 / 门框条 / 踢脚线 / 北墙海报 ----
            Material paperMat = AnimatronicFactory.StandardMat(new Color(0.92f, 0.92f, 0.87f), 0.1f);

            // 桌面散落的文件、马克杯、笔（夜班保安的工位）
            string[] docNames = { "PaperA", "PaperB", "PaperC" };
            Vector3[] docPos = { new Vector3(-1.05f, 1.02f, 1.15f), new Vector3(-0.72f, 1.02f, 1.42f), new Vector3(1.62f, 1.02f, 1.2f) };
            float[] docYaw = { 14f, -9f, 26f };
            for (int i = 0; i < 3; i++)
            {
                GameObject doc = SBox(docNames[i], root.transform, docPos[i], new Vector3(0.36f, 0.012f, 0.5f), paperMat);
                doc.transform.localRotation = Quaternion.Euler(0f, docYaw[i], 0f);
            }
            Cylinder("Mug", root.transform, new Vector3(-1.42f, 1.08f, 1.32f), new Vector3(0.12f, 0.14f, 0.12f), metalMat);
            SBox("MugHandle", root.transform, new Vector3(-1.35f, 1.09f, 1.32f), new Vector3(0.05f, 0.09f, 0.02f), metalMat);
            GameObject pen1 = SBox("Pen1", root.transform, new Vector3(-0.85f, 1.022f, 1.3f), new Vector3(0.025f, 0.012f, 0.22f), buttonRedMat);
            pen1.transform.localRotation = Quaternion.Euler(0f, 38f, 0f);
            GameObject pen2 = SBox("Pen2", root.transform, new Vector3(1.45f, 1.022f, 1.45f), new Vector3(0.025f, 0.012f, 0.2f), AnimatronicFactory.StandardMat(new Color(0.05f, 0.05f, 0.05f), 0.4f));
            pen2.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);

            // 桌下垃圾桶
            Cylinder("OfficeTrash", root.transform, new Vector3(-1.85f, 0.19f, 1.55f), new Vector3(0.36f, 0.38f, 0.36f), metalMat);

            // 吸顶灯：垂线 + 灯罩包住 OfficeLight 灯点
            SBox("LampWire", root.transform, new Vector3(0, 2.96f, 0.3f), new Vector3(0.02f, 0.22f, 0.02f), metalMat);
            Cylinder("LampShade", root.transform, new Vector3(0, 2.79f, 0.3f), new Vector3(0.5f, 0.1f, 0.5f), metalMat);

            // 门框装饰条（门洞两侧 + 上沿）
            foreach (float sx in new[] { -1f, 1f })
            {
                string tag = sx < 0 ? "W" : "E";
                SBox($"Frame{tag}_N", root.transform, new Vector3(sx * 3.08f, 1.2f, 0.94f), new Vector3(0.06f, 2.4f, 0.08f), metalMat);
                SBox($"Frame{tag}_S", root.transform, new Vector3(sx * 3.08f, 1.2f, -0.94f), new Vector3(0.06f, 2.4f, 0.08f), metalMat);
                SBox($"Frame{tag}_Top", root.transform, new Vector3(sx * 3.08f, 2.44f, 0), new Vector3(0.06f, 0.08f, 1.96f), metalMat);
            }

            // 踢脚线：南北整条，东西因门洞分两段
            SBox("BaseboardN", root.transform, new Vector3(0, 0.06f, 2.1f), new Vector3(6.3f, 0.12f, 0.05f), metalMat);
            SBox("BaseboardS", root.transform, new Vector3(0, 0.06f, -2.1f), new Vector3(6.3f, 0.12f, 0.05f), metalMat);
            foreach (float sz in new[] { -1f, 1f })
            {
                SBox($"BaseboardW{sz}", root.transform, new Vector3(-3.1f, 0.06f, sz * 1.55f), new Vector3(0.05f, 0.12f, 1.2f), metalMat);
                SBox($"BaseboardE{sz}", root.transform, new Vector3(3.1f, 0.06f, sz * 1.55f), new Vector3(0.05f, 0.12f, 1.2f), metalMat);
            }

            // 北墙海报（员工须知风格，画面朝南）
            Poster("OfficePoster", root.transform, new Vector3(2.2f, 1.6f, 2.06f), 180f, paperMat, metalMat);

            // ---- 点鼻子彩蛋：北墙三人海报（熊/兔/鸡），点击弗雷迪鼻子播欢快叮当 ----
            GameObject celebNose = BuildCelebratePoster(root.transform, new Vector3(-2.2f, 1.6f, 2.06f));
            AudioSource celebSrc = CreateSource(root.transform, "Src_Celebrate", Clip("honk"), false, 1f);
            CelebrateNose noseEgg = celebNose.AddComponent<CelebrateNose>();
            Wire.Ref(noseEgg, "source", celebSrc);
            // 双音源叠加暂且停用；要恢复：加回 Src_Celebrate2 的 CreateSource + Wire.Ref(noseEgg,"source2",...)，
            // CelebrateNose.source2 字段已保留且自带空值保护，直接接线即可生效

            return refs;
        }

        private static DoorController BuildDoor(Transform parent, string id, Vector3 rootPos,
            Vector3 lightPos, int side, Material doorMat)
        {
            GameObject doorRoot = new GameObject($"Door_{id}");
            doorRoot.transform.SetParent(parent, false);
            doorRoot.transform.localPosition = rootPos;

            GameObject panel = SBox("Panel", doorRoot.transform, new Vector3(0, 1.2f, 0), new Vector3(0.12f, 2.4f, 1.76f), doorMat);
            DoorController door = doorRoot.AddComponent<DoorController>();
            Wire.Int(door, "side", side);
            Wire.Ref(door, "doorPanel", panel.transform);
            Wire.Float(door, "openOffsetY", 2.6f);
            Wire.Float(door, "moveSpeed", 7f);

            // 门灯（点亮门外隔间，照出袭击者）
            GameObject lightGo = new GameObject($"DoorLight_{id}");
            lightGo.transform.SetParent(doorRoot.transform, false);
            lightGo.transform.localPosition = lightPos - rootPos;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.65f);
            light.intensity = 1.3f; // 门房基暗后门灯微补亮，保证开灯时照得清玩偶
            light.range = 6.5f;
            light.shadows = LightShadows.None;
            // 不放灯泡可视化网格（开灯时视野里会出现一颗发光小球），只保留灯光本身
            lightGo.SetActive(false);
            Wire.Ref(door, "doorLightObject", lightGo);

            return door;
        }

        private static void BuildWallButtons(Transform officeRoot, DoorController door, int sideSign,
            Material buttonRedMat, Material buttonPaleMat)
        {
            float x = sideSign * 3.04f;
            string prefix = door.name;

            // 按钮在门洞另一侧（南侧墙段，原版布局：门旁按钮，玩家转头/推屏幕边缘可见）
            GameObject doorBtn = CBox($"Btn_{prefix}_Door", officeRoot,
                new Vector3(x, 1.45f, -1.1f), new Vector3(0.1f, 0.34f, 0.34f), buttonRedMat);
            OfficeButton doorAction = doorBtn.AddComponent<OfficeButton>();
            Wire.Ref(doorAction, "door", door);
            Wire.Int(doorAction, "action", 0);

            GameObject lightBtn = CBox($"Btn_{prefix}_Light", officeRoot,
                new Vector3(x, 1.05f, -1.1f), new Vector3(0.1f, 0.34f, 0.34f), buttonPaleMat);
            OfficeButton lightAction = lightBtn.AddComponent<OfficeButton>();
            Wire.Ref(lightAction, "door", door);
            Wire.Int(lightAction, "action", 1);
        }

        // ---------- 菜单展示台（玩偶图鉴） ----------

        /// <summary>构建远离主场景（300 米外）的展示区：四只图鉴展示玩偶（ViewerCamera 拍转台），
        /// 相机只渲染到 RenderTexture，far 裁剪面收窄，不会把主场景拍进画面，
        /// 也不受主场景雾影响（雾 55m 外全黑但展示物都在 4m 内）。</summary>
        private static MenuStageRefs BuildMenuStage()
        {
            MenuStageRefs refs = new MenuStageRefs();
            refs.ViewerRT = EnsureRenderTexture("ViewerRT", 768, 768);

            GameObject root = new GameObject("MenuStage");
            root.transform.position = new Vector3(300f, 0f, 300f);

            // 图鉴区：四只玩偶叠放在同一点位（同一时间只显示一只），脚下配台座
            Transform bodiesRoot = new GameObject("ViewBodies").transform;
            bodiesRoot.SetParent(root.transform, false);
            bodiesRoot.localPosition = Vector3.zero;

            Cylinder("Pedestal", bodiesRoot, new Vector3(0f, -0.06f, 0f),
                new Vector3(2.6f, 0.06f, 2.6f),
                AnimatronicFactory.StandardMat(new Color(0.16f, 0.16f, 0.19f), 0.3f));

            refs.ViewBodies[0] = AnimatronicFactory.BuildBody(bodiesRoot, "View_Freddy",
                AnimatronicFactory.Kind.Bear, new Color(0.42f, 0.27f, 0.14f), new Color(0.54f, 0.42f, 0.23f)).gameObject;
            refs.ViewBodies[1] = AnimatronicFactory.BuildBody(bodiesRoot, "View_Bonnie",
                AnimatronicFactory.Kind.Rabbit, new Color(0.35f, 0.25f, 0.56f), new Color(0.48f, 0.36f, 0.74f)).gameObject;
            refs.ViewBodies[2] = AnimatronicFactory.BuildBody(bodiesRoot, "View_Chica",
                AnimatronicFactory.Kind.Bird, new Color(0.85f, 0.75f, 0.15f), new Color(0.82f, 0.47f, 0.09f)).gameObject;
            refs.ViewBodies[3] = AnimatronicFactory.BuildBody(bodiesRoot, "View_Foxy",
                AnimatronicFactory.Kind.Fox, new Color(0.63f, 0.19f, 0.16f), new Color(0.75f, 0.28f, 0.22f)).gameObject;

            GameObject viewerCamGo = new GameObject("ViewerCamera");
            viewerCamGo.transform.SetParent(root.transform, false);
            viewerCamGo.transform.localPosition = new Vector3(0f, 1.45f, 3.4f);
            viewerCamGo.transform.rotation = Quaternion.LookRotation(
                root.transform.TransformPoint(0f, 1.05f, 0f) - viewerCamGo.transform.position, Vector3.up);
            Camera viewerCam = viewerCamGo.AddComponent<Camera>();
            viewerCam.clearFlags = CameraClearFlags.SolidColor;
            viewerCam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            viewerCam.fieldOfView = 50f;
            // 近裁剪面收窄：跳杀预览时吻部尖端距镜头仅约 0.2m，0.25 的近裁剪会把脸切穿
            viewerCam.nearClipPlane = 0.05f;
            viewerCam.farClipPlane = 5f;
            viewerCam.targetTexture = refs.ViewerRT;
            refs.ViewerCamera = viewerCamGo.transform;

            // 灯必须放在相机一侧（z=3.3）：展示台玩偶和跳杀预览的脸都朝 +z，
            // 灯在脸前方才能正面打光——旧位置 z=1.5 会在跳杀时变成背光，怼满屏幕的脸渲染成一片纯黑
            PointLight(root.transform, "ViewerLight", new Vector3(0f, 2f, 3.3f),
                new Color(0.92f, 0.94f, 1f), 1.4f, 9f);

            return refs;
        }

        private static RenderTexture EnsureRenderTexture(string name, int width, int height)
        {
            string path = $"Assets/FNAF/RenderTextures/{name}.renderTexture";
            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory("Assets/FNAF/RenderTextures");
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            AssetDatabase.CreateAsset(rt, path);
            AssetDatabase.SaveAssets();
            return rt;
        }

        // ---------- 房间布景（每个房间的特色道具，全部避开玩偶站位与监控机位视线） ----------

        private static DecorMats CreateDecorMats()
        {
            return new DecorMats
            {
                PosterCream = AnimatronicFactory.StandardMat(new Color(0.80f, 0.74f, 0.56f), 0.1f),
                PosterBlue = AnimatronicFactory.StandardMat(new Color(0.24f, 0.36f, 0.52f), 0.1f),
                PosterGreen = AnimatronicFactory.StandardMat(new Color(0.26f, 0.46f, 0.30f), 0.1f),
                Frame = AnimatronicFactory.StandardMat(new Color(0.12f, 0.11f, 0.10f), 0.2f),
                BuntingA = AnimatronicFactory.StandardMat(new Color(0.68f, 0.20f, 0.20f), 0.2f),
                BuntingB = AnimatronicFactory.StandardMat(new Color(0.18f, 0.52f, 0.52f), 0.2f),
                BuntingC = AnimatronicFactory.StandardMat(new Color(0.82f, 0.70f, 0.22f), 0.2f),
                Wood = AnimatronicFactory.StandardMat(new Color(0.34f, 0.24f, 0.15f), 0.15f),
                Crate = AnimatronicFactory.StandardMat(new Color(0.55f, 0.43f, 0.30f), 0.1f),
                Gold = AnimatronicFactory.StandardMat(new Color(0.85f, 0.68f, 0.20f), 0.75f),
                Bone = AnimatronicFactory.StandardMat(new Color(0.88f, 0.86f, 0.80f), 0.2f),
                DarkGray = AnimatronicFactory.StandardMat(new Color(0.18f, 0.18f, 0.20f), 0.4f),
                Pink = AnimatronicFactory.StandardMat(new Color(0.90f, 0.45f, 0.60f), 0.3f),
                Porcelain = AnimatronicFactory.StandardMat(new Color(0.88f, 0.91f, 0.92f), 0.55f),
                Mirror = AnimatronicFactory.StandardMat(new Color(0.62f, 0.76f, 0.86f), 0.95f),
                StagePurple = AnimatronicFactory.StandardMat(new Color(0.26f, 0.16f, 0.38f), 0.25f),
                StarYellow = AnimatronicFactory.StandardMat(new Color(0.95f, 0.85f, 0.30f), 0.3f),
                Bristle = AnimatronicFactory.StandardMat(new Color(0.76f, 0.66f, 0.46f), 0.1f),
                Cloth = AnimatronicFactory.StandardMat(new Color(0.86f, 0.86f, 0.90f), 0.25f),
            };
        }

        private static void DecorateRoom(RoomSpec spec, Transform root, DecorMats decor)
        {
            switch (spec.Id)
            {
                case RoomIds.ShowStage: DecorShowStage(root, decor); break;
                case RoomIds.DiningArea: DecorDining(root, decor); break;
                case RoomIds.PirateCove: DecorPirateCove(root, decor); break;
                case RoomIds.WestHall: DecorHall(root, decor, -1f); break;
                case RoomIds.EastHall: DecorHall(root, decor, 1f); break;
                case RoomIds.WestHallCorner: DecorCorner(root, decor, false); break;
                case RoomIds.EastHallCorner: DecorCorner(root, decor, true); break;
                case RoomIds.SupplyCloset: DecorSupplyCloset(root, decor); break;
                case RoomIds.Backstage: DecorBackstage(root, decor); break;
                case RoomIds.Kitchen: DecorKitchen(root, decor); break;
                case RoomIds.Restrooms: DecorRestrooms(root, decor); break;
                case RoomIds.LeftDoor: DecorDoorVestibule(root, decor, -1f); break;
                case RoomIds.RightDoor: DecorDoorVestibule(root, decor, 1f); break;
            }
        }

        private static void DecorShowStage(Transform root, DecorMats decor)
        {
            // 北墙背景幕：紫黑相间竖条 + 星星
            for (int i = 0; i < 8; i++)
                SBox($"Backdrop{i}", root, new Vector3(-3.5f + i, 1.5f, 2.95f), new Vector3(0.92f, 3f, 0.05f),
                    i % 2 == 0 ? decor.StagePurple : decor.DarkGray);
            for (int i = 0; i < 5; i++)
            {
                GameObject star = SBox($"Star{i}", root, new Vector3(-3.2f + i * 1.6f, i % 2 == 0 ? 1.7f : 2.1f, 2.9f),
                    new Vector3(0.34f, 0.34f, 0.03f), decor.StarYellow);
                star.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 舞台前沿台裙（玩偶站位在 z=1，台裙在其身后不遮挡）
            SBox("StageSkirt", root, new Vector3(0, 0.2f, 2.25f), new Vector3(9.2f, 0.4f, 1.1f), decor.Wood);
            SBox("StageTrim", root, new Vector3(0, 0.38f, 1.72f), new Vector3(9.2f, 0.07f, 0.04f), decor.StarYellow);

            // 三盏彩色舞台聚光
            Color[] spotColors = { new Color(1f, 0.3f, 0.3f), new Color(0.45f, 0.35f, 1f), new Color(0.3f, 0.9f, 0.8f) };
            for (int i = 0; i < 3; i++)
            {
                float x = -2.4f + i * 2.4f;
                Cylinder($"SpotFixture{i}", root, new Vector3(x, 2.82f, 1.6f), new Vector3(0.2f, 0.09f, 0.2f), decor.DarkGray);
                PointLight(root, $"SpotLight{i}", new Vector3(x, 2.5f, 1.6f), spotColors[i], 0.55f, 6f);
            }

            // 幕布顶金色横杆 + 两根立柱
            SBox("Rod", root, new Vector3(0, 3.02f, 2.95f), new Vector3(9.4f, 0.07f, 0.07f), decor.Gold);
            Cylinder("RodPoleL", root, new Vector3(-4.7f, 1.55f, 2.95f), new Vector3(0.1f, 3.1f, 0.1f), decor.Gold);
            Cylinder("RodPoleR", root, new Vector3(4.7f, 1.55f, 2.95f), new Vector3(0.1f, 3.1f, 0.1f), decor.Gold);
            // 台前两侧台阶
            SBox("StepL", root, new Vector3(-4.4f, 0.09f, 1.9f), new Vector3(0.5f, 0.18f, 0.9f), decor.Wood);
            SBox("StepR", root, new Vector3(4.4f, 0.09f, 1.9f), new Vector3(0.5f, 0.18f, 0.9f), decor.Wood);
        }

        private static void DecorDining(Transform root, DecorMats decor)
        {
            Vector3[] tables =
            {
                new Vector3(-3.2f, 0, -1.8f), new Vector3(3.2f, 0, -1.8f),
                new Vector3(-3.2f, 0, 1.8f), new Vector3(3.2f, 0, 1.8f)
            };
            for (int t = 0; t < tables.Length; t++)
            {
                Vector3 tp = tables[t];
                GameObject table = new GameObject($"Table{t}");
                table.transform.SetParent(root, false);
                table.transform.localPosition = tp;
                Cylinder("Top", table.transform, new Vector3(0, 0.78f, 0), new Vector3(1.8f, 0.04f, 1.8f), decor.Cloth);
                Cylinder("Leg", table.transform, new Vector3(0, 0.38f, 0), new Vector3(0.14f, 0.38f, 0.14f), decor.DarkGray);
                Cylinder("Foot", table.transform, new Vector3(0, 0.02f, 0), new Vector3(0.55f, 0.02f, 0.55f), decor.DarkGray);
                // 桌心双层蛋糕 + 餐盘杯子
                Cylinder("CakeBase", table.transform, new Vector3(0, 0.87f, 0), new Vector3(0.34f, 0.045f, 0.34f), decor.BuntingC);
                Cylinder("CakeTop", table.transform, new Vector3(0, 0.95f, 0), new Vector3(0.2f, 0.045f, 0.2f), decor.Pink);
                for (int p = 0; p < 3; p++)
                {
                    float a = p * 120f * Mathf.Deg2Rad + 0.5f;
                    Vector3 pp = new Vector3(Mathf.Cos(a) * 0.55f, 0, Mathf.Sin(a) * 0.55f);
                    Cylinder($"Plate{p}", table.transform, pp + new Vector3(0, 0.825f, 0), new Vector3(0.42f, 0.012f, 0.42f), decor.Porcelain);
                    if (p < 2)
                        Cylinder($"Cup{p}", table.transform, pp + new Vector3(0.12f, 0.87f, 0.08f), new Vector3(0.1f, 0.05f, 0.1f), decor.BuntingA);
                }
                for (int c = 0; c < 3; c++)
                {
                    float a = (90f + c * 120f) * Mathf.Deg2Rad;
                    Vector3 cp = new Vector3(Mathf.Cos(a) * 1.2f, 0, Mathf.Sin(a) * 1.2f);
                    float yaw = Mathf.Atan2(-cp.x, -cp.z) * Mathf.Rad2Deg;
                    BuildChair($"Chair{t}_{c}", root, tp + cp, yaw, decor.Wood);
                }
            }

            // 北墙彩旗 + 标语
            Material[] flagMats = { decor.BuntingA, decor.BuntingB, decor.BuntingC };
            for (int i = 0; i < 9; i++)
            {
                GameObject flag = SBox($"Flag{i}", root, new Vector3(-5.6f + i * 1.4f, 2.45f, 3.95f),
                    new Vector3(0.34f, 0.34f, 0.03f), flagMats[i % 3]);
                flag.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 每桌一只飘着的气球（系在桌角，绳垂到椅背高度）
            for (int t = 0; t < tables.Length; t++)
            {
                Vector3 bp = tables[t] + new Vector3(0.75f, 0, 0.75f);
                GameObject balloon = new GameObject($"Balloon{t}");
                balloon.transform.SetParent(root, false);
                balloon.transform.localPosition = bp + new Vector3(0, 1.65f, 0);
                Sphere("Skin", balloon.transform, Vector3.zero, 0.17f, flagMats[t % 3]);
                SBox("Knot", balloon.transform, new Vector3(0, -0.19f, 0), new Vector3(0.05f, 0.04f, 0.05f), flagMats[t % 3]);
                SBox("String", root, bp + new Vector3(0, 0.7f, 0), new Vector3(0.012f, 1.4f, 0.012f), decor.DarkGray);
            }
        }

        private static void DecorPirateCove(Transform root, DecorMats decor)
        {
            // 西南角：半开宝箱 + 金币
            GameObject chest = new GameObject("Chest");
            chest.transform.SetParent(root, false);
            chest.transform.localPosition = new Vector3(-2.1f, 0, -2.0f);
            SBox("Base", chest.transform, new Vector3(0, 0.2f, 0), new Vector3(0.85f, 0.4f, 0.52f), decor.Wood);
            GameObject lid = SBox("Lid", chest.transform, new Vector3(0, 0.5f, -0.1f), new Vector3(0.85f, 0.14f, 0.52f), decor.Wood);
            lid.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            Sphere("Gold0", chest.transform, new Vector3(-0.18f, 0.46f, 0.08f), 0.07f, decor.Gold);
            Sphere("Gold1", chest.transform, new Vector3(0.02f, 0.48f, 0.1f), 0.07f, decor.Gold);
            Sphere("Gold2", chest.transform, new Vector3(0.2f, 0.44f, 0.02f), 0.07f, decor.Gold);

            // 东侧两只木桶（一只顶骷髅）
            for (int i = 0; i < 2; i++)
            {
                GameObject barrel = new GameObject($"Barrel{i}");
                barrel.transform.SetParent(root, false);
                barrel.transform.localPosition = new Vector3(2.05f - i * 0.55f, 0, -1.8f + i * 0.5f);
                Cylinder("Body", barrel.transform, new Vector3(0, 0.45f, 0), new Vector3(0.52f, 0.45f, 0.52f), decor.Wood);
                Cylinder("RingL", barrel.transform, new Vector3(0, 0.14f, 0), new Vector3(0.56f, 0.02f, 0.56f), decor.DarkGray);
                Cylinder("RingH", barrel.transform, new Vector3(0, 0.76f, 0), new Vector3(0.56f, 0.02f, 0.56f), decor.DarkGray);
            }
            GameObject skull = new GameObject("Skull");
            skull.transform.SetParent(root, false);
            skull.transform.localPosition = new Vector3(2.05f, 0.9f, -1.8f);
            Sphere("Head", skull.transform, Vector3.zero, 0.11f, decor.Bone);
            Sphere("EyeL", skull.transform, new Vector3(-0.045f, 0.02f, -0.09f), 0.024f, decor.DarkGray);
            Sphere("EyeR", skull.transform, new Vector3(0.045f, 0.02f, -0.09f), 0.024f, decor.DarkGray);

            // 北墙口两片半开的紫红幕帘（福克斯从这里探出来）
            SBox("CurtainL", root, new Vector3(-1.1f, 1.45f, 1.95f), new Vector3(1.1f, 2.9f, 0.08f), decor.StagePurple);
            SBox("CurtainR", root, new Vector3(1.1f, 1.45f, 1.95f), new Vector3(1.1f, 2.9f, 0.08f), decor.StagePurple);

            // "停业"木牌（两根柱 + 斜板，无文字）
            GameObject sign = new GameObject("ClosedSign");
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(0f, 0f, 1.6f);
            SBox("PostL", sign.transform, new Vector3(-0.55f, 0.55f, 0), new Vector3(0.06f, 1.1f, 0.06f), decor.Wood);
            SBox("PostR", sign.transform, new Vector3(0.55f, 0.55f, 0), new Vector3(0.06f, 1.1f, 0.06f), decor.Wood);
            GameObject board = SBox("Board", sign.transform, new Vector3(0, 0.95f, 0), new Vector3(1.3f, 0.5f, 0.05f), decor.Wood);
            board.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        private static void DecorHall(Transform root, DecorMats decor, float side)
        {
            // 侧墙三张海报（错落高度）
            Material[] papers = { decor.PosterCream, decor.PosterBlue, decor.PosterGreen };
            float wallX = side * 1.95f;
            for (int i = 0; i < 3; i++)
                Poster($"HallPoster{i}", root, new Vector3(wallX, 1.45f + (i % 2) * 0.35f, -1.6f + i * 1.8f),
                    -side * 90f, papers[i], decor.Frame);

            // 尽头垃圾桶
            Cylinder("TrashCan", root, new Vector3(-side * 1.5f, 0.34f, -3.2f), new Vector3(0.44f, 0.34f, 0.44f), decor.DarkGray);

            // 天花板管道（两段 + 接头法兰），工业感
            SBox("PipeA", root, new Vector3(0, 2.82f, -2.0f), new Vector3(0.16f, 0.16f, 2.4f), decor.DarkGray);
            SBox("PipeB", root, new Vector3(0, 2.82f, 0.6f), new Vector3(0.16f, 0.16f, 2.8f), decor.DarkGray);
            Cylinder("PipeFlangeA", root, new Vector3(0, 2.82f, -0.75f), new Vector3(0.26f, 0.08f, 0.26f), decor.DarkGray);
            Cylinder("PipeFlangeB", root, new Vector3(0, 2.82f, 2.05f), new Vector3(0.26f, 0.08f, 0.26f), decor.DarkGray);
            // 海报侧墙踢脚线
            SBox("Baseboard", root, new Vector3(side * 1.94f, 0.06f, 0), new Vector3(0.05f, 0.12f, 7.9f), decor.DarkGray);
        }

        private static void DecorCorner(Transform root, DecorMats decor, bool east)
        {
            // 角落纸箱堆
            Vector3 basePos = new Vector3(east ? 1.1f : -1.1f, 0, -1.1f);
            SBox("Crate0", root, basePos + new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), decor.Crate);
            SBox("Crate1", root, basePos + new Vector3(0.45f, 0.22f, 0.25f), new Vector3(0.45f, 0.45f, 0.45f), decor.Crate);
            SBox("Crate2", root, basePos + new Vector3(-0.1f, 0.83f, 0.05f), new Vector3(0.4f, 0.45f, 0.4f), decor.Crate);

            // 西侧角落（CAM 2B）的海报由金熊彩蛋接管：弗雷迪海报 + 隐藏的金熊脸特写
            if (east)
                Poster("CornerPoster", root, new Vector3(0, 1.55f, -1.95f), 0f, decor.PosterCream, decor.Frame);

            if (east)
            {
                // 东侧角落的纸杯蛋糕（奇卡同款）
                GameObject cupcake = new GameObject("Cupcake");
                cupcake.transform.SetParent(root, false);
                cupcake.transform.localPosition = basePos + new Vector3(-0.1f, 1.13f, 0.05f);
                Cylinder("CakeBase", cupcake.transform, new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.05f, 0.15f), decor.BuntingB);
                Sphere("Frosting", cupcake.transform, new Vector3(0, 0.13f, 0), 0.09f, decor.Pink);
                Cylinder("Candle", cupcake.transform, new Vector3(0, 0.25f, 0), new Vector3(0.025f, 0.045f, 0.025f), decor.Bone);
                Sphere("Flame", cupcake.transform, new Vector3(0, 0.31f, 0), 0.022f, decor.StarYellow);
            }
        }

        private static void DecorSupplyCloset(Transform root, DecorMats decor)
        {
            // 西墙置物架 + 清洁用品
            SBox("Shelf", root, new Vector3(-1.75f, 1.5f, 0), new Vector3(0.45f, 0.05f, 2.6f), decor.Wood);
            SBox("SupplyA", root, new Vector3(-1.75f, 1.68f, -0.7f), new Vector3(0.32f, 0.32f, 0.32f), decor.Crate);
            SBox("SupplyB", root, new Vector3(-1.75f, 1.66f, 0.3f), new Vector3(0.28f, 0.28f, 0.4f), decor.BuntingB);
            Cylinder("SupplyC", root, new Vector3(-1.75f, 1.63f, 0.9f), new Vector3(0.14f, 0.11f, 0.14f), decor.BuntingA);

            // 南侧：扫把、拖把、水桶
            BuildBroom("Broom", root, new Vector3(0.75f, 0, -1.7f), decor.Wood, decor.Bristle, new Vector3(0.1f, 0.14f, 0.36f));
            BuildBroom("Mop", root, new Vector3(1.05f, 0, -1.62f), decor.Wood, decor.DarkGray, new Vector3(0.18f, 0.08f, 0.42f));
            Cylinder("Bucket", root, new Vector3(1.15f, 0.17f, -1.8f), new Vector3(0.36f, 0.17f, 0.36f), decor.DarkGray);

            // 西侧纸箱
            SBox("Crate", root, new Vector3(-1.6f, 0.28f, 0.9f), new Vector3(0.56f, 0.56f, 0.56f), decor.Crate);
        }

        private static void DecorBackstage(Transform root, DecorMats decor)
        {
            // 长桌 + 三个备用玩偶头（FNAF 后台名场面）
            SBox("TableTop", root, new Vector3(-1.6f, 0.82f, 0), new Vector3(2.6f, 0.07f, 0.85f), decor.Wood);
            SBox("TableLegL", root, new Vector3(-2.75f, 0.4f, 0), new Vector3(0.07f, 0.8f, 0.8f), decor.Wood);
            SBox("TableLegR", root, new Vector3(-0.45f, 0.4f, 0), new Vector3(0.07f, 0.8f, 0.8f), decor.Wood);
            SpareHead("BearHead", root, new Vector3(-2.25f, 1.06f, 0), 0.2f, new Color(0.42f, 0.27f, 0.14f), decor);
            SpareHead("RabbitHead", root, new Vector3(-1.6f, 1.05f, 0.05f), 0.19f, new Color(0.35f, 0.25f, 0.56f), decor);
            SpareHead("BirdHead", root, new Vector3(-0.95f, 1.04f, -0.05f), 0.18f, new Color(0.85f, 0.75f, 0.15f), decor);

            // 北墙工具架
            SBox("Shelf", root, new Vector3(1.2f, 1.6f, 2.75f), new Vector3(2.2f, 0.05f, 0.4f), decor.Wood);
            Cylinder("CanA", root, new Vector3(0.6f, 1.72f, 2.75f), new Vector3(0.14f, 0.09f, 0.14f), decor.BuntingA);
            Cylinder("CanB", root, new Vector3(1.1f, 1.72f, 2.75f), new Vector3(0.12f, 0.08f, 0.12f), decor.BuntingB);
            SBox("ToolBox", root, new Vector3(1.7f, 1.73f, 2.75f), new Vector3(0.35f, 0.22f, 0.22f), decor.DarkGray);

            // 第四个备用头（狐狸）+ 靠墙的裸端骨架躯干（细思恐极的名场面暗示）
            SpareHead("FoxHead", root, new Vector3(-0.3f, 1.03f, 0.08f), 0.17f, new Color(0.55f, 0.16f, 0.12f), decor);
            SBox("EndoTorso", root, new Vector3(1.7f, 0.55f, -2.35f), new Vector3(0.44f, 0.8f, 0.3f), decor.DarkGray);
            Cylinder("EndoNeck", root, new Vector3(1.7f, 1.04f, -2.35f), new Vector3(0.13f, 0.14f, 0.13f), decor.DarkGray);
            Sphere("EndoSkull", root, new Vector3(1.7f, 1.22f, -2.3f), 0.15f, decor.DarkGray);
        }

        private static void DecorKitchen(Transform root, DecorMats decor)
        {
            // 厨房为仅音频房间，简单布景保持场景一致（监控画面里看不到）
            SBox("Counter", root, new Vector3(-0.8f, 0.45f, 2.6f), new Vector3(3.4f, 0.9f, 0.6f), decor.Porcelain);
            SBox("Stove", root, new Vector3(1.5f, 0.47f, 2.55f), new Vector3(1.1f, 0.94f, 0.7f), decor.DarkGray);
            Cylinder("PotA", root, new Vector3(1.25f, 1.05f, 2.55f), new Vector3(0.4f, 0.1f, 0.4f), decor.DarkGray);
            Cylinder("PotB", root, new Vector3(1.7f, 1.05f, 2.55f), new Vector3(0.34f, 0.09f, 0.34f), decor.BuntingA);
            SBox("Fridge", root, new Vector3(-2.4f, 0.95f, 2.45f), new Vector3(0.9f, 1.9f, 0.8f), decor.Porcelain);

            // 吊柜 + 墙面挂杆和两只挂锅
            SBox("UpperCabinet", root, new Vector3(-0.8f, 2.2f, 2.7f), new Vector3(3.0f, 0.7f, 0.45f), decor.Porcelain);
            SBox("PotRail", root, new Vector3(0f, 1.78f, 1.0f), new Vector3(2.2f, 0.045f, 0.045f), decor.DarkGray);
            SBox("HookA", root, new Vector3(-0.5f, 1.64f, 1.0f), new Vector3(0.035f, 0.24f, 0.035f), decor.DarkGray);
            SBox("HookB", root, new Vector3(0.5f, 1.64f, 1.0f), new Vector3(0.035f, 0.24f, 0.035f), decor.DarkGray);
            Cylinder("HangPotA", root, new Vector3(-0.5f, 1.44f, 1.0f), new Vector3(0.28f, 0.14f, 0.28f), decor.BuntingA);
            Cylinder("HangPotB", root, new Vector3(0.5f, 1.46f, 1.0f), new Vector3(0.24f, 0.12f, 0.24f), decor.BuntingB);
        }

        private static void DecorRestrooms(Transform root, DecorMats decor)
        {
            // 东墙：洗手台 + 镜子
            SBox("Counter", root, new Vector3(2.55f, 0.45f, -0.6f), new Vector3(0.55f, 0.9f, 2.8f), decor.Porcelain);
            Cylinder("SinkA", root, new Vector3(2.5f, 0.96f, -1.4f), new Vector3(0.46f, 0.05f, 0.46f), decor.Mirror);
            Cylinder("SinkB", root, new Vector3(2.5f, 0.96f, 0.2f), new Vector3(0.46f, 0.05f, 0.46f), decor.Mirror);
            SBox("Mirror", root, new Vector3(2.93f, 1.75f, -0.6f), new Vector3(0.04f, 1.0f, 2.4f), decor.Mirror);

            // 西墙：两个隔间
            for (int i = 0; i < 2; i++)
            {
                float z = -1.0f + i * 1.6f;
                Cylinder($"Toilet{i}", root, new Vector3(-2.5f, 0.2f, z), new Vector3(0.38f, 0.2f, 0.38f), decor.Porcelain);
                SBox($"Tank{i}", root, new Vector3(-2.8f, 0.55f, z), new Vector3(0.18f, 0.5f, 0.5f), decor.Porcelain);
                SBox($"Divider{i}", root, new Vector3(-1.9f, 0.95f, z + 0.75f), new Vector3(1.0f, 1.9f, 0.05f), decor.Porcelain);
                // 半开的隔间门
                SBox($"StallDoor{i}", root, new Vector3(-1.42f, 1.0f, z + 0.25f), new Vector3(0.04f, 1.7f, 0.5f), decor.Porcelain);
            }

            // 洗手台边墙挂式干手器
            SBox("Dryer", root, new Vector3(2.8f, 1.15f, 0.95f), new Vector3(0.14f, 0.36f, 0.28f), decor.DarkGray);
        }

        private static void DecorDoorVestibule(Transform root, DecorMats decor, float side)
        {
            // 远端墙上的欢庆海报（门外张望/开门灯时增加辨识度）
            float wallX = side * 1.46f;
            Poster("VestibulePoster", root, new Vector3(wallX, 1.55f, 0.3f), -side * 90f, decor.PosterCream, decor.Frame);
        }

        private static void Poster(string name, Transform parent, Vector3 localPos, float yaw, Material paper, Material frame)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            SBox("Paper", go.transform, Vector3.zero, new Vector3(0.62f, 0.86f, 0.03f), paper);
            SBox("Frame", go.transform, new Vector3(0, 0, -0.02f), new Vector3(0.72f, 0.96f, 0.03f), frame);
        }

        /// <summary>西走廊角落（CAM 2B）的弗雷迪海报——金熊彩蛋的"平常态"：画框 + 简笔弗雷迪脸。</summary>
        private static GameObject BuildFreddyPoster(Transform parent)
        {
            Material paper = AnimatronicFactory.StandardMat(new Color(0.80f, 0.74f, 0.56f), 0.1f);
            Material frame = AnimatronicFactory.StandardMat(new Color(0.15f, 0.12f, 0.08f), 0.3f);
            Material brown = AnimatronicFactory.StandardMat(new Color(0.42f, 0.27f, 0.14f), 0.3f);
            Material tan = AnimatronicFactory.StandardMat(new Color(0.54f, 0.42f, 0.23f), 0.25f);
            Material black = AnimatronicFactory.StandardMat(new Color(0.05f, 0.05f, 0.05f), 0.4f);

            GameObject go = new GameObject("FreddyPoster");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1.55f, -1.95f);

            SBox("Paper", go.transform, Vector3.zero, new Vector3(0.62f, 0.86f, 0.03f), paper);
            SBox("Frame", go.transform, new Vector3(0, 0, -0.02f), new Vector3(0.72f, 0.96f, 0.03f), frame);

            // 简笔弗雷迪：棕头 + 圆耳 + 黑眼 + 浅吻 + 小礼帽
            SBox("Face", go.transform, new Vector3(0, 0.08f, 0.03f), new Vector3(0.3f, 0.26f, 0.012f), brown);
            Sphere("EarL", go.transform, new Vector3(-0.13f, 0.23f, 0.03f), 0.05f, brown);
            Sphere("EarR", go.transform, new Vector3(0.13f, 0.23f, 0.03f), 0.05f, brown);
            SBox("EyeL", go.transform, new Vector3(-0.06f, 0.1f, 0.042f), new Vector3(0.045f, 0.05f, 0.008f), black);
            SBox("EyeR", go.transform, new Vector3(0.06f, 0.1f, 0.042f), new Vector3(0.045f, 0.05f, 0.008f), black);
            SBox("Snout", go.transform, new Vector3(0, 0.01f, 0.04f), new Vector3(0.14f, 0.08f, 0.01f), tan);
            SBox("Nose", go.transform, new Vector3(0, 0.03f, 0.05f), new Vector3(0.03f, 0.02f, 0.008f), black);
            SBox("HatBrim", go.transform, new Vector3(0, 0.24f, 0.03f), new Vector3(0.16f, 0.015f, 0.01f), black);
            SBox("HatCrown", go.transform, new Vector3(0, 0.29f, 0.03f), new Vector3(0.1f, 0.08f, 0.01f), black);
            return go;
        }

        /// <summary>西走廊角落（CAM 2B）的金熊脸特写海报——彩蛋触发态：金色大脸 + 黑眼窝 + 咧嘴白牙。</summary>
        private static GameObject BuildGoldenPoster(Transform parent)
        {
            Material gold = AnimatronicFactory.StandardMat(new Color(0.82f, 0.66f, 0.18f), 0.3f);
            Material black = AnimatronicFactory.StandardMat(new Color(0.04f, 0.04f, 0.04f), 0.4f);
            Material white = AnimatronicFactory.StandardMat(new Color(0.92f, 0.92f, 0.9f), 0.25f);

            GameObject go = new GameObject("GoldenPoster");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1.55f, -1.95f);

            SBox("Paper", go.transform, Vector3.zero, new Vector3(0.62f, 0.86f, 0.03f), gold);

            // 特写大脸：巨大的黑眼窝 + 白点瞳孔 + 黑鼻 + 咧开的黑洞嘴 + 白牙排 + 圆耳
            Sphere("EarL", go.transform, new Vector3(-0.26f, 0.38f, 0.03f), 0.07f, gold);
            Sphere("EarR", go.transform, new Vector3(0.26f, 0.38f, 0.03f), 0.07f, gold);
            SBox("SocketL", go.transform, new Vector3(-0.13f, 0.18f, 0.032f), new Vector3(0.16f, 0.2f, 0.01f), black);
            SBox("SocketR", go.transform, new Vector3(0.13f, 0.18f, 0.032f), new Vector3(0.16f, 0.2f, 0.01f), black);
            SBox("PupilL", go.transform, new Vector3(-0.13f, 0.18f, 0.042f), new Vector3(0.035f, 0.035f, 0.008f), white);
            SBox("PupilR", go.transform, new Vector3(0.13f, 0.18f, 0.042f), new Vector3(0.035f, 0.035f, 0.008f), white);
            SBox("Nose", go.transform, new Vector3(0, 0.03f, 0.04f), new Vector3(0.07f, 0.05f, 0.008f), black);
            SBox("Mouth", go.transform, new Vector3(0, -0.18f, 0.032f), new Vector3(0.34f, 0.14f, 0.01f), black);
            SBox("Teeth", go.transform, new Vector3(0, -0.12f, 0.04f), new Vector3(0.3f, 0.05f, 0.008f), white);
            return go;
        }

        /// <summary>办公室北墙"欢庆!"三人海报（熊/兔/鸡，复刻原版 Celebrate!）——返回可点击的弗雷迪鼻子（带碰撞体）。</summary>
        private static GameObject BuildCelebratePoster(Transform parent, Vector3 localPos)
        {
            Material paper = AnimatronicFactory.StandardMat(new Color(0.80f, 0.74f, 0.56f), 0.1f);
            Material frame = AnimatronicFactory.StandardMat(new Color(0.15f, 0.12f, 0.08f), 0.3f);
            Material brown = AnimatronicFactory.StandardMat(new Color(0.42f, 0.27f, 0.14f), 0.3f);
            Material tan = AnimatronicFactory.StandardMat(new Color(0.54f, 0.42f, 0.23f), 0.25f);
            Material purple = AnimatronicFactory.StandardMat(new Color(0.38f, 0.28f, 0.60f), 0.3f);
            Material yellow = AnimatronicFactory.StandardMat(new Color(0.85f, 0.70f, 0.20f), 0.3f);
            Material orange = AnimatronicFactory.StandardMat(new Color(0.90f, 0.50f, 0.10f), 0.3f);
            Material black = AnimatronicFactory.StandardMat(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            Material red = AnimatronicFactory.StandardMat(new Color(0.68f, 0.20f, 0.20f), 0.25f);
            Material star = AnimatronicFactory.StandardMat(new Color(0.95f, 0.85f, 0.30f), 0.3f);

            GameObject go = new GameObject("CelebratePoster");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            SBox("Paper", go.transform, Vector3.zero, new Vector3(0.62f, 0.86f, 0.03f), paper);
            SBox("Frame", go.transform, new Vector3(0, 0, -0.02f), new Vector3(0.72f, 0.96f, 0.03f), frame);

            // 顶部红色横幅 + 三颗星（庆祝感）
            SBox("Banner", go.transform, new Vector3(0, 0.33f, 0.03f), new Vector3(0.5f, 0.08f, 0.01f), red);
            for (int i = -1; i <= 1; i++)
            {
                GameObject starBox = SBox("Star" + (i + 2), go.transform, new Vector3(i * 0.16f, 0.405f, 0.03f),
                    new Vector3(0.05f, 0.05f, 0.008f), star);
                starBox.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 中间：弗雷迪（大脸 + 礼帽），鼻子单独可点击
            SBox("FreddyFace", go.transform, new Vector3(0, 0.02f, 0.03f), new Vector3(0.24f, 0.22f, 0.012f), brown);
            Sphere("FreddyEarL", go.transform, new Vector3(-0.10f, 0.15f, 0.03f), 0.04f, brown);
            Sphere("FreddyEarR", go.transform, new Vector3(0.10f, 0.15f, 0.03f), 0.04f, brown);
            SBox("FreddyHatBrim", go.transform, new Vector3(0, 0.195f, 0.03f), new Vector3(0.15f, 0.012f, 0.01f), black);
            SBox("FreddyHatCrown", go.transform, new Vector3(0, 0.24f, 0.03f), new Vector3(0.09f, 0.07f, 0.01f), black);
            SBox("FreddyEyeL", go.transform, new Vector3(-0.05f, 0.065f, 0.042f), new Vector3(0.04f, 0.045f, 0.008f), black);
            SBox("FreddyEyeR", go.transform, new Vector3(0.05f, 0.065f, 0.042f), new Vector3(0.04f, 0.045f, 0.008f), black);
            SBox("FreddySnout", go.transform, new Vector3(0, -0.025f, 0.04f), new Vector3(0.12f, 0.07f, 0.01f), tan);

            // 左：邦尼（长耳 + 浅吻）
            SBox("BonnieEarL", go.transform, new Vector3(-0.235f, 0.12f, 0.03f), new Vector3(0.04f, 0.13f, 0.01f), purple);
            SBox("BonnieEarR", go.transform, new Vector3(-0.145f, 0.12f, 0.03f), new Vector3(0.04f, 0.13f, 0.01f), purple);
            SBox("BonnieFace", go.transform, new Vector3(-0.19f, 0f, 0.03f), new Vector3(0.17f, 0.16f, 0.012f), purple);
            SBox("BonnieEyeL", go.transform, new Vector3(-0.215f, 0.03f, 0.042f), new Vector3(0.035f, 0.04f, 0.008f), black);
            SBox("BonnieEyeR", go.transform, new Vector3(-0.165f, 0.03f, 0.042f), new Vector3(0.035f, 0.04f, 0.008f), black);
            SBox("BonnieSnout", go.transform, new Vector3(-0.19f, -0.04f, 0.04f), new Vector3(0.09f, 0.05f, 0.01f), tan);

            // 右：奇卡（呆毛 + 橙喙）
            SBox("ChicaTuft", go.transform, new Vector3(0.19f, 0.11f, 0.03f), new Vector3(0.02f, 0.05f, 0.01f), yellow);
            SBox("ChicaFace", go.transform, new Vector3(0.19f, 0f, 0.03f), new Vector3(0.17f, 0.16f, 0.012f), yellow);
            SBox("ChicaEyeL", go.transform, new Vector3(0.165f, 0.03f, 0.042f), new Vector3(0.035f, 0.04f, 0.008f), black);
            SBox("ChicaEyeR", go.transform, new Vector3(0.215f, 0.03f, 0.042f), new Vector3(0.035f, 0.04f, 0.008f), black);
            SBox("ChicaBeakTop", go.transform, new Vector3(0.19f, -0.045f, 0.04f), new Vector3(0.07f, 0.03f, 0.01f), orange);
            SBox("ChicaBeakLow", go.transform, new Vector3(0.19f, -0.078f, 0.04f), new Vector3(0.06f, 0.02f, 0.01f), orange);

            // 底部三色彩旗条
            SBox("FlagA", go.transform, new Vector3(-0.1f, -0.36f, 0.03f), new Vector3(0.08f, 0.05f, 0.008f), red);
            SBox("FlagB", go.transform, new Vector3(0f, -0.36f, 0.03f), new Vector3(0.08f, 0.05f, 0.008f), star);
            SBox("FlagC", go.transform, new Vector3(0.1f, -0.36f, 0.03f), new Vector3(0.08f, 0.05f, 0.008f), purple);

            // 点击目标：弗雷迪鼻尖（碰撞体比视觉略大，隔着屏幕好点中）
            GameObject nose = new GameObject("ClickableNose");
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.052f);
            nose.AddComponent<BoxCollider>().size = new Vector3(0.1f, 0.09f, 0.03f);
            SBox("NoseTip", nose.transform, Vector3.zero, new Vector3(0.034f, 0.024f, 0.01f), black);
            return nose;
        }

        private static void BuildChair(string name, Transform parent, Vector3 localPos, float yawDeg, Material mat)
        {
            GameObject chair = new GameObject(name);
            chair.transform.SetParent(parent, false);
            chair.transform.localPosition = localPos;
            chair.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            SBox("Seat", chair.transform, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), mat);
            SBox("Back", chair.transform, new Vector3(0, 0.72f, -0.19f), new Vector3(0.42f, 0.5f, 0.05f), mat);
            SBox("LegL", chair.transform, new Vector3(-0.16f, 0.22f, 0), new Vector3(0.05f, 0.45f, 0.05f), mat);
            SBox("LegR", chair.transform, new Vector3(0.16f, 0.22f, 0), new Vector3(0.05f, 0.45f, 0.05f), mat);
        }

        private static void BuildBroom(string name, Transform parent, Vector3 localPos, Material handleMat, Material headMat, Vector3 headSize)
        {
            GameObject broom = new GameObject(name);
            broom.transform.SetParent(parent, false);
            broom.transform.localPosition = localPos;
            Cylinder("Pole", broom.transform, new Vector3(0, 0.75f, 0), new Vector3(0.045f, 0.75f, 0.045f), handleMat);
            SBox("Head", broom.transform, new Vector3(0, headSize.y * 0.5f, 0), headSize, headMat);
        }

        private static void SpareHead(string name, Transform parent, Vector3 localPos, float radius, Color color, DecorMats decor)
        {
            Material mat = AnimatronicFactory.StandardMat(color, 0.2f);
            GameObject head = new GameObject(name);
            head.transform.SetParent(parent, false);
            head.transform.localPosition = localPos;
            Sphere("Skull", head.transform, Vector3.zero, radius, mat);
            Sphere("EyeL", head.transform, new Vector3(-radius * 0.4f, radius * 0.15f, radius * 0.82f), radius * 0.14f, decor.DarkGray);
            Sphere("EyeR", head.transform, new Vector3(radius * 0.4f, radius * 0.15f, radius * 0.82f), radius * 0.14f, decor.DarkGray);
            Cylinder("Stand", head.transform, new Vector3(0, -0.2f, 0), new Vector3(radius * 0.8f, 0.04f, radius * 0.8f), decor.DarkGray);
        }

        // ---------- 通用辅助 ----------

        private static void EnsureAssets()
        {
            // 音效全部程序化，每次一键覆盖重写——音色改动总能生效（无需手动删旧 wav）
            WavGenerator.GenerateAll();
            NightConfigFactory.EnsureAll();
        }

        /// <summary>取音频：Audio/Recorded/ 下有同名用户录音则优先（重跑一键不会被合成版覆盖），否则用程序化合成版。</summary>
        private static AudioClip Clip(string name)
        {
            AudioClip recorded = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/Recorded/{name}.wav");
            if (recorded != null) return recorded;
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");
        }

        /// <summary>音量默认值版本：改代码里的默认音量时 +1，下次 Build 忽略旧场景记忆、让新默认值生效。</summary>
        private const int VolumeDefaultsVersion = 3;
        private const string VolumeVersionKey = "fnaf.volumeDefaultsVersion";

        /// <summary>本次 Build 前从旧场景收集的音源音量（Src_ 前缀），CreateSource 创建时沿用——让编辑器成为音量的唯一修改入口。</summary>
        private static Dictionary<string, float> pendingVolumes;

        /// <summary>遍历旧场景 FNAF_Systems 下所有 Src_ 音源，记录 名称→音量。</summary>
        private static Dictionary<string, float> CollectSourceVolumes()
        {
            Dictionary<string, float> map = new Dictionary<string, float>();
            Scene prev = EditorSceneManager.GetActiveScene();
            if (!prev.IsValid() || !prev.isLoaded) return map;
            foreach (GameObject root in prev.GetRootGameObjects())
            {
                if (root.name != "FNAF_Systems") continue;
                foreach (AudioSource src in root.GetComponentsInChildren<AudioSource>(true))
                    if (src.name.StartsWith("Src_"))
                        map[src.name] = src.volume;
            }
            return map;
        }

        private static AudioSource CreateSource(Transform parent, string name, AudioClip clip, bool loop, float volume)
        {
            // 用户在场景里调过的音量优先于代码默认值（Build 开头从旧场景抓取）
            if (pendingVolumes != null && pendingVolumes.TryGetValue(name, out float kept))
                volume = kept;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            if (clip == null) Debug.LogWarning($"[SceneBuilder] 音源 {name} 未找到音频文件（检查 Audio 与 Audio/Recorded 目录）");
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        private static void PlaceAt(AnimatronicBody body, Transform stop) =>
            body.transform.SetPositionAndRotation(stop.position, stop.rotation);

        private static Transform CreateStop(Transform parent, Vector3 localPos, float yawDeg)
        {
            GameObject go = new GameObject("Stop");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            return go.transform;
        }

        private static GameObject SBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Sphere(string name, Transform parent, Vector3 localPos, float radius, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Material EmissiveMat(Color color)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color * 0.5f;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.6f);
            return mat;
        }
    }
}
