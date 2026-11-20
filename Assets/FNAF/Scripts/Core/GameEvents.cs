using System;
using FNAF.Data;

namespace FNAF.Core
{
    /// <summary>
    /// 全局事件总线：各系统之间只通过事件通信，不互相直接引用。
    /// 发事件用 Raise 方法，收事件在 OnEnable 订阅、OnDisable 取消订阅。
    /// </summary>
    public static class GameEvents
    {
        // ---- 夜晚流程 ----
        public static event Action<int> NightStarted;
        public static event Action<int> HourChanged;
        public static event Action<int> NightCompleted;

        // ---- 玩家生死 ----
        /// <summary>玩家被抓，参数为凶手玩偶 ID（"Freddy"/"Bonnie"/"Chica"/"Foxy"/"PowerOutFreddy"）</summary>
        public static event Action<string> PlayerCaught;

        // ---- 电力 ----
        /// <summary>参数：当前电力百分比，当前使用等级 1-5</summary>
        public static event Action<float, int> PowerChanged;
        public static event Action PowerOut;

        // ---- 阶段切换 ----
        /// <summary>从夜晚结算/失败界面回到主菜单：用于掐断钟声、欢呼等跨阶段残留音效。</summary>
        public static event Action MenuReturned;
        /// <summary>断电处刑开始（弗雷迪现身左门、眼睛开始闪烁）：停电神曲从此刻才响起。</summary>
        public static event Action PowerOutExecutionBegan;

        // ---- 办公室交互 ----
        /// <summary>参数：门的一侧，是否关闭</summary>
        public static event Action<DoorSide, bool> DoorToggled;
        /// <summary>参数：门的一侧，门灯是否点亮</summary>
        public static event Action<DoorSide, bool> DoorLightToggled;

        // ---- 监控器（后续会话实现，接口先约定好） ----
        /// <summary>参数：监控器是否打开</summary>
        public static event Action<bool> MonitorToggled;

        // ---- 玩偶移动 ----
        /// <summary>参数：玩偶 ID、出发房间 ID、到达房间 ID。监控画面借此在"你正看着的玩偶离开画面"瞬间切强噪点遮挡。</summary>
        public static event Action<string, string, string> AnimatronicMoved;

        // ---- 氛围还原事件：厨房锅碗 / 弗雷迪移动深笑 / 狐狸砸门 ----

        /// <summary>奇卡进/出厨房：锅碗瓢盆循环声开关（CAM6 仅音频的"听声辨位"）。</summary>
        public static event Action<bool> KitchenNoised;

        /// <summary>弗雷迪完成一次移动：播放标志性深笑声。</summary>
        public static event Action FreddyMoved;

        /// <summary>狐狸冲刺被门挡住后的砸门声（一阵敲门）。参数：是否正在砸门。</summary>
        public static event Action<bool> FoxyBanged;

        /// <summary>狐狸海盗湾蓄力阶段变化（0 关严 / 1 微开 / 2 全开唱歌 / 3 冲刺）。参数：新阶段。</summary>
        public static event Action<int> FoxyStageChanged;

        public static void RaiseKitchenNoised(bool on) => KitchenNoised?.Invoke(on);
        public static void RaiseFreddyMoved() => FreddyMoved?.Invoke();
        public static void RaiseFoxyBanged(bool on) => FoxyBanged?.Invoke(on);
        public static void RaiseFoxyStageChanged(int stage) => FoxyStageChanged?.Invoke(stage);

        public static void RaiseNightStarted(int night) => NightStarted?.Invoke(night);
        public static void RaiseHourChanged(int hour) => HourChanged?.Invoke(hour);
        public static void RaiseNightCompleted(int night) => NightCompleted?.Invoke(night);
        public static void RaisePlayerCaught(string enemyId) => PlayerCaught?.Invoke(enemyId);
        public static void RaisePowerChanged(float power, int usage) => PowerChanged?.Invoke(power, usage);
        public static void RaisePowerOut() => PowerOut?.Invoke();
        public static void RaiseMenuReturned() => MenuReturned?.Invoke();
        public static void RaisePowerOutExecutionBegan() => PowerOutExecutionBegan?.Invoke();
        public static void RaiseDoorToggled(DoorSide side, bool closed) => DoorToggled?.Invoke(side, closed);
        public static void RaiseDoorLightToggled(DoorSide side, bool on) => DoorLightToggled?.Invoke(side, on);
        public static void RaiseMonitorToggled(bool up) => MonitorToggled?.Invoke(up);
        public static void RaiseAnimatronicMoved(string enemyId, string fromRoomId, string toRoomId) => AnimatronicMoved?.Invoke(enemyId, fromRoomId, toRoomId);
    }
}
