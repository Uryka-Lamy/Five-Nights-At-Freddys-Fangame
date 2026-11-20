using UnityEngine;
using FNAF.Core;
using FNAF.Data;

namespace FNAF.Enemies
{
    /// <summary>
    /// 奇卡（黄鸡）：东侧路线推进，途经洗手间和厨房（厨房仅音频，监控看不到她），
    /// 最终出现在右门。
    /// </summary>
    public class ChicaAI : AnimatronicAI
    {
        protected override void ApplyNightConfig(NightConfig config) => aiLevel = config.ChicaAI;

        // 原版第一夜奇卡 3AM 左右才开始行动，逐夜提前
        protected override int BaseStartHour => 3;

        protected override void ResetState()
        {
            base.ResetState();
            CurrentRoomId = RoomIds.ShowStage;
            MoveToRoom(RoomIds.ShowStage, 2, true); // 舞台站位错开：弗雷迪0/邦尼1/奇卡2（force：初始站位允许多具玩偶同房）
            GameEvents.RaiseKitchenNoised(false); // 夜开始：锅碗声归零
        }

        protected override bool TryMove()
        {
            string next = NextRoomOnEastRoute();
            if (next == null) return false;
            string before = CurrentRoomId;
            MoveToRoom(next);
            // 真的换房后才更新锅碗声（被房间互斥挡下时保持原状态，避免"人在厨房声音却停了"之类的错报）
            if (CurrentRoomId != before)
                GameEvents.RaiseKitchenNoised(CurrentRoomId == RoomIds.Kitchen);
            return true;
        }

        private string NextRoomOnEastRoute()
        {
            switch (CurrentRoomId)
            {
                case RoomIds.ShowStage:
                    return RoomIds.DiningArea;
                case RoomIds.DiningArea:
                    float roll = Random.value;
                    if (roll < 0.35f) return RoomIds.Restrooms;
                    if (roll < 0.7f) return RoomIds.Kitchen;
                    return RoomIds.EastHall;
                case RoomIds.Restrooms:
                    return RoomIds.EastHall;
                case RoomIds.Kitchen:
                    return RoomIds.EastHall;
                case RoomIds.EastHall:
                    return Random.value < 0.3f ? RoomIds.Kitchen : RoomIds.EastHallCorner;
                case RoomIds.EastHallCorner:
                    return RoomIds.RightDoor;
                default:
                    return RoomIds.DiningArea;
            }
        }

        protected override void OnRetreatFromDoor()
        {
            GameEvents.RaiseKitchenNoised(false);
            MoveToRoom(RoomIds.DiningArea);
        }
    }
}
