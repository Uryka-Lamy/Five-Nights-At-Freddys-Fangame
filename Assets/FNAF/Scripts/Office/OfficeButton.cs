using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Office
{
    /// <summary>
    /// 办公室墙上可点击的实体按钮（门/门灯）：挂在带碰撞体的小方块上，
    /// 玩家用鼠标操作触发对应 DoorController 动作——复刻原版"看过去再点墙"的操作。
    /// 门：点击切换开关；灯：按住点亮、松手熄灭（同原版手感）。
    /// </summary>
    public class OfficeButton : MonoBehaviour
    {
        public enum ButtonAction
        {
            Door = 0,
            Light = 1
        }

        [Tooltip("按钮控制的门控制器")]
        [SerializeField] private DoorController door;

        [Tooltip("按钮功能：关门/开门 或 门灯开关")]
        [SerializeField] private ButtonAction action = ButtonAction.Door;

        private bool lightHeld;

        /// <summary>只在游戏中响应：主菜单/结算阶段办公室还渲染在背后，点按钮位置不能误触游戏操作。</summary>
        private static bool InGame =>
            GameManager.Instance != null && GameManager.Instance.Phase == GameManager.GamePhase.Playing;

        private void OnMouseDown()
        {
            if (!InGame) return;
            if (door == null) return;
            if (action == ButtonAction.Door) door.ToggleDoor();
            else
            {
                lightHeld = true;
                door.SetLight(true);
            }
        }

        private void OnMouseUp()
        {
            ReleaseLight();
        }

        private void OnMouseEnter()
        {
            // 按住期间滑出又滑回：鼠标键仍按着则续亮
            if (!InGame) return;
            if (lightHeld && Input.GetMouseButton(0)) door?.SetLight(true);
        }

        private void OnMouseExit()
        {
            // 按住灯时滑出按钮区域即视为松手，避免灯卡在常亮
            if (!InGame) return;
            if (lightHeld) ReleaseLight();
        }

        private void OnDisable()
        {
            ReleaseLight();
        }

        private void ReleaseLight()
        {
            if (!lightHeld) return;
            lightHeld = false;
            if (door != null) door.SetLight(false);
        }
    }
}
