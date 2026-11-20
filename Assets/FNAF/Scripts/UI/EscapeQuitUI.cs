using UnityEngine;
using FNAF.Core;

namespace FNAF.UI
{
    /// <summary>
    /// ESC 退出确认：游玩中按 ESC 暂停并弹出确认框（退出后不会保留本夜进度），
    /// "继续游玩"恢复本夜，"退出本夜"放弃进度回主菜单。
    /// 弹出期间 Time.timeScale = 0——时钟与玩偶全部冻结，不会被趁机跳杀。
    /// </summary>
    public class EscapeQuitUI : MonoBehaviour
    {
        [Header("面板（场景生成器填充）")]
        [Tooltip("ESC 确认退出面板")]
        [SerializeField] private GameObject confirmPanel;

        private bool paused;

        private void Update()
        {
            if (GameManager.Instance == null) return;
            // 只在夜晚进行中响应；菜单/结算/跳杀/断电演出阶段一律不弹
            if (GameManager.Instance.Phase != GameManager.GamePhase.Playing) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (paused) Resume();
                else Pause();
            }
        }

        private void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        /// <summary>"继续游玩"按钮 / 再次按 ESC：取消退出，恢复本夜。</summary>
        public void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        /// <summary>"退出本夜"按钮：放弃本夜进度，返回主菜单。</summary>
        public void QuitToMenu()
        {
            paused = false;
            Time.timeScale = 1f;
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.ShowMenu();
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
        }
    }
}
