using UnityEngine;
using FNAF.Data;

namespace FNAF.Core
{
    /// <summary>
    /// 电力管理：FNAF 1 的核心资源压力。
    /// 使用等级 = 1（基础）+ 关闭的门数 + 点亮的门灯数 + 监控器是否打开（1-5 格）。
    /// 耗电速率与使用等级成正比；第 2 夜起另有一笔与用电格数无关的逐夜附加耗电；
    /// 电力归零后触发全局断电事件。
    /// </summary>
    public class PowerManager : MonoBehaviour
    {
        public float Power { get; private set; }
        public int Usage { get; private set; } = 1;
        public bool IsPowerOut { get; private set; }

        private float baseDrainPerSecond = 0.104f;
        private float extraDrainPerSecond;
        private int closedDoorCount;
        private int litLightCount;
        private bool monitorUp;
        private bool running;

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.DoorToggled += OnDoorToggled;
            GameEvents.DoorLightToggled += OnDoorLightToggled;
            GameEvents.MonitorToggled += OnMonitorToggled;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.DoorToggled -= OnDoorToggled;
            GameEvents.DoorLightToggled -= OnDoorLightToggled;
            GameEvents.MonitorToggled -= OnMonitorToggled;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnPlayerCaught(string enemyId) => running = false;
        private void OnNightCompleted(int night) => running = false; // 胜利面板停留期间不再耗电（防断电处刑抢屏）
        private void OnMenuReturned() => running = false; // ESC 退出本夜：菜单下电力停表

        private void OnNightStarted(int night)
        {
            NightConfig config = GameManager.Instance != null
                ? GameManager.Instance.GetNightConfig(night)
                : null;

            if (config == null)
            {
                Debug.LogWarning("[PowerManager] 拿不到夜晚配置，电力系统不启动");
                running = false;
                return;
            }

            baseDrainPerSecond = Mathf.Max(0.001f, config.BaseDrainPerSecond);
            // 原版机制：随夜数加快的恒定附加耗电（每 N 秒多扣 0.1%，与用电格数无关）
            extraDrainPerSecond = config.NightlyDrainIntervalSeconds > 0f
                ? 0.1f / config.NightlyDrainIntervalSeconds
                : 0f;
            Power = config.StartingPower;
            closedDoorCount = 0;
            litLightCount = 0;
            monitorUp = false;
            IsPowerOut = false;
            running = true;
            RecalculateUsage();
            GameEvents.RaisePowerChanged(Power, Usage);
        }

        private void OnDoorToggled(DoorSide side, bool closed)
        {
            if (!running) return;
            closedDoorCount = Mathf.Max(0, closedDoorCount + (closed ? 1 : -1));
            RecalculateUsage();
        }

        private void OnDoorLightToggled(DoorSide side, bool on)
        {
            if (!running) return;
            litLightCount = Mathf.Max(0, litLightCount + (on ? 1 : -1));
            RecalculateUsage();
        }

        private void OnMonitorToggled(bool up)
        {
            if (!running) return;
            monitorUp = up;
            RecalculateUsage();
        }

        private void RecalculateUsage()
        {
            Usage = 1 + closedDoorCount + litLightCount + (monitorUp ? 1 : 0);
            GameEvents.RaisePowerChanged(Power, Usage);
        }

        private void Update()
        {
            if (!running || IsPowerOut) return;

            Power -= (baseDrainPerSecond * Usage + extraDrainPerSecond) * Time.deltaTime;

            if (Power <= 0f)
            {
                Power = 0f;
                IsPowerOut = true;
                running = false;
                GameEvents.RaisePowerOut();
                GameEvents.RaisePowerChanged(Power, Usage);
                return;
            }

            GameEvents.RaisePowerChanged(Power, Usage);
        }
    }
}
