using System;
using UnityEngine;

namespace FNAF.Data
{
    /// <summary>门/门灯的左右侧标识。放在数据层供核心事件与办公室系统共用。</summary>
    public enum DoorSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// 房间定义：监控系统的摄像头机位、敌人 AI 的站位与移动图，都以这份房间数据为准。
    /// 房间 ID 是全局契约，场景生成器、监控器、敌人 AI 必须使用同一套 ID：
    /// ShowStage(表演舞台) / DiningArea(餐厅) / Backstage(后台) / PirateCove(海盗湾) /
    /// WestHall(西走廊) / WestHallCorner(西走廊角落) / SupplyCloset(杂物间) /
    /// EastHall(东走廊) / EastHallCorner(东走廊角落) / Restrooms(洗手间) / Kitchen(厨房，仅音频)
    /// </summary>
    [Serializable]
    public class RoomDefinition
    {
        [Tooltip("房间唯一 ID（英文，见类注释中的契约列表）")]
        public string RoomId;

        [Tooltip("监控地图上显示的中文名")]
        public string DisplayName;

        [Tooltip("该房间监控摄像头的机位（场景生成器填充）")]
        public Transform CameraAnchor;

        [Tooltip("玩偶在这个房间的可见站位（场景生成器填充）")]
        public Transform[] EnemyStops;

        [Tooltip("邻接房间 ID 列表，构成敌人 AI 的移动图")]
        public string[] AdjacentRoomIds;

        [Tooltip("仅音频房间（如厨房）：监控里听得到但看不到画面")]
        public bool AudioOnly;

        [Tooltip("该房间摄像头是否左右缓慢往复旋转（走廊式机位）；杂物间/海盗湾为固定机位")]
        public bool CameraPans = true;

        [Tooltip("往复旋转最小偏角（度，负=向左）")]
        public float CameraPanMin = -12f;

        [Tooltip("往复旋转最大偏角（度，正=向右）")]
        public float CameraPanMax = 12f;

        [Tooltip("是否出现在监控地图按钮上（门口虚拟房间为 false，仅供敌人 AI 使用）")]
        public bool ShowOnMonitor = true;
    }
}
