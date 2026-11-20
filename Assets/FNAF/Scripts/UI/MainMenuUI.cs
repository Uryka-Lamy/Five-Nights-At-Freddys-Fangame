using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;

namespace FNAF.UI
{
    /// <summary>
    /// 主菜单与夜晚流程控制：菜单面板（按存档解锁状态点亮夜晚按钮）、
    /// 夜晚片头（"第 X 夜"黑屏 2.2 秒）、开始夜晚（通知 GameManager）。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        [Header("面板")]
        [Tooltip("主菜单面板")]
        [SerializeField] private GameObject menuPanel;

        [Tooltip("夜晚片头黑屏面板")]
        [SerializeField] private GameObject introPanel;

        [Tooltip("片头文字（第 X 夜）")]
        [SerializeField] private Text introText;

        [Tooltip("游戏内 HUD 面板")]
        [SerializeField] private GameObject hudPanel;

        [Header("FNAF 风格元素")]
        [Tooltip("全屏雪花噪点 RawImage")]
        [SerializeField] private RawImage staticImage;

        [Tooltip("菜单雪花氛围音（static_loop 循环）")]
        [SerializeField] private AudioSource staticAudio;

        [Header("评星（标题下的白色五角星，SaveSystem.GetStars 填充）")]
        [SerializeField] private Text menuStarsText;

        [Tooltip("自定义夜入口按钮（通关第 5 夜后才显示）")]
        [SerializeField] private GameObject customNightButton;

        [Tooltip("查看玩偶入口按钮（通关第 5 夜后才显示）")]
        [SerializeField] private GameObject viewAnimatronicsButton;

        [Tooltip("玩偶图鉴面板控制器")]
        [SerializeField] private AnimatronicViewerUI animatronicViewer;

        [Tooltip("自定义夜面板控制器")]
        [SerializeField] private CustomNightUI customNight;

        [Header("雪花噪点参数")]
        [Tooltip("噪点纹理宽（像素，点采样放大出 CRT 颗粒感）")]
        [SerializeField] private int staticWidth = 160;
        [Tooltip("噪点纹理高")]
        [SerializeField] private int staticHeight = 90;
        [Tooltip("噪点刷新率（帧/秒）")]
        [SerializeField] private float staticFps = 12f;

        [Tooltip("第 1-6 夜按钮（按顺序）")]
        [SerializeField] private Button[] nightButtons;

        private Texture2D staticTex;
        private Color32[] staticPixels;
        private float staticTimer;

        private void Awake()
        {
            Instance = this;
            CreateStaticTexture();
        }

        private void Start()
        {
            ShowMenu();
        }

        private void Update()
        {
            if (menuPanel == null || !menuPanel.activeSelf) return;

            // 雪花噪点按低帧率刷新（老式监视器的闪烁感）
            staticTimer -= Time.unscaledDeltaTime;
            if (staticTimer <= 0f)
            {
                staticTimer = 1f / Mathf.Max(1f, staticFps);
                RefreshStatic();
            }
        }

        public void ShowMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(true);
            if (introPanel != null) introPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (staticAudio != null && staticAudio.clip != null && !staticAudio.isPlaying)
                staticAudio.Play();
            RefreshNightButtons();
            RefreshStars();
        }

        /// <summary>刷新标题下的评星（通第 5 夜、通第 6 夜、自定义夜全 20 各得一颗白色五角星）。</summary>
        private void RefreshStars()
        {
            if (menuStarsText != null) menuStarsText.text = new string('★', SaveSystem.GetStars());
        }

        /// <summary>按钮持久化监听入口：从菜单开始第 night 夜。</summary>
        public void StartNight(int night)
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (introPanel != null) introPanel.SetActive(true);
            if (staticAudio != null && staticAudio.isPlaying) staticAudio.Stop();
            if (introText != null) introText.text = $"第 {night} 夜"; // 自定义夜=第 7 夜（原版同款显示）
            StartCoroutine(StartNightAfterIntro(night));
        }

        private IEnumerator StartNightAfterIntro(int night)
        {
            yield return new WaitForSeconds(2.2f);
            if (introPanel != null) introPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(true);
            if (GameManager.Instance != null) GameManager.Instance.StartNight(night);
        }

        /// <summary>金熊 1987 彩蛋：隐藏全部菜单视觉（雪花是菜单面板子物体，一并隐藏），让 3D 跳杀直出画面。</summary>
        public void HideForGoldenCrash()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (introPanel != null) introPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (staticAudio != null && staticAudio.isPlaying) staticAudio.Stop();
        }

        // ---------- FNAF 风格菜单项 ----------

        /// <summary>新游戏：从第 1 夜开始（不清存档）。</summary>
        public void OnNewGame() => StartNight(1);

        /// <summary>继续：从存档最高解锁夜开始。</summary>
        public void OnContinue() => StartNight(SaveSystem.GetHighestUnlockedNight());

        /// <summary>自定义夜：打开 AI 等级设置面板。</summary>
        public void OnCustomNight()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (customNight != null) customNight.Show();
        }

        /// <summary>查看玩偶：打开玩偶图鉴面板。</summary>
        public void OnViewAnimatronics()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (animatronicViewer != null) animatronicViewer.Show();
        }

        // ---------- 雪花噪点 ----------

        private void CreateStaticTexture()
        {
            staticTex = new Texture2D(staticWidth, staticHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            staticPixels = new Color32[staticWidth * staticHeight];
            if (staticImage != null) staticImage.texture = staticTex;
            RefreshStatic();
        }

        private void RefreshStatic()
        {
            if (staticTex == null) return;
            for (int i = 0; i < staticPixels.Length; i++)
            {
                // 深灰底 + 少量亮斑：还原老式监视器的雪花质感
                byte v = (byte)Random.Range(8, 64);
                if (Random.value < 0.035f) v = (byte)Random.Range(130, 215);
                staticPixels[i] = new Color32(v, v, v, 255);
            }
            staticTex.SetPixels32(staticPixels);
            staticTex.Apply(false);
        }

        public void ResetProgress()
        {
            SaveSystem.ResetProgress();
            RefreshNightButtons();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RefreshNightButtons()
        {
            if (nightButtons == null) return;
            for (int i = 0; i < nightButtons.Length; i++)
            {
                if (nightButtons[i] != null)
                    nightButtons[i].interactable = SaveSystem.IsNightUnlocked(i + 1);
            }
            // 自定义夜/查看玩偶入口：通关第 5 夜后才会出现（原版同款）
            if (customNightButton != null)
                customNightButton.SetActive(SaveSystem.IsCustomNightUnlocked());
            if (viewAnimatronicsButton != null)
                viewAnimatronicsButton.SetActive(SaveSystem.IsCustomNightUnlocked());
        }
    }
}
