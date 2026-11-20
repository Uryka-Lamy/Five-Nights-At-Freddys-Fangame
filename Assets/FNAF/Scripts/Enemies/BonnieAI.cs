using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Enemies
{
    /// <summary>
    /// 邦尼（紫兔）：西侧路线推进，最终出现在左门。
    /// 路线带随机分支（餐厅/后台/杂物间），对应原版邦尼的游走特性。
    /// </summary>
    public class BonnieAI : AnimatronicAI
    {
        protected override void ApplyNightConfig(NightConfig config) => aiLevel = config.BonnieAI;

        // 原版式爬升：兔是唯一 2AM 就 +1 的玩偶（3AM/4AM 基类已覆盖）
        protected override void ApplyHourlyRamp(int hour)
        {
            if (hour == 2 || hour == 3 || hour == 4) aiLevel = Mathf.Min(20, aiLevel + 1);
        }

        // 原版第一夜邦尼 2AM 左右才开始行动，逐夜提前
        protected override int BaseStartHour => 2;

        protected override void ResetState()
        {
            base.ResetState();
            CurrentRoomId = RoomIds.ShowStage;
            MoveToRoom(RoomIds.ShowStage, 1, true); // 舞台站位错开：弗雷迪0/邦尼1/奇卡2（force：初始站位允许多具玩偶同房）
        }

        protected override bool TryMove()
        {
            string next = NextRoomOnWestRoute();
            if (next == null) return false;
            MoveToRoom(next);
            return true;
        }

        private string NextRoomOnWestRoute()
        {
            switch (CurrentRoomId)
            {
                case RoomIds.ShowStage:
                    return Random.value < 0.5f ? RoomIds.DiningArea : RoomIds.Backstage;
                case RoomIds.Backstage:
                    return Random.value < 0.5f ? RoomIds.DiningArea : RoomIds.WestHall;
                case RoomIds.DiningArea:
                    return RoomIds.WestHall;
                case RoomIds.WestHall:
                    return Random.value < 0.4f ? RoomIds.SupplyCloset : RoomIds.WestHallCorner;
                case RoomIds.SupplyCloset:
                    return RoomIds.WestHallCorner;
                case RoomIds.WestHallCorner:
                    return RoomIds.LeftDoor;
                default:
                    return RoomIds.DiningArea;
            }
        }

        protected override void OnRetreatFromDoor()
        {
            // 被门挡住后退回餐厅继续游走
            MoveToRoom(RoomIds.DiningArea);
        }
    }
}
