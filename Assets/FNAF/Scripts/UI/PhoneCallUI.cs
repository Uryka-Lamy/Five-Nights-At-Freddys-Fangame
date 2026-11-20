using System.Collections;
using UnityEngine;
using FNAF.Core;

namespace FNAF.UI
{
    /// <summary>
    /// 电话语音：夜晚开始后延迟片刻播放对应夜的录音（第 1~5 夜有录音，第 6/7/自定义夜无电话）。
    /// 开播后 60 秒内可点游玩界面左上角"挂断电话"按钮挂断；超时按钮消失且不能再挂。
    /// 断电/被抓/天亮自动挂断。
    /// </summary>
    public class PhoneCallUI : MonoBehaviour
    {
        [Tooltip("各夜电话语音（索引 0 = 第一夜）")]
        [SerializeField] private AudioClip[] nightVoiceClips;

        [Tooltip("夜晚片头结束后再开始播放的延迟（秒）")]
        [SerializeField] private float startDelay = 3.0f;

        [Tooltip("可挂断窗口时长（秒）：超时按钮消失且不能再挂")]
        [SerializeField] private float hangupWindow = 60f;

        [Tooltip("挂断电话按钮（游玩界面左上角，仅窗口期内显示）")]
        [SerializeField] private GameObject hangupButton;

        private AudioSource voiceSource;
        private Coroutine callRoutine;
        private bool canHangup;

        private void Awake()
        {
            // 挂一个自己的音源播各夜电话语音
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            if (hangupButton != null) hangupButton.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.NightStarted += OnNightStarted;
            GameEvents.NightCompleted += OnNightCompleted;
            GameEvents.PlayerCaught += OnPlayerCaught;
            GameEvents.PowerOut += OnPowerOut;
            GameEvents.MenuReturned += OnMenuReturned;
        }

        private void OnDisable()
        {
            GameEvents.NightStarted -= OnNightStarted;
            GameEvents.NightCompleted -= OnNightCompleted;
            GameEvents.PlayerCaught -= OnPlayerCaught;
            GameEvents.PowerOut -= OnPowerOut;
            GameEvents.MenuReturned -= OnMenuReturned;
        }

        private void OnMenuReturned() => HangUp(); // ESC 退出本夜：电话在主菜单里也要挂断

        private void OnNightStarted(int night)
        {
            HangUp();
            // 该夜的语音：有录音才播（第 6/7/自定义夜没有电话）
            AudioClip voice = nightVoiceClips != null && night >= 1 && night <= nightVoiceClips.Length
                ? nightVoiceClips[night - 1]
                : null;
            if (voice == null) return;
            callRoutine = StartCoroutine(CallRoutine(voice));
        }

        private IEnumerator CallRoutine(AudioClip voice)
        {
            yield return new WaitForSeconds(startDelay);
            if (voiceSource == null) yield break;
            voiceSource.clip = voice;
            voiceSource.loop = false;
            voiceSource.Play();

            // 挂断窗口期内按钮显示；窗口过了按钮消失（语音继续播到完，只是不能再挂）
            canHangup = true;
            if (hangupButton != null) hangupButton.SetActive(true);
            float window = hangupWindow;
            while (window > 0f && voiceSource.isPlaying)
            {
                window -= Time.deltaTime;
                yield return null;
            }
            canHangup = false;
            if (hangupButton != null) hangupButton.SetActive(false);
            callRoutine = null;
        }

        /// <summary>挂断电话按钮点击（场景生成器绑定）。</summary>
        public void OnHangupClicked()
        {
            if (!canHangup) return;
            if (callRoutine != null)
            {
                StopCoroutine(callRoutine);
                callRoutine = null;
            }
            canHangup = false;
            if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
            if (hangupButton != null) hangupButton.SetActive(false);
        }

        private void OnNightCompleted(int night) => HangUp();
        private void OnPlayerCaught(string enemyId) => HangUp();
        private void OnPowerOut() => HangUp();

        private void HangUp()
        {
            if (callRoutine != null)
            {
                StopCoroutine(callRoutine);
                callRoutine = null;
            }
            canHangup = false;
            if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
            if (hangupButton != null) hangupButton.SetActive(false);
        }
    }
}
