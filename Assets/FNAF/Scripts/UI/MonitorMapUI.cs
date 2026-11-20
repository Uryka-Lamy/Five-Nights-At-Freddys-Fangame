using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FNAF.UI
{
    /// <summary>
    /// 监控地图按钮注册表：CameraFeedController 切换房间时高亮对应的 CAM 按钮
    /// （FNAF 1 式：当前选中的摄像头亮绿色，其余保持暗色）。
    /// </summary>
    public class MonitorMapUI : MonoBehaviour
    {
        [Serializable]
        public class CamEntry
        {
            public string RoomId;
            public Image Background;
        }

        [SerializeField] private List<CamEntry> entries = new List<CamEntry>();

        private static readonly Color IdleColor = new Color(0.14f, 0.18f, 0.24f, 0.95f);
        private static readonly Color ActiveColor = new Color(0.42f, 0.85f, 0.25f, 0.98f);

        /// <summary>构建期注册（UIBuilder 调用）：房间 ID 与对应按钮背景。</summary>
        public void Register(string roomId, Image background)
        {
            entries.Add(new CamEntry { RoomId = roomId, Background = background });
        }

        /// <summary>高亮当前选中房间的按钮（CameraFeedController 切房时调用）。</summary>
        public void Highlight(string roomId)
        {
            foreach (CamEntry entry in entries)
            {
                if (entry.Background == null) continue;
                entry.Background.color = entry.RoomId == roomId ? ActiveColor : IdleColor;
            }
        }
    }
}
