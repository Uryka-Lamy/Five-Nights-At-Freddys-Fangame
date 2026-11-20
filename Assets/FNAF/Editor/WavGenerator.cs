using System.IO;
using UnityEditor;
using UnityEngine;

namespace FNAF.EditorTools
{
    /// <summary>
    /// 程序化音效合成器：不依赖任何外部素材，用代码生成全部兜底 .wav 音效
    /// （呼吸/砸门/熊笑等合成版），写入 Assets/FNAF/Audio；有用户录音时运行时优先取 Audio/Recorded。
    /// </summary>
    public static class WavGenerator
    {
        private const int SampleRate = 44100;
        private const string OutputDir = "Assets/FNAF/Audio";

        public static void GenerateAll()
        {
            if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);

            Save("amb_hum.wav", Hum(2.0f));
            Save("static_loop.wav", StaticNoise(1.0f));
            Save("door_close.wav", DoorSlam(0.4f, 1.0f, 60f));
            Save("door_open.wav", DoorSlam(0.32f, 0.6f, 85f));
            Save("light_click.wav", Click(0.07f));
            Save("monitor_toggle.wav", MonitorToggle(0.18f));
            Save("power_down.wav", PowerDown(1.6f));
            Save("scream.wav", Scream(1.25f));
            Save("chime_6am.wav", Chime(2.6f));
            Save("foxy_run.wav", RunSteps(2.0f));
            Save("girl_laugh.wav", GirlLaugh(1.4f));
            Save("celebrate.wav", Celebrate(0.6f));
            Save("freddy_laugh.wav", FreddyLaugh(1.5f));
            Save("pots_clatter.wav", PotsClatter(3.0f));
            Save("cheer_kids.wav", KidsCheer(1.6f));
            Save("breath_loop.wav", Breath(2.4f));
            Save("window_knock.wav", WindowKnock(1.8f));
            Save("power_out_music.wav", PowerOutMusic(7.44f)); // 31 拍 × 0.24s，循环无缝

            AssetDatabase.Refresh();
            Debug.Log($"[WavGenerator] 已生成 18 个音效到 {OutputDir}");
        }

        // ---------- 合成器 ----------

        private static float[] Hum(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                s[i] = 0.34f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                     + 0.16f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                     + 0.07f * Mathf.Sin(2f * Mathf.PI * 165f * t + 0.5f)
                     + 0.025f * (Random.value * 2f - 1f);
            }
            return FadeEdges(s, 0.05f);
        }

        private static float[] StaticNoise(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++) s[i] = Random.value * 2f - 1f;
            return FadeEdges(s, 0.01f);
        }

        private static float[] DoorSlam(float duration, float volume, float thumpFreq)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float thump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * 16f);
                float clank = (Random.value * 2f - 1f) * Mathf.Exp(-t * 55f) * 0.6f;
                float rattle = (Random.value * 2f - 1f) * Mathf.Exp(-Mathf.Abs(t - 0.12f) * 40f) * 0.3f;
                s[i] = (thump + clank + rattle) * volume;
            }
            return FadeEdges(s, 0.01f);
        }

        private static float[] Click(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                s[i] = (Random.value * 2f - 1f) * Mathf.Exp(-t * 110f) * 0.8f
                     + Mathf.Sin(2f * Mathf.PI * 950f * t) * Mathf.Exp(-t * 70f) * 0.35f;
            }
            return FadeEdges(s, 0.005f);
        }

        private static float[] MonitorToggle(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / duration), 0.6f);
                s[i] = (Random.value * 2f - 1f) * 0.5f * env
                     + Mathf.Sin(2f * Mathf.PI * (300f + 900f * t / duration) * t) * 0.15f * env;
            }
            return s;
        }

        private static float[] PowerDown(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float freq = 170f * Mathf.Pow(38f / 170f, t / duration); // 170Hz → 38Hz 下坠
                phase += freq / SampleRate;
                float env = t < 0.05f ? t / 0.05f : 1f - Mathf.Pow(t / duration, 2f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * phase) * 0.55f
                     + (Random.value * 2f - 1f) * 0.06f) * env;
            }
            return FadeEdges(s, 0.02f);
        }

        /// <summary>女孩笑声（金熊彩蛋）：五声下行的"咯咯"笑，高频 + 颤音 + 二次谐波营造童声。</summary>
        private static float[] GirlLaugh(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float[] starts = { 0f, 0.22f, 0.42f, 0.62f, 0.84f };
            float[] freqs = { 820f, 760f, 700f, 655f, 615f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float v = 0f;
                for (int k = 0; k < starts.Length; k++)
                {
                    float lt = t - starts[k];
                    if (lt < 0f || lt > 0.19f) continue;
                    float vib = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 13f * lt);
                    float f = freqs[k] * vib;
                    v += (Mathf.Sin(2f * Mathf.PI * f * lt) * 0.6f
                        + Mathf.Sin(2f * Mathf.PI * f * 2f * lt) * 0.25f)
                        * Mathf.Exp(-lt * 18f);
                }
                s[i] = v * 0.8f + (Random.value * 2f - 1f) * 0.015f;
            }
            return FadeEdges(s, 0.02f);
        }

        private static float[] Scream(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                // 频率在 380-800Hz 间快速起伏，制造刺耳的机械尖叫感
                float freq = 380f + 420f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 2.6f * t));
                phase += freq / SampleRate;
                float saw = Mathf.Sin(2f * Mathf.PI * phase);
                float body = (float)System.Math.Tanh(2.8f * saw) * 0.75f + (Random.value * 2f - 1f) * 0.22f;
                float attack = Mathf.Clamp01(t / 0.02f);
                float release = Mathf.Clamp01((duration - t) / 0.15f);
                s[i] = body * attack * release;
            }
            return s;
        }

        private static float[] Chime(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            // C5-E5-G5-C6 大调琶音钟声（6AM 仪式感）
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float[] onsets = { 0f, 0.45f, 0.9f, 1.35f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float sum = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    if (t < onsets[k]) continue;
                    float lt = t - onsets[k];
                    float decay = Mathf.Exp(-lt * 2.1f);
                    sum += (Mathf.Sin(2f * Mathf.PI * notes[k] * lt)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2f * lt)
                          + 0.15f * Mathf.Sin(2f * Mathf.PI * notes[k] * 3f * lt)) * decay;
                }
                s[i] = sum * 0.3f;
            }
            return FadeEdges(s, 0.05f);
        }

        /// <summary>点鼻子彩蛋音效：单声喇叭"叭——"（双音齐鸣+奇次谐波+tanh 饱和，一声 0.6s）。</summary>
        private static float[] Celebrate(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            // 单声喇叭"叭——"：双音齐鸣（汽车喇叭式大三度）+ 奇次谐波 + 饱和
            const float f1 = 880f;     // A5（比初版高八度，更尖锐）
            const float f2 = 1108.73f; // C#6
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                // 吹起 0.03s、吹住保持、收音 0.06s
                float env = Mathf.Min(t / 0.03f, 1f) * Mathf.Min((duration - t) / 0.06f, 1f);
                float p1 = 2f * Mathf.PI * f1 * t;
                float p2 = 2f * Mathf.PI * f2 * t;
                // 两个音各自的基频+3/5/7 次谐波 = 铜管质感
                float v = Mathf.Sin(p1) + Mathf.Sin(p2)
                        + (Mathf.Sin(3f * p1) + Mathf.Sin(3f * p2)) / 3f
                        + (Mathf.Sin(5f * p1) + Mathf.Sin(5f * p2)) / 5f
                        + (Mathf.Sin(7f * p1) + Mathf.Sin(7f * p2)) / 8f; // 7次谐波提亮度
                // tanh 饱和 = 喇叭"噗"的失真感（注意 Unity Mathf 无 Tanh，用 System.Math）
                s[i] = (float)System.Math.Tanh(v * 1.2) * 0.45f * env;
            }
            return FadeEdges(s, 0.01f);
        }

        private static float[] RunSteps(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            const float stepInterval = 0.2f; // 5 步/秒的冲刺节奏（与跑步颠簸频率一致）
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float ph = t % stepInterval;
                float thump = Mathf.Sin(2f * Mathf.PI * 78f * ph) * Mathf.Exp(-ph * 34f); // 落脚闷响
                float scuff = (Random.value * 2f - 1f) * Mathf.Exp(-ph * 90f) * 0.25f;    // 鞋底擦地
                int stepIndex = Mathf.FloorToInt(t / stepInterval);
                float accent = stepIndex % 2 == 0 ? 1f : 0.82f; // 左右脚轻重交替
                s[i] = (thump * 0.9f + scuff) * accent;
            }
            return FadeEdges(s, 0.05f);
        }

        /// <summary>弗雷迪移动深笑：两声浑厚的"哈……哈……"，基频更低、胸腔共鸣更重。</summary>
        private static float[] FreddyLaugh(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float[] starts = { 0f, 0.6f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float v = 0f;
                for (int k = 0; k < starts.Length; k++)
                {
                    float lt = t - starts[k];
                    if (lt < 0f || lt > 0.5f) continue;
                    float env = Mathf.Sin(Mathf.PI * lt / 0.5f);
                    float f = 85f - k * 8f; // 第二声更低
                    float tone = Mathf.Sin(2f * Mathf.PI * f * lt)
                               + Mathf.Sin(2f * Mathf.PI * f * 2f * lt) * 0.6f
                               + Mathf.Sin(2f * Mathf.PI * f * 3f * lt) * 0.35f
                               + Mathf.Sin(2f * Mathf.PI * f * 4f * lt) * 0.2f;
                    v += tone * env * 0.42f;
                }
                s[i] = v;
            }
            return FadeEdges(s, 0.06f);
        }

        /// <summary>厨房锅碗瓢盆（循环）：随机金属敲击串，清脆 clank 叠低频闷响（CAM6 的"听声辨位"）。</summary>
        private static float[] PotsClatter(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float tPos = 0.1f;
            while (tPos < duration - 0.3f)
            {
                int begin = Mathf.FloorToInt(tPos * SampleRate);
                float baseFreq = Random.Range(420f, 1500f); // 每次敲的锅不一样
                int len = Mathf.FloorToInt(0.22f * SampleRate);
                for (int i = 0; i < len && begin + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float ring = (Mathf.Sin(2f * Mathf.PI * baseFreq * t)
                                + Mathf.Sin(2f * Mathf.PI * baseFreq * 1.51f * t) * 0.6f
                                + Mathf.Sin(2f * Mathf.PI * baseFreq * 2.34f * t) * 0.4f)
                                * Mathf.Exp(-t * 22f) * 0.4f; // 非整数比泛音 = 金属铿锵
                    float thud = Mathf.Sin(2f * Mathf.PI * 130f * t) * Mathf.Exp(-t * 40f) * 0.3f;
                    s[begin + i] += ring + thud;
                }
                tPos += Random.Range(0.25f, 0.7f); // 随机节奏
            }
            return FadeEdges(s, 0.1f);
        }

        /// <summary>6AM 小孩欢呼："耶~"群声上扬，多路合唱正弦+上滑音+宽带噪声掌声。</summary>
        private static float[] KidsCheer(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float[] voices = { 0f, 0.06f, 0.12f, 0.2f, 0.27f, 0.35f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float v = 0f;
                for (int k = 0; k < voices.Length; k++)
                {
                    float lt = t - voices[k];
                    if (lt < 0f || lt > 1.2f) continue;
                    float env = Mathf.Clamp01(lt / 0.08f) * Mathf.Exp(-lt * 3.2f);
                    // 音高上滑（"耶~"），每人起点略不同
                    float f = (300f + k * 45f) * (1f + 0.45f * Mathf.Clamp01(lt / 0.5f));
                    v += Mathf.Sin(2f * Mathf.PI * f * lt) * env * 0.22f;
                }
                // 尾部拍手噪声
                float clap = t > 0.5f ? (Random.value * 2f - 1f) * Mathf.Exp(-(t - 0.5f) * 2.5f) * 0.1f : 0f;
                s[i] = v + clap;
            }
            return FadeEdges(s, 0.05f);
        }

        /// <summary>玩偶贴窗呼吸声（循环）：两声缓慢的机械呼气，低频噪声+胸腔低通，尾音拖长。</summary>
        private static float[] Breath(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float[] starts = { 0.1f, 1.3f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float v = 0f;
                for (int k = 0; k < starts.Length; k++)
                {
                    float lt = t - starts[k];
                    if (lt < 0f || lt > 1.0f) continue;
                    // 呼气包络：快起慢落，中段最响
                    float env = Mathf.Sin(Mathf.PI * lt / 1.0f);
                    env *= env;
                    // 低频噪声 = 肺腔闷响；叠 90Hz 哼振 = 机械喉管
                    float noise = (Random.value * 2f - 1f);
                    float lp = noise * 0.35f + Mathf.Sin(2f * Mathf.PI * 90f * lt) * 0.4f;
                    v += lp * env * 0.5f;
                }
                s[i] = v;
            }
            // 一阶低通让"呼"更闷
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                acc += 0.18f * (s[i] - acc);
                s[i] = acc;
            }
            return FadeEdges(s, 0.08f);
        }

        /// <summary>狐狸砸门（循环）：四连"咚、咚咚"重拳砸门，低频拳击+门板震颤。</summary>
        private static float[] WindowKnock(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            float[] hits = { 0f, 0.55f, 0.75f, 1.05f };
            foreach (float start in hits)
            {
                int begin = Mathf.FloorToInt(start * SampleRate);
                int len = Mathf.FloorToInt(0.3f * SampleRate);
                for (int i = 0; i < len && begin + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float punch = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 30f) * 0.9f; // 重拳闷响
                    float wood = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.35f;          // 门板震颤
                    s[begin + i] += punch + wood;
                }
            }
            return FadeEdges(s, 0.06f);
        }

        /// <summary>停电神曲（循环）：斗牛士进行曲风格的音乐盒旋律——断电后黑暗中飘出的八音盒声。</summary>
        private static float[] PowerOutMusic(float duration)
        {
            int n = Samples(duration);
            float[] s = new float[n];
            const float beat = 0.24f; // 每拍 0.24 秒（进行曲速度）
            // A 大调斗牛士进行曲风格主旋律（音高 Hz, 拍数）——"哒哒哒-哒哒-"的进行曲气质
            (float f, float b)[] melody =
            {
                (440f, 1f), (440f, 1f), (440f, 2f),                    // A4 A4 A4-
                (493.88f, 1f), (554.37f, 1f), (554.37f, 2f),           // B4 C#5 C#5-
                (440f, 1f), (493.88f, 1f), (554.37f, 1f), (587.33f, 1f), // A4 B4 C#5 D5
                (659.25f, 4f),                                          // E5----
                (659.25f, 1f), (739.99f, 1f), (659.25f, 1f), (587.33f, 1f), // E5 F#5 E5 D5
                (554.37f, 2f), (493.88f, 2f),                           // C#5- B4-
                (554.37f, 1f), (587.33f, 1f), (659.25f, 1f), (739.99f, 1f), // C#5 D5 E5 F#5
                (880f, 4f),                                             // A5----
            };
            // 低音：每小节根音"蹦-嚓"（A-E 交替），给进行曲骨架
            (float f, float b)[] bass = { (220f, 2f), (164.81f, 2f) };
            float totalBeats = 0f;
            foreach ((float _, float b) in melody) totalBeats += b;
            float loopLen = totalBeats * beat; // 实际循环长度（与 duration 对齐用）
            int bassIdx = 0;
            float bassPos = 0f;
            // 渲染主旋律（音乐盒音色：正弦+泛音+快衰减）
            float pos = 0f;
            foreach ((float freq, float beats) in melody)
            {
                float len = beats * beat;
                int begin = Mathf.FloorToInt(pos / loopLen * n);
                int lenS = Mathf.FloorToInt(len / loopLen * n);
                for (int i = 0; i < lenS && begin + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = 2f * Mathf.PI * freq * t;
                    // 八音盒：敲击感起音 + 指数衰减 + 高次泛音清脆
                    float env = Mathf.Min(t / 0.006f, 1f) * Mathf.Exp(-t * 3.2f);
                    float tone = Mathf.Sin(p) + 0.42f * Mathf.Sin(2f * p) + 0.18f * Mathf.Sin(3f * p) + 0.08f * Mathf.Sin(4.02f * p);
                    s[begin + i] += tone * env * 0.4f;
                }
                pos += len;
            }
            // 渲染低音（短促"咚"）
            while (bassPos < loopLen - 0.01f)
            {
                (float f, float b) note = bass[bassIdx % bass.Length];
                bassIdx++;
                int begin = Mathf.FloorToInt(bassPos / loopLen * n);
                int lenS = Mathf.FloorToInt(note.b * beat / loopLen * n);
                for (int i = 0; i < lenS && begin + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float thump = (Mathf.Sin(2f * Mathf.PI * note.f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * note.f * 2f * t)) * Mathf.Exp(-t * 4.5f);
                    s[begin + i] += thump * 0.3f;
                }
                bassPos += note.b * beat;
            }
            return s; // 循环音频不做首尾淡出：快衰减设计让尾部自然归零，无缝衔接
        }

        // ---------- WAV 写盘 ----------

        private static int Samples(float duration) => Mathf.CeilToInt(duration * SampleRate);

        private static float[] FadeEdges(float[] s, float fadeSeconds)
        {
            int fade = Mathf.Clamp(Mathf.CeilToInt(fadeSeconds * SampleRate), 1, s.Length / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                s[i] *= k;
                s[s.Length - 1 - i] *= k;
            }
            return s;
        }

        private static void Save(string fileName, float[] samples)
        {
            string path = $"{OutputDir}/{fileName}";
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);        // PCM
                writer.Write((short)1);        // 单声道
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);  // 字节率
                writer.Write((short)2);        // 块对齐
                writer.Write((short)16);       // 位深
                writer.Write("data".ToCharArray());
                writer.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Mathf.Clamp(samples[i], -1f, 1f) * 0.9f;
                    writer.Write((short)(v * 32767f));
                }
            }
        }
    }
}
