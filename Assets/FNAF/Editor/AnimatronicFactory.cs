using UnityEditor;
using UnityEngine;
using FNAF.Data;
using FNAF.Enemies;

namespace FNAF.EditorTools
{
    /// <summary>
    /// 积木玩偶工厂：用基本几何体拼出四只玩偶（躯干/头/耳/吻部/发光眼睛/手脚/颈肩），
    /// 并为每只加上标志性配件（礼帽领结麦克风/红领结大门牙/围兜/眼罩铁钩尖牙）。
    /// 复用项目约定——先用积木风，后期可替换正式模型。
    /// </summary>
    internal static class AnimatronicFactory
    {
        internal enum Kind
        {
            Bear,   // 弗雷迪
            Rabbit, // 邦尼
            Bird,   // 奇卡
            Fox     // 狐狸
        }

        /// <summary>拼装一只玩偶身体并返回其 AnimatronicBody 组件。</summary>
        internal static AnimatronicBody BuildBody(Transform parent, string id, Kind kind,
            Color primary, Color secondary)
        {
            GameObject root = new GameObject(id);
            root.transform.SetParent(parent, false);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            Material primaryMat = StandardMat(primary, 0.35f);
            Material secondaryMat = StandardMat(secondary, 0.3f);
            Material eyeMat = EyeMat();
            Material blackMat = StandardMat(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            Material whiteMat = StandardMat(new Color(0.92f, 0.92f, 0.9f), 0.25f);
            Material metalMat = MetalMat();
            Material redMat = StandardMat(new Color(0.75f, 0.12f, 0.12f), 0.3f);

            // 腿与脚：狐狸左腿外皮破损失效，露出下半截金属骨架
            if (kind == Kind.Fox)
            {
                Box("LegL", visual.transform, new Vector3(-0.18f, 0.56f, 0f), new Vector3(0.22f, 0.42f, 0.28f), secondaryMat);
                Cylinder("LegEndoL", visual.transform, new Vector3(-0.18f, 0.17f, 0f), 0.045f, 0.38f, metalMat);
            }
            else
            {
                Box("LegL", visual.transform, new Vector3(-0.18f, 0.375f, 0f), new Vector3(0.22f, 0.75f, 0.28f), secondaryMat);
            }
            Box("LegR", visual.transform, new Vector3(0.18f, 0.375f, 0f), new Vector3(0.22f, 0.75f, 0.28f), secondaryMat);
            Box("FootL", visual.transform, new Vector3(-0.18f, 0.07f, 0.06f), new Vector3(0.26f, 0.14f, 0.4f), secondaryMat);
            Box("FootR", visual.transform, new Vector3(0.18f, 0.07f, 0.06f), new Vector3(0.26f, 0.14f, 0.4f), secondaryMat);

            // 胯、躯干（熊/兔有浅色肚皮）
            Box("Hips", visual.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.25f, 0.4f), primaryMat);
            Box("Torso", visual.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.7f, 0.75f, 0.45f), primaryMat);
            if (kind == Kind.Bear || kind == Kind.Rabbit)
                Box("Belly", visual.transform, new Vector3(0f, 1.16f, 0.225f), new Vector3(0.46f, 0.34f, 0.03f), secondaryMat);

            // 手臂与肩关节：狐狸右手是铁钩（外皮撕裂露出金属骨架）
            Sphere("ShoulderL", visual.transform, new Vector3(-0.47f, 1.6f, 0f), 0.12f, primaryMat);
            Box("ArmL", visual.transform, new Vector3(-0.47f, 1.3f, 0f), new Vector3(0.18f, 0.7f, 0.22f), primaryMat);
            Sphere("ShoulderR", visual.transform, new Vector3(0.47f, 1.6f, 0f), 0.12f, primaryMat);
            if (kind == Kind.Fox)
            {
                Box("ArmR", visual.transform, new Vector3(0.47f, 1.48f, 0f), new Vector3(0.18f, 0.4f, 0.22f), primaryMat);
                Cylinder("ArmEndoR", visual.transform, new Vector3(0.47f, 1.09f, 0f), 0.038f, 0.38f, metalMat);
                // 钩子：三段金属条拼出 J 形
                Box("HookStem", visual.transform, new Vector3(0.47f, 0.82f, 0f), new Vector3(0.04f, 0.16f, 0.04f), metalMat);
                Box("HookCurve", visual.transform, new Vector3(0.47f, 0.76f, 0.06f), new Vector3(0.04f, 0.04f, 0.14f), metalMat);
                Box("HookTip", visual.transform, new Vector3(0.47f, 0.8f, 0.12f), new Vector3(0.04f, 0.1f, 0.04f), metalMat);
            }
            else
            {
                Box("ArmR", visual.transform, new Vector3(0.47f, 1.3f, 0f), new Vector3(0.18f, 0.7f, 0.22f), primaryMat);
            }

            // 手掌（狐狸右手是钩子，无手掌）
            Box("HandL", visual.transform, new Vector3(-0.47f, 0.86f, 0.03f), new Vector3(0.2f, 0.18f, 0.26f), primaryMat);
            if (kind != Kind.Fox)
                Box("HandR", visual.transform, new Vector3(0.47f, 0.86f, 0.03f), new Vector3(0.2f, 0.18f, 0.26f), primaryMat);

            // 机械关节精细化：肘/膝关节球 + 手腕金属环 + 双节颈管，突出"外皮下的骨架"
            Sphere("ElbowL", visual.transform, new Vector3(-0.47f, 1.3f, 0f), 0.095f, primaryMat);
            Sphere("ElbowR", visual.transform, new Vector3(0.47f, 1.3f, 0f), 0.095f, primaryMat);
            Sphere("KneeL", visual.transform, new Vector3(-0.18f, 0.72f, 0f), 0.105f, secondaryMat);
            Sphere("KneeR", visual.transform, new Vector3(0.18f, 0.72f, 0f), 0.105f, secondaryMat);
            GameObject wristL = Cylinder("WristL", visual.transform, new Vector3(-0.47f, 0.97f, 0f), 0.105f, 0.035f, metalMat);
            wristL.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (kind != Kind.Fox)
            {
                GameObject wristR = Cylinder("WristR", visual.transform, new Vector3(0.47f, 0.97f, 0f), 0.105f, 0.035f, metalMat);
                wristR.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // 颈与头
            Cylinder("Neck", visual.transform, new Vector3(0f, 1.74f, 0f), 0.08f, 0.16f, primaryMat);
            GameObject neckRing = Cylinder("NeckRing", visual.transform, new Vector3(0f, 1.79f, 0f), 0.095f, 0.03f, metalMat);
            GameObject head = Box("Head", visual.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.5f, 0.42f, 0.42f), primaryMat);

            // 自发光眼睛 + 内侧下压的怒眉（跳杀时更凶）
            GameObject eyeL = Box("EyeL", head.transform, new Vector3(-0.11f, 0.06f, 0.22f), new Vector3(0.07f, 0.07f, 0.05f), eyeMat);
            GameObject eyeR = Box("EyeR", head.transform, new Vector3(0.11f, 0.06f, 0.22f), new Vector3(0.07f, 0.07f, 0.05f), eyeMat);

            // 眼睛伴生点光：黑暗中（断电处刑）眼睛的"真实发光"光源——纯材质自发光在某些管线状态下不可靠，
            // 点光 100% 可见，还能在纯黑房间里映出熊脸的轮廓
            GameObject eyeGlowObj = new GameObject("EyeGlowLight");
            eyeGlowObj.transform.SetParent(head.transform, false);
            eyeGlowObj.transform.localPosition = new Vector3(0f, 0.06f, 0.26f); // 两眼之间、微微前伸
            Light eyeGlow = eyeGlowObj.AddComponent<Light>();
            eyeGlow.type = LightType.Point;
            eyeGlow.color = new Color(1f, 0.86f, 0.6f);
            eyeGlow.intensity = 1.7f;
            eyeGlow.range = 2f;
            eyeGlow.shadows = LightShadows.None;
            eyeGlow.enabled = false;
            GameObject browL = Box("BrowL", head.transform, new Vector3(-0.11f, 0.145f, 0.22f), new Vector3(0.1f, 0.035f, 0.05f), blackMat);
            browL.transform.localRotation = Quaternion.Euler(0f, 0f, -0.25f);
            GameObject browR = Box("BrowR", head.transform, new Vector3(0.11f, 0.145f, 0.22f), new Vector3(0.1f, 0.035f, 0.05f), blackMat);
            browR.transform.localRotation = Quaternion.Euler(0f, 0f, 0.25f);

            // 头部特征（吻部/喙/鼻/牙/标志性配件，按物种）
            switch (kind)
            {
                case Kind.Bear:
                    Box("Snout", head.transform, new Vector3(0f, -0.06f, 0.26f), new Vector3(0.3f, 0.18f, 0.14f), secondaryMat);
                    Box("Nose", head.transform, new Vector3(0f, -0.02f, 0.34f), new Vector3(0.07f, 0.05f, 0.04f), blackMat);
                    Cylinder("HatBrim", head.transform, new Vector3(0f, 0.235f, 0f), 0.19f, 0.035f, blackMat);
                    Cylinder("HatCrown", head.transform, new Vector3(0f, 0.335f, 0f), 0.115f, 0.17f, blackMat);
                    // 帽檐上的紫色缎带
                    Cylinder("HatBand", head.transform, new Vector3(0f, 0.268f, 0f), 0.122f, 0.045f, StandardMat(new Color(0.32f, 0.18f, 0.42f), 0.4f));
                    // 脸颊雀斑（左右各三颗，原版标志）
                    for (int i = 0; i < 3; i++)
                    {
                        Sphere("FreckleL" + i, head.transform, new Vector3(-0.175f + i * 0.045f, -0.045f, 0.205f), 0.013f, blackMat);
                        Sphere("FreckleR" + i, head.transform, new Vector3(0.175f - i * 0.045f, -0.045f, 0.205f), 0.013f, blackMat);
                    }
                    // 圆耳内的深色耳廓
                    Sphere("EarInnerL", head.transform, new Vector3(-0.2f, 0.29f, 0.055f), 0.055f, blackMat);
                    Sphere("EarInnerR", head.transform, new Vector3(0.2f, 0.29f, 0.055f), 0.055f, blackMat);
                    break;
                case Kind.Rabbit:
                    Box("Snout", head.transform, new Vector3(0f, -0.08f, 0.24f), new Vector3(0.22f, 0.14f, 0.12f), secondaryMat);
                    Box("Nose", head.transform, new Vector3(0f, -0.04f, 0.305f), new Vector3(0.05f, 0.04f, 0.035f), redMat);
                    // 两颗大门牙
                    Box("ToothL", head.transform, new Vector3(-0.024f, -0.14f, 0.3f), new Vector3(0.038f, 0.09f, 0.03f), whiteMat);
                    Box("ToothR", head.transform, new Vector3(0.024f, -0.14f, 0.3f), new Vector3(0.038f, 0.09f, 0.03f), whiteMat);
                    break;
                case Kind.Bird:
                    Box("Beak", head.transform, new Vector3(0f, -0.08f, 0.26f), new Vector3(0.34f, 0.1f, 0.18f), secondaryMat);
                    // 微张的下喙
                    GameObject lowerBeak = Box("BeakLower", head.transform, new Vector3(0f, -0.155f, 0.24f), new Vector3(0.3f, 0.06f, 0.14f), secondaryMat);
                    lowerBeak.transform.localRotation = Quaternion.Euler(0.15f, 0f, 0f);
                    break;
                case Kind.Fox:
                    Box("Snout", head.transform, new Vector3(0f, -0.05f, 0.34f), new Vector3(0.16f, 0.13f, 0.34f), secondaryMat);
                    Box("Nose", head.transform, new Vector3(0f, -0.02f, 0.515f), new Vector3(0.05f, 0.045f, 0.04f), blackMat);
                    // 下颚 + 四颗尖牙
                    Box("Jaw", head.transform, new Vector3(0f, -0.145f, 0.3f), new Vector3(0.14f, 0.05f, 0.3f), secondaryMat);
                    for (int i = 0; i < 4; i++)
                        Box("Fang" + i, head.transform, new Vector3(-0.054f + i * 0.036f, -0.04f, 0.5f), new Vector3(0.03f, 0.08f, 0.03f), whiteMat);
                    // 翻起的眼罩（压在右眼上方）
                    GameObject patch = Box("EyePatch", head.transform, new Vector3(0.11f, 0.175f, 0.205f), new Vector3(0.13f, 0.06f, 0.04f), blackMat);
                    patch.transform.localRotation = Quaternion.Euler(0f, 0f, -0.3f);
                    // 耳内的深色耳腔
                    Box("EarInnerL", head.transform, new Vector3(-0.18f, 0.34f, 0.02f), new Vector3(0.05f, 0.22f, 0.03f), blackMat);
                    Box("EarInnerR", head.transform, new Vector3(0.18f, 0.34f, 0.02f), new Vector3(0.05f, 0.22f, 0.03f), blackMat);
                    break;
            }

            // 耳朵（按物种区分）
            switch (kind)
            {
                case Kind.Bear:
                    Sphere("EarL", head.transform, new Vector3(-0.2f, 0.28f, 0f), 0.12f, secondaryMat);
                    Sphere("EarR", head.transform, new Vector3(0.2f, 0.28f, 0f), 0.12f, secondaryMat);
                    break;
                case Kind.Rabbit:
                    // FNAF1 邦尼标志性长耳：显著加长+顶端圆头，耳腔作为耳朵子物体跟随旋转不脱节
                    GameObject earL = Box("EarL", head.transform, new Vector3(-0.17f, 0.56f, 0f), new Vector3(0.13f, 0.8f, 0.11f), secondaryMat);
                    earL.transform.localRotation = Quaternion.Euler(0.1f, 0f, 0.12f);
                    Box("EarInnerL", earL.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.6f, 0.03f), blackMat);
                    Sphere("EarTipL", earL.transform, new Vector3(0f, 0.4f, 0f), 0.066f, secondaryMat);
                    GameObject earR = Box("EarR", head.transform, new Vector3(0.17f, 0.56f, 0f), new Vector3(0.13f, 0.8f, 0.11f), secondaryMat);
                    earR.transform.localRotation = Quaternion.Euler(0.1f, 0f, -0.12f);
                    Box("EarInnerR", earR.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.6f, 0.03f), blackMat);
                    Sphere("EarTipR", earR.transform, new Vector3(0f, 0.4f, 0f), 0.066f, secondaryMat);
                    break;
                case Kind.Bird:
                    for (int i = -1; i <= 1; i++)
                        Box("Crest" + i, head.transform, new Vector3(i * 0.09f, 0.26f, 0f), new Vector3(0.06f, 0.16f, 0.06f), secondaryMat);
                    break;
                case Kind.Fox:
                    GameObject fEarL = Box("EarL", head.transform, new Vector3(-0.18f, 0.32f, 0f), new Vector3(0.1f, 0.32f, 0.08f), secondaryMat);
                    fEarL.transform.localRotation = Quaternion.Euler(0f, 0f, 0.35f);
                    GameObject fEarR = Box("EarR", head.transform, new Vector3(0.18f, 0.32f, 0f), new Vector3(0.1f, 0.32f, 0.08f), secondaryMat);
                    fEarR.transform.localRotation = Quaternion.Euler(0f, 0f, -0.35f);
                    break;
            }

            // 胸前配饰（按物种：熊=黑领结+双排扣+麦克风，兔=红领结，鸟=围兜，狐=胸毛）
            switch (kind)
            {
                case Kind.Bear:
                    BowTie(visual.transform, 1.665f, blackMat);
                    GameObject btn1 = Cylinder("BtnTop", visual.transform, new Vector3(0f, 1.5f, 0.23f), 0.04f, 0.03f, blackMat);
                    btn1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameObject btn2 = Cylinder("BtnLow", visual.transform, new Vector3(0f, 1.37f, 0.23f), 0.04f, 0.03f, blackMat);
                    btn2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    // 右手麦克风（歌手）
                    Cylinder("MicHandle", visual.transform, new Vector3(0.47f, 0.98f, 0.05f), 0.018f, 0.2f, blackMat);
                    Sphere("MicBall", visual.transform, new Vector3(0.47f, 1.09f, 0.075f), 0.05f, metalMat);
                    break;
                case Kind.Rabbit:
                    BowTie(visual.transform, 1.665f, redMat);
                    break;
                case Kind.Bird:
                    // 素白围兜 + 上下滚边（原版围兜文字按需求去除）
                    Box("Bib", visual.transform, new Vector3(0f, 1.5f, 0.225f), new Vector3(0.44f, 0.34f, 0.03f), whiteMat);
                    Box("BibTrimTop", visual.transform, new Vector3(0f, 1.655f, 0.245f), new Vector3(0.44f, 0.028f, 0.02f), secondaryMat);
                    Box("BibTrimLow", visual.transform, new Vector3(0f, 1.345f, 0.245f), new Vector3(0.44f, 0.028f, 0.02f), secondaryMat);
                    // 左手托着的纸杯蛋糕（奇卡标志性道具）
                    GameObject cupcake = new GameObject("Cupcake");
                    cupcake.transform.SetParent(visual.transform, false);
                    cupcake.transform.localPosition = new Vector3(-0.47f, 1.02f, 0.08f);
                    Material cupMat = StandardMat(new Color(0.55f, 0.32f, 0.18f), 0.3f);
                    Material frostMat = StandardMat(new Color(0.95f, 0.55f, 0.7f), 0.35f);
                    Cylinder("CupBase", cupcake.transform, new Vector3(0, 0.03f, 0), 0.075f, 0.06f, cupMat);
                    Sphere("Frosting", cupcake.transform, new Vector3(0, 0.08f, 0), 0.06f, frostMat);
                    Cylinder("Candle", cupcake.transform, new Vector3(0, 0.16f, 0), 0.012f, 0.05f, whiteMat);
                    Sphere("Flame", cupcake.transform, new Vector3(0, 0.2f, 0), 0.018f, EyeMat());
                    // 蛋糕上的两只大眼（盯着人的错位萌）
                    Sphere("CakeEyeL", cupcake.transform, new Vector3(-0.025f, 0.1f, 0.05f), 0.016f, eyeMat);
                    Sphere("CakeEyeR", cupcake.transform, new Vector3(0.025f, 0.1f, 0.05f), 0.016f, eyeMat);
                    break;
                case Kind.Fox:
                    Box("ChestTuft", visual.transform, new Vector3(0f, 1.28f, 0.225f), new Vector3(0.34f, 0.44f, 0.03f), secondaryMat);
                    // 胸口外皮破损失效骨架斑 + 左臂下段露骨架（原版双臂皆损，右手已是钩）
                    Box("TorsoTear", visual.transform, new Vector3(-0.2f, 1.42f, 0.235f), new Vector3(0.22f, 0.3f, 0.02f), metalMat);
                    Cylinder("ArmEndoL", visual.transform, new Vector3(-0.47f, 1.05f, 0f), 0.04f, 0.42f, metalMat);
                    break;
            }

            // 组装 AnimatronicBody
            AnimatronicBody body = root.AddComponent<AnimatronicBody>();
            Wire.Ref(body, "head", head.transform);
            Wire.RefArray(body, "eyeRenderers", new Object[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() });
            Wire.Ref(body, "eyeGlowLight", eyeGlow);
            Wire.Ref(body, "visualRoot", visual);
            Wire.ColorField(body, "eyeGlowColor", new Color(1f, 0.82f, 0.55f));

            return body;
        }

        /// <summary>挂 AI 组件并完成公共字段接线（类型专属字段由场景生成器补充）。</summary>
        internal static T AttachAI<T>(GameObject go, string enemyId, AnimatronicBody body, RoomRegistry registry)
            where T : AnimatronicAI
        {
            T ai = go.AddComponent<T>();
            Wire.Str(ai, "enemyId", enemyId);
            Wire.Ref(ai, "body", body);
            Wire.Ref(ai, "roomRegistry", registry);
            Wire.Float(ai, "moveOpportunityInterval", 5f);
            return ai;
        }

        // ---------- 几何体与材质 ----------

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
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

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, float radius, float height, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// 拼装瘫坐姿势的金熊（Golden Freddy 彩蛋专用）：金色身体坐在地上、双腿前伸、
        /// 双臂摊在外侧、头歪向一边、黑色空眼窝带白点瞳孔、黑礼帽黑领结。
        /// 不挂 AnimatronicBody/AI——由 GoldenFreddyController 直接驱动。
        /// </summary>
        internal static GameObject BuildGoldenFreddy(Transform parent, out Transform head)
        {
            GameObject root = new GameObject("GoldenFreddy");
            root.transform.SetParent(parent, false);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            Material goldMat = StandardMat(new Color(0.78f, 0.62f, 0.18f), 0.35f);
            Material goldLightMat = StandardMat(new Color(0.87f, 0.74f, 0.34f), 0.3f);
            Material blackMat = StandardMat(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            Material whiteMat = StandardMat(new Color(0.92f, 0.92f, 0.9f), 0.25f);

            // 双腿前伸平铺在地上 + 双脚
            Box("LegL", visual.transform, new Vector3(-0.17f, 0.085f, 0.38f), new Vector3(0.2f, 0.17f, 0.72f), goldLightMat);
            Box("LegR", visual.transform, new Vector3(0.17f, 0.085f, 0.38f), new Vector3(0.2f, 0.17f, 0.72f), goldLightMat);
            Box("FootL", visual.transform, new Vector3(-0.17f, 0.09f, 0.78f), new Vector3(0.24f, 0.14f, 0.2f), goldLightMat);
            Box("FootR", visual.transform, new Vector3(0.17f, 0.09f, 0.78f), new Vector3(0.24f, 0.14f, 0.2f), goldLightMat);

            // 坐姿胯 + 微微后仰的躯干 + 浅色肚皮
            Box("Hips", visual.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.58f, 0.3f, 0.44f), goldMat);
            GameObject torso = Box("Torso", visual.transform, new Vector3(0f, 0.68f, -0.02f), new Vector3(0.66f, 0.52f, 0.44f), goldMat);
            torso.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Box("Belly", visual.transform, new Vector3(0f, 0.6f, 0.21f), new Vector3(0.42f, 0.34f, 0.03f), goldLightMat);

            // 摊在身侧的双臂与手掌
            GameObject armL = Box("ArmL", visual.transform, new Vector3(-0.43f, 0.5f, 0.03f), new Vector3(0.16f, 0.6f, 0.2f), goldMat);
            armL.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            GameObject armR = Box("ArmR", visual.transform, new Vector3(0.43f, 0.5f, 0.03f), new Vector3(0.16f, 0.6f, 0.2f), goldMat);
            armR.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            Sphere("HandL", visual.transform, new Vector3(-0.5f, 0.18f, 0.06f), 0.1f, goldMat);
            Sphere("HandR", visual.transform, new Vector3(0.5f, 0.18f, 0.06f), 0.1f, goldMat);

            // 颈 + 歪向一侧的头（含配件）
            Cylinder("Neck", visual.transform, new Vector3(0f, 0.97f, -0.04f), 0.07f, 0.12f, goldMat);
            GameObject gfHead = Box("Head", visual.transform, new Vector3(0f, 1.17f, -0.02f), new Vector3(0.46f, 0.38f, 0.38f), goldMat);
            gfHead.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);

            // 黑色空眼窝 + 白点瞳孔（"灵魂出窍"的眼神）
            Box("SocketL", gfHead.transform, new Vector3(-0.1f, 0.03f, 0.195f), new Vector3(0.1f, 0.12f, 0.04f), blackMat);
            Box("SocketR", gfHead.transform, new Vector3(0.1f, 0.03f, 0.195f), new Vector3(0.1f, 0.12f, 0.04f), blackMat);
            Box("PupilL", gfHead.transform, new Vector3(-0.1f, 0.03f, 0.215f), new Vector3(0.02f, 0.02f, 0.02f), whiteMat);
            Box("PupilR", gfHead.transform, new Vector3(0.1f, 0.03f, 0.215f), new Vector3(0.02f, 0.02f, 0.02f), whiteMat);

            // 吻部 + 黑鼻 + 张开的黑洞嘴 + 一排白牙
            Box("Snout", gfHead.transform, new Vector3(0f, -0.1f, 0.2f), new Vector3(0.26f, 0.14f, 0.12f), goldLightMat);
            Box("Nose", gfHead.transform, new Vector3(0f, -0.06f, 0.27f), new Vector3(0.06f, 0.045f, 0.04f), blackMat);
            Box("Mouth", gfHead.transform, new Vector3(0f, -0.18f, 0.19f), new Vector3(0.22f, 0.1f, 0.04f), blackMat);
            Box("Teeth", gfHead.transform, new Vector3(0f, -0.135f, 0.205f), new Vector3(0.2f, 0.03f, 0.025f), whiteMat);

            // 圆耳 + 黑礼帽（跟头部一起歪）
            Sphere("EarL", gfHead.transform, new Vector3(-0.25f, 0.17f, -0.02f), 0.085f, goldMat);
            Sphere("EarR", gfHead.transform, new Vector3(0.25f, 0.17f, -0.02f), 0.085f, goldMat);
            Cylinder("HatBrim", gfHead.transform, new Vector3(0f, 0.21f, 0f), 0.17f, 0.028f, blackMat);
            Cylinder("HatCrown", gfHead.transform, new Vector3(0f, 0.3f, 0f), 0.105f, 0.14f, blackMat);

            // 黑领结（手写坐标贴住后仰的躯干，BowTie 辅助的 z 是站姿值会悬空）
            GameObject bowL = Box("BowL", visual.transform, new Vector3(-0.055f, 0.92f, 0.185f), new Vector3(0.09f, 0.05f, 0.035f), blackMat);
            bowL.transform.localRotation = Quaternion.Euler(-8f, 0f, 0.3f);
            GameObject bowR = Box("BowR", visual.transform, new Vector3(0.055f, 0.92f, 0.185f), new Vector3(0.09f, 0.05f, 0.035f), blackMat);
            bowR.transform.localRotation = Quaternion.Euler(-8f, 0f, -0.3f);
            Box("BowKnot", visual.transform, new Vector3(0f, 0.92f, 0.19f), new Vector3(0.05f, 0.05f, 0.04f), blackMat);

            head = gfHead.transform;
            return root;
        }

        /// <summary>领结：两片斜翼 + 中心结，贴在躯干正面上缘。</summary>
        private static void BowTie(Transform parent, float y, Material mat)
        {
            GameObject wingL = Box("BowL", parent, new Vector3(-0.055f, y, 0.235f), new Vector3(0.09f, 0.05f, 0.035f), mat);
            wingL.transform.localRotation = Quaternion.Euler(0f, 0f, 0.3f);
            GameObject wingR = Box("BowR", parent, new Vector3(0.055f, y, 0.235f), new Vector3(0.09f, 0.05f, 0.035f), mat);
            wingR.transform.localRotation = Quaternion.Euler(0f, 0f, -0.3f);
            Box("BowKnot", parent, new Vector3(0f, y, 0.24f), new Vector3(0.05f, 0.05f, 0.04f), mat);
        }

        private static Material MetalMat()
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.55f, 0.57f, 0.6f);
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Glossiness", 0.65f);
            return mat;
        }

        internal static Material StandardMat(Color color, float smoothness)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        private static Material EyeMat()
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.05f, 0.05f, 0.05f);
            mat.SetFloat("_Glossiness", 0.1f);
            // 预启用自发光并置黑：确保 _EMISSION 变体在创建时就编译好，
            // 运行时 SetEyesGlowing 只改颜色就能立刻点亮（否则断电跳杀时眼睛可能不亮）
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }
    }
}
