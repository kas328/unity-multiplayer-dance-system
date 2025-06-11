using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EasyUI.Toast;
using Mingle.Dev.Extensions.Animators;
using Mingle.Dev.Extensions.Audios;
using Mingle.Dev.KSK_Test._02.Scripts.Utility;
using Photon.Pun;
using UnityEngine;

namespace Mingle.Dev.KSK_Test._02.Scripts.Dance
{
    public class DanceEmoticonManager : MonoBehaviourPunCallbacks
    {
        #region Private Fields
        private PhotonView _photonView;
        private PlayerMoveManager _playerMoveManager;
        private Coroutine _autoAcceptCoroutine;
        private Coroutine _audioSyncCoroutine;
        private bool _isDancing = false;
        private bool _isWaitingForResponse = false;
        private float _savedBGMVolume;
        private DanceUISetup _danceUISetup;
        private Dictionary<string, AudioClip> _danceMusicMap;
        private string[] _danceMusicTriggerName;
        #endregion

        #region SerializeField
        [SerializeField] private float danceRadius;
        [SerializeField] private GameObject danceUI;
        [SerializeField] private PhotonView emoticonPhotonView;
        
        [Header("사운드")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip danceMusic_E;
        [SerializeField] private AudioClip danceMusic_I;
        [SerializeField] private AudioClip danceMusic_F;
        [SerializeField] private AudioClip danceMusic_T;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            StartCoroutine(InitializeAfterAvatarSpawn());
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnEmoticonVolumeChanged -= UpdateEmoticonVolume;
                SettingsManager.Instance.OnEmoticonMutedChanged -= UpdateEmoticonMuteState;
            }
            
            if (_audioSyncCoroutine != null)
            {
                StopCoroutine(_audioSyncCoroutine);
                _audioSyncCoroutine = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (GameManager.Instance?.MyAvatar == null) return;

            Vector3 myPosition = GameManager.Instance.MyAvatar.transform.position;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(myPosition, danceRadius);

            PlayerManager[] allPlayers = FindObjectsOfType<PlayerManager>();
            foreach (PlayerManager playerManager in allPlayers)
            {
                if (playerManager.gameObject == GameManager.Instance.MyAvatar) continue;

                float distance = Vector3.Distance(myPosition, playerManager.transform.position);
                Gizmos.color = distance <= danceRadius ? Color.green : Color.red;
                Gizmos.DrawLine(myPosition, playerManager.transform.position);
            }
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeAfterAvatarSpawn()
        {
            yield return new WaitUntil(() => GameManager.Instance.MyAvatar != null);

            _danceMusicMap = new Dictionary<string, AudioClip>
            {
                { "Dance_E_Trigger", danceMusic_E },
                { "Dance_I_Trigger", danceMusic_I },
                { "Dance_F_Trigger", danceMusic_F },
                { "Dance_T_Trigger", danceMusic_T }
            };
            
            _danceMusicTriggerName = _danceMusicMap.Keys.ToArray();
            
            var avatar = GameManager.Instance.MyAvatar;
            _photonView = avatar.GetPhotonView();
            _playerMoveManager = avatar.GetComponent<PlayerMoveManager>();
            _danceUISetup = danceUI.GetComponent<DanceUISetup>();
            
            // 이모티콘 볼륨 변경 이벤트 구독
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnEmoticonVolumeChanged += UpdateEmoticonVolume;
                SettingsManager.Instance.OnEmoticonMutedChanged += UpdateEmoticonMuteState;
            }
        }
        #endregion

        #region Audio Management
        // 볼륨 변경 시 호출
        private void UpdateEmoticonVolume(float volume)
        {
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }

        // 음소거 상태 변경 시 호출
        private void UpdateEmoticonMuteState(bool muted)
        {
            if (audioSource != null)
            {
                audioSource.mute = muted;
            }
        }

        private void StartAudioSync()
        {
            if (_audioSyncCoroutine != null)
            {
                StopCoroutine(_audioSyncCoroutine);
            }
            _audioSyncCoroutine = StartCoroutine(SyncAudioWithSettings());
        }

        private void StopAudioSync()
        {
            if (_audioSyncCoroutine != null)
            {
                StopCoroutine(_audioSyncCoroutine);
                _audioSyncCoroutine = null;
            }
        }

        private IEnumerator SyncAudioWithSettings()
        {
            while (_isDancing || _isWaitingForResponse)
            {
                if (audioSource)
                {
                    audioSource.mute = SettingsManager.Instance.EmoticonMuted;
                    audioSource.volume = SettingsManager.Instance.EmoticonVolume;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
        #endregion

        #region Dance Request
        public void RequestGroupDance()
        {
            if (!PhotonNetwork.IsConnected || _isDancing || _isWaitingForResponse) return;

            var text = LocalizationManager.GetLocalizedText("ToastUITable", "ToastUI_Emo_Dance_sender_send",
                "친구들과 함께 즐겁게 춤을 춰봐요!");
            Toast.Show($"<size=14>{text}</size>", 2f, ToastPosition.TopCenter);

            List<PhotonView> nearByPlayers = new List<PhotonView>();
            PlayerActionManager[] allPlayers = FindObjectsOfType<PlayerActionManager>();

            foreach (PlayerActionManager playerAction in allPlayers)
            {
                if (playerAction.gameObject == GameManager.Instance.MyAvatar) continue;

                float distance = Vector3.Distance(GameManager.Instance.MyAvatar.transform.position,
                    playerAction.transform.position);
                if (distance <= danceRadius)
                {
                    PhotonView pv = playerAction.photonView;
                    if (pv != null) nearByPlayers.Add(pv);
                }
            }
            string requesterName = _photonView.Owner.NickName;
            string selectedDance = _danceMusicTriggerName[Random.Range(0, _danceMusicTriggerName.Length)];

            StartDanceCountdown(selectedDance);
            if (nearByPlayers.Count > 0)
            {
                foreach (var nearByPlayer in nearByPlayers.Select(pv => pv.Owner))
                    emoticonPhotonView.RPC(nameof(ReceiveDanceRequest), nearByPlayer, selectedDance, requesterName);
            }
        }

        [PunRPC]
        private void ReceiveDanceRequest(string danceName, string requesterName)
        {
            if (_isDancing || _isWaitingForResponse) return;

            _isWaitingForResponse = true;
            GameManager.Instance.MyAvatar.GetComponent<PlayerMoveManager>().enabled = false;

            _autoAcceptCoroutine = StartDanceCountdown(danceName);

            var text = LocalizationManager.GetLocalizedTextWithColoredText(
                "ToastUITable",
                "ToastUI_Emo_Dance_receiver_receive",
                requesterName,
                "{0}님과 함께 신나는 댄스 타임을 가져요!"
            );
            var confirmText = LocalizationManager.GetLocalizedText("GeneralTable", "UI_Common_Decline", "거절");
            
            ThinPopup.Create()
                .SetTitle($"<size=12>{text}</size>")
                .SetDuration(0)
                .SetTimeLimit(5f)
                .SetConfirmButtonText(confirmText)
                .SetConfirmAction(RejectDance)
                .SetColor(new Color(0x73/255f, 0xE9/255f, 0xD0/255f))
                .Show();
        }

        public void RejectDance()
        {
            if (!_isWaitingForResponse) return;

            if (_autoAcceptCoroutine != null)
            {
                StopCoroutine(_autoAcceptCoroutine);
                _autoAcceptCoroutine = null;
            }

            _isWaitingForResponse = false;
            _isDancing = false;
            _playerMoveManager.enabled = true;
            Toast.Show("춤추기를 거절했습니다.", 2f);
        }
        #endregion

        #region Dance Execution
        private Coroutine StartDanceCountdown(string danceName) => StartCoroutine(DanceCountdown(danceName));

        private IEnumerator DanceCountdown(string danceName)
        {
            _isDancing = true;
            yield return new WaitForSeconds(2f);

            for (int i = 3; i > 0; i--)
            {
                _danceUISetup.ShowCountdown(i);
                yield return new WaitForSeconds(1f);
            }
            StartDance(danceName);
        }

        private void StartDance(string danceName)
        {
            _isWaitingForResponse = false;
            _isDancing = true;
            _playerMoveManager.enabled = true;
            
            // 현재 배경음악 볼륨을 저장하고 음소거
            if (SettingsManager.Instance != null)
            {
                _savedBGMVolume = SettingsManager.Instance.BGMVolume;
                SettingsManager.Instance.BGMVolume = 0f;
            }
            
            emoticonPhotonView.RPC(nameof(RPCStartDance), RpcTarget.All, _photonView.ViewID, danceName);
        }

        [PunRPC]
        private void RPCStartDance(int viewId, string danceName)
        {
            var pv = PhotonView.Find(viewId);
            var targetAnimator = pv.GetComponent<Animator>();
            targetAnimator.SetTrigger(danceName);
            targetAnimator.SetLayerWeight(6, 1);
    
            // * 춤 및 오디오 시작
            if (pv.IsMine)
            {
                if (audioSource)
                {
                    audioSource.PlayMusicByType(danceName, _danceMusicMap);
                    
                    // 이모티콘 볼륨 및 음소거 설정 적용
                    audioSource.volume = SettingsManager.Instance.EmoticonVolume;
                    audioSource.mute = SettingsManager.Instance.EmoticonMuted;
                    
                    StartAudioSync();
                }
            }
    
            string stateName = danceName.Replace("_Trigger", "");
            StartCoroutine(EndDanceAfterAnimation(pv, stateName));
        }

        private IEnumerator EndDanceAfterAnimation(PhotonView pv, string stateName)
        {
            var targetAnimator = pv.GetComponent<Animator>();
            yield return new WaitUntil(() => targetAnimator.IsAnimationComplete(stateName,6)); 
            targetAnimator.SetLayerWeight(6, 0);
            
            // * 춤 끝
            if (pv.IsMine)
            {
                StopAudioSync();
                if (audioSource && audioSource.isPlaying) 
                {
                    audioSource.Stop();
                }
                
                if (SettingsManager.Instance != null)
                {
                    SettingsManager.Instance.BGMVolume = _savedBGMVolume;
                }

                _isDancing = false;
                _isWaitingForResponse = false;
            }
        }
        #endregion
    }
}