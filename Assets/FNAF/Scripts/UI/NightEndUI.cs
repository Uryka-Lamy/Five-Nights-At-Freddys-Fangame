using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FNAF.Core;

namespace FNAF.UI
{
    /// <summary>
    /// 夜晚结算：6AM 胜利面板（延迟浮现）与被抓 GAME OVER 面板。
    /// 被抓流程：跳杀演出（约 1.2s）转黑后弹出 GAME OVER，展示 3 秒自动返回主菜单；
    /// 面板上的"重试本夜/返回主菜单"按钮仍可用，点击即取消自动跳转。
    /// </summary>
    public class NightEndUI : MonoBehaviour
    {
        [Header("面板（场景生成器填充）")]
        [Tooltip("6AM 胜利面板")]
        [SerializeField] private GameObject victoryPanel;

        [Tooltip("胜利副标题文字（第 X 夜 · 完成）")]
        [SerializeField] private Text victorySubText;

        [Tooltip("GAME OVER 面板")]
        [SerializeField] private GameObject gameOverPanel;

        [Tooltip("第 5 夜通关的支票画面（原版同款：$120.50 周薪支票，按任意键返回主菜单）")]
        [SerializeField] private GameObject paycheckPanel;

        [Tooltip("跳杀黑幕对象（结算结束前必须关闭，否则黑幕会盖住主菜单）")]
        [SerializeField] private GameObject jumpscareBlack;

        [Tooltip("GAME OVER 展示多少秒后自动返回主菜单")]
        [SerializeField] private float gameOverAutoReturnSeconds = 3f;

        private void OnEnable()
        {
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
        }

        private void OnDisable()
        {
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
        }

        private void OnNightCompleted(int night)
        {
            if (victorySubText != null)
                victorySubText.text = $"第 {night} 夜 · 完成！"; // 自定义夜=第 7 夜，同款显示

            // 原版彩蛋：第 5 夜通关后发工资（$120.50 支票画面），按任意键回主菜单
            if (night == 5)
                StartCoroutine(ShowAfterDelay(paycheckPanel, 0.8f));
            else
                StartCoroutine(ShowAfterDelay(victoryPanel, 0.8f));
        }

        private void Update()
        {
            // 支票画面：按任意键返回主菜单（"继续游玩/新游戏"都在主菜单选择）
            if (paycheckPanel != null && paycheckPanel.activeSelf && Input.anyKeyDown)
                HideAll();
        }

        private void OnPlayerCaught(string enemyId)
        {
            // 等跳杀演出（约 1.2s）收尾转黑后再弹 GAME OVER
            StartCoroutine(ShowAfterDelay(gameOverPanel, 1.6f));
            // 展示 3 秒后自动返回主菜单（期间点按钮则取消）
            StartCoroutine(AutoReturnToMenu(1.6f + gameOverAutoReturnSeconds));
        }

        private IEnumerator ShowAfterDelay(GameObject panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel != null) panel.SetActive(true);
        }

        private IEnumerator AutoReturnToMenu(float delay)
        {
            yield return new WaitForSeconds(delay);
            BackToMenu();
        }

        /// <summary>胜利面板"继续"按钮：进入下一夜，第 6 夜通关则回主菜单。</summary>
        public void OnContinueClicked()
        {
            HideAll();
            int next = GameManager.Instance != null ? GameManager.Instance.CurrentNight + 1 : 2;
            if (next > 6 || MainMenuUI.Instance == null)
            {
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.ShowMenu();
                return;
            }
            MainMenuUI.Instance.StartNight(next);
        }

        /// <summary>失败面板"重试本夜"按钮。</summary>
        public void RetryNight()
        {
            HideAll();
            int current = GameManager.Instance != null ? GameManager.Instance.CurrentNight : 1;
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.StartNight(current);
        }

        /// <summary>失败面板"返回主菜单"按钮，也是 3 秒自动跳转的终点。</summary>
        public void BackToMenu()
        {
            HideAll();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.ShowMenu();
        }

        private void HideAll()
        {
            StopAllCoroutines();
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (paycheckPanel != null) paycheckPanel.SetActive(false);
            if (jumpscareBlack != null) jumpscareBlack.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
        }
    }
}
