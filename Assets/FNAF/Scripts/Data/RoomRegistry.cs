using System.Collections.Generic;
using UnityEngine;

namespace FNAF.Data
{
    /// <summary>
    /// 房间注册表：场景中所有房间（监控机位 + 敌人站位 + 邻接图）的唯一数据源。
    /// 监控系统和敌人 AI 只通过 GetById 查询，不各自维护房间列表。
    /// </summary>
    public class RoomRegistry : MonoBehaviour
    {
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

        public IReadOnlyList<RoomDefinition> Rooms => rooms;

        private Dictionary<string, RoomDefinition> byId;

        private void Awake()
        {
            byId = new Dictionary<string, RoomDefinition>();
            foreach (RoomDefinition room in rooms)
            {
                if (string.IsNullOrEmpty(room.RoomId) || byId.ContainsKey(room.RoomId))
                {
                    Debug.LogWarning($"[RoomRegistry] 房间 ID 为空或重复，已跳过: {room.RoomId} ({name})");
                    continue;
                }
                byId.Add(room.RoomId, room);
            }
        }

        public RoomDefinition GetById(string roomId)
        {
            if (!string.IsNullOrEmpty(roomId) && byId.TryGetValue(roomId, out RoomDefinition room))
                return room;
            return null;
        }
    }
}
