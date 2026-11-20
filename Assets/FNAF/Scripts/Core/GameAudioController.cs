using UnityEngine;
using FNAF.Data;
using FNAF.Enemies;

namespace FNAF.Core
{
    /// <summary>
    /// 全局游戏音频：集中持有所有 AudioSource（场景生成器接线并塞入程序化生成的 wav），
    /// 监听事件总线播放对应音效——各玩法系统零音频依赖。
    /// </summary>
    public class GameAudioController : MonoBehaviour
    {
        [Header("音量")]
        [Tooltip("全局音量缩放（整体压低所有音效响度，0.5 约为一半音量）")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.35f;

        [Header("单次音源")]
        [Tooltip("门升降声")]
        [SerializeField] private AudioSource doorSource;

        [Tooltip("门灯咔哒声（按住灯期间循环）")]
        [SerializeField] private AudioSource lightSource;

        [Tooltip("开灯发现门外有玩偶的惊吓音（单次）")]
        [SerializeField] private AudioSource lightSpottedSource;

        [Tooltip("监控器抬起声")]
        [SerializeField] private AudioSource monitorUpSource;

        [Tooltip("监控器放下声")]
        [SerializeField] private AudioSource monitorDownSource;

        [Tooltip("兔/鸡移动的脚步声（每次移动播放）")]
        [SerializeField] private AudioSource stepsSource;

        [Tooltip("断电下坠声")]
        [SerializeField] private AudioSource powerOutSource;

        [Tooltip("6AM 钟声")]
        [SerializeField] private AudioSource chimeSource;

        [Tooltip("跳杀尖叫（跳杀控制器直接引用此源）")]
        [SerializeField] private AudioSource screamSource;

        [Header("循环音源")]
        [Tooltip("办公室环境背景音（循环：夜晚开始后一直播放）")]
        [SerializeField] private AudioSource ambientSource;

        [Tooltip("监控开启期间环境音的衰减系数（0~1：越小环境音被压得越低，放下监控立即恢复）")]
        [SerializeField, Range(0f, 1f)] private float monitorDuckFactor = 0.4f;

        [Tooltip("警报状态（狐狸砸门/狐狸唱歌）期间环境音的衰减系数")]
        [SerializeField, Range(0f, 1f)] private float alertDuckFactor = 0.5f;

        // 环境音闪避状态（任一激活即按对应系数压低环境音，取更低值）
        private float ambientBaseVolume = -1f;
        private bool monitorUpNow, knockNow, songNow;
        private bool leftLightOn, rightLightOn; // 门灯当前是否亮着（供"灯亮时玩偶才到位"补发现音）

        [Header("氛围还原音源（一键生成接线）")]
        [Tooltip("弗雷迪移动深笑声（每次移动播放）")]
        [SerializeField] private AudioSource freddyLaughSource;

        [Tooltip("厨房锅碗瓢盆声（循环：奇卡在厨房时播放）")]
        [SerializeField] private AudioSource potsSource;

        [Tooltip("6AM 小孩欢呼（钟声后播放）")]
        [SerializeField] private AudioSource cheerSource;

        [Tooltip("狐狸砸门声（一阵连环敲门）")]
        [SerializeField] private AudioSource knockSource;

        [Tooltip("狐狸唱歌声（循环：海盗湾帘子全开、即将冲刺时播放）")]
        [SerializeField] private AudioSource foxySongSource;

        [Tooltip("停电神曲（循环：断电后黑暗中飘出的八音盒旋律，直到跳杀或天亮）")]
        [SerializeField] private AudioSource powerOutMusicSource;

        [Tooltip("兔/鸡靠近办公室提示音（走到走廊角落或门口房间时响一声）")]
        [SerializeField] private AudioSource nearOfficeSource;

        [Tooltip("弗雷迪移动笑声变体（每次移动二选一随机播放）")]
        [SerializeField] private AudioClip[] freddyLaughClips;

        [Header("其他")]
        [Tooltip("办公室主灯（断电时熄灭）")]
        [SerializeField] private GameObject officeLightObject;

        private void Awake()
        {
            AudioListener.volume = masterVolume;
        }

        private void OnDestroy()
        {
            // 退出播放时还原，避免把编辑器全局音量也压低
            AudioListener.volume = 1f;
        }

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.PowerOut += OnPowerOut;
            GameEvents.MenuReturned += OnMenuReturned;
            GameEvents.PowerOutExecutionBegan += OnPowerOutExecutionBegan;
            GameEvents.DoorToggled += OnDoorToggled;
            GameEvents.DoorLightToggled += OnDoorLightToggled;
            GameEvents.MonitorToggled += OnMonitorToggled;
            GameEvents.KitchenNoised += OnKitchenNoised;
            GameEvents.FreddyMoved += OnFreddyMoved;
            GameEvents.AnimatronicMoved += OnAnimatronicMoved;
            GameEvents.FoxyBanged += OnFoxyBanged;
            GameEvents.FoxyStageChanged += OnFoxyStageChanged;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.PowerOut -= OnPowerOut;
            GameEvents.MenuReturned -= OnMenuReturned;
            GameEvents.PowerOutExecutionBegan -= OnPowerOutExecutionBegan;
            GameEvents.DoorToggled -= OnDoorToggled;
            GameEvents.DoorLightToggled -= OnDoorLightToggled;
            GameEvents.MonitorToggled -= OnMonitorToggled;
            GameEvents.KitchenNoised -= OnKitchenNoised;
            GameEvents.FreddyMoved -= OnFreddyMoved;
            GameEvents.AnimatronicMoved -= OnAnimatronicMoved;
            GameEvents.FoxyBanged -= OnFoxyBanged;
            GameEvents.FoxyStageChanged -= OnFoxyStageChanged;
        }

        private void OnNightStarted(int night)
        {
            StopCarryoverAudio();
            // 先还原基准音量再开播：上一夜若带着闪避结束（如监控开到 6AM），volume 停在压低值，
            // 直接重捕会把污染值当新基准，环境音从此越来越小
            if (ambientBaseVolume >= 0f && ambientSource != null) ambientSource.volume = ambientBaseVolume;
            Play(ambientSource);
            // 首次记录基准音量（场景里调的值），此后只还原不重捕
            if (ambientBaseVolume < 0f && ambientSource != null) ambientBaseVolume = ambientSource.volume;
        }

        private void OnNightCompleted(int night)
        {
            StopLoops();
            Play(chimeSource);
            // 原版 6AM：钟声后小孩欢呼"耶~"
            Play(cheerSource);
        }

        private void OnPlayerCaught(string enemyId)
        {
            StopLoops();
            // 门灯是按住式循环，不走 StopCarryoverAudio（避免掐尖叫），这里单独掐断并复位灯标志
            if (lightSource != null) lightSource.Stop();
            leftLightOn = rightLightOn = false;
        }

        private void OnPowerOut()
        {
            StopLoops();
            Play(powerOutSource);
            // 停电神曲不在这里播放：等熊现身左门、眼睛开始闪烁时才响（PowerOutExecutionBegan 事件）
            if (officeLightObject != null) officeLightObject.SetActive(false);
        }

        private void OnMenuReturned() => StopCarryoverAudio();

        private void OnPowerOutExecutionBegan()
        {
            // 熊现身+眼睛开始闪烁：停电神曲此刻才从黑暗中飘出（循环，直到跳杀或天亮被 StopLoops 掐断）
            Play(powerOutMusicSource);
        }

        private void OnDoorToggled(DoorSide side, bool closed) => Play(doorSource);

        private void OnDoorLightToggled(DoorSide side, bool on)
        {
            // 门灯按住式：按住（灯亮）循环播灯音，松手（灯灭）即停
            if (side == DoorSide.Left) leftLightOn = on;
            else rightLightOn = on;
            if (on)
            {
                Play(lightSource);
                // 开灯瞬间门外正好站着/贴着玩偶（门口站位或窗边贴脸）：惊吓提示音（单次播完）
                if (FNAF.Enemies.AnimatronicAI.IsAnyoneAtDoor(side)) Play(lightSpottedSource);
            }
            else if (lightSource != null) lightSource.Stop();
        }

        /// <summary>灯亮着的时候玩偶才走到门口/贴到窗边——同样算"开灯看见"，补一声惊吓提示音。</summary>
        private void PlaySpottedIfLit(DoorSide side)
        {
            bool lit = side == DoorSide.Left ? leftLightOn : rightLightOn;
            if (lit) Play(lightSpottedSource);
        }

        private void OnMonitorToggled(bool up)
        {
            Play(up ? monitorUpSource : monitorDownSource);
            // 监控开启=当前主活动：压低环境音，放下后立即恢复
            monitorUpNow = up;
            UpdateAmbientVolume();
        }

        /// <summary>环境音闪避总控：监控与各警报循环音期间按系数压低环境音（多重叠加取更低值）。</summary>
        private void UpdateAmbientVolume()
        {
            if (ambientSource == null) return;
            if (ambientBaseVolume < 0f) ambientBaseVolume = ambientSource.volume;
            float factor = 1f;
            if (monitorUpNow) factor = Mathf.Min(factor, monitorDuckFactor);
            if (knockNow || songNow) factor = Mathf.Min(factor, alertDuckFactor);
            ambientSource.volume = ambientBaseVolume * factor;
        }

        private void OnAnimatronicMoved(string enemyId, string fromRoomId, string toRoomId)
        {
            // 兔/鸡每挪一格播一声脚步（弗雷迪用深笑、狐狸用冲刺音，各有专属提示）
            if (enemyId == "Bonnie" || enemyId == "Chica")
            {
                Play(stepsSource);
                // 挪到办公室附近的房间（两个走廊角落 / 两个门口）时补一声靠近提示
                if (toRoomId == RoomIds.WestHallCorner || toRoomId == RoomIds.EastHallCorner
                    || toRoomId == RoomIds.LeftDoor || toRoomId == RoomIds.RightDoor)
                {
                    Play(nearOfficeSource);
                }
                // 灯亮着玩偶才走到门口：同样触发"开灯发现"惊吓音
                if (toRoomId == RoomIds.LeftDoor) PlaySpottedIfLit(DoorSide.Left);
                else if (toRoomId == RoomIds.RightDoor) PlaySpottedIfLit(DoorSide.Right);
            }
        }

        private void OnKitchenNoised(bool on)
        {
            if (on) Play(potsSource);
            else if (potsSource != null) potsSource.Stop();
        }

        private void OnFreddyMoved()
        {
            // 移动笑声二选一随机（熊笑声1 / 熊笑声2 录音变体）
            if (freddyLaughSource != null && freddyLaughClips != null && freddyLaughClips.Length > 0)
                freddyLaughSource.clip = freddyLaughClips[Random.Range(0, freddyLaughClips.Length)];
            Play(freddyLaughSource);
        }

        private void OnFoxyBanged(bool on)
        {
            if (on) Play(knockSource);
            else if (knockSource != null) knockSource.Stop();
            knockNow = on; // 砸门=警报主导，压低环境音
            UpdateAmbientVolume();
        }

        private void OnFoxyStageChanged(int stage)
        {
            // 狐狸帘子全开（阶段2，冲刺前兆）时开始循环唱歌，冲出/被赶回后停止
            if (stage == 2) Play(foxySongSource);
            else if (foxySongSource != null) foxySongSource.Stop();
            songNow = stage == 2; // 唱歌=冲刺前兆警报，压低环境音
            UpdateAmbientVolume();
        }

        private static void Play(AudioSource source)
        {
            if (source != null && source.clip != null) source.Play();
        }

        private void StopLoops()
        {
            if (ambientSource != null) ambientSource.Stop();
            if (potsSource != null) potsSource.Stop();
            if (knockSource != null) knockSource.Stop();
            if (foxySongSource != null) foxySongSource.Stop();
            if (powerOutMusicSource != null) powerOutMusicSource.Stop();
            monitorUpNow = knockNow = songNow = false;
            // 闪避结束后还原环境音基准音量（防压低值跨夜残留）
            if (ambientBaseVolume >= 0f && ambientSource != null) ambientSource.volume = ambientBaseVolume;
        }

        /// <summary>
        /// 掐断跨阶段残留音效（进入新夜晚 / 回主菜单时调用）：
        /// 循环音之外，钟声/欢呼/跳杀尖叫/停电下坠声/灯音等一次性音效也一并停止——
        /// 避免 6AM 点"继续"后钟声还飘进第二夜这类串音（用户 2026-11-08 反馈）。
        /// 注意：PlayerCaught 不走这里（走 StopLoops），否则会掐断刚响起的跳杀尖叫。
        /// </summary>
        private void StopCarryoverAudio()
        {
            StopLoops();
            if (chimeSource != null) chimeSource.Stop();
            if (cheerSource != null) cheerSource.Stop();
            if (screamSource != null) screamSource.Stop();
            if (powerOutSource != null) powerOutSource.Stop();
            if (lightSpottedSource != null) lightSpottedSource.Stop();
            if (lightSource != null) lightSource.Stop();
            leftLightOn = rightLightOn = false; // 新夜/回菜单：门灯状态复位
        }
    }
}
