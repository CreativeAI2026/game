using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボスの発見状態(IsAlerted)の変化に合わせて、発見音と BGM を切り替える。
    /// TutorialBossController と同じ GameObject に置く。
    ///
    /// - 開始時: 通常時の BGM(怪しげな曲)を鳴らす
    /// - 見つかった瞬間(IsAlerted が false → true): 発見音(画面全体に鳴る演出音)を鳴らし、戦闘曲へフェードする
    /// - 見失って警戒が解けた瞬間(true → false): 通常時の BGM へフェードで戻す
    ///
    /// IsAlerted は複数のステートから直接書き換えられるため、ステート側に通知を足さずにここで毎フレーム変化を見る。
    /// フェード時間はどちらも試しながら調整できるようインスペクタに出している。
    /// </summary>
    [RequireComponent(typeof(TutorialBossController))]
    public class TBossAlertAudio : MonoBehaviour
    {
        [Header("発見音")]
        [Tooltip("見つかった瞬間の演出音。立体音はオフにしておく")]
        [SerializeField]
        private SoundCue _discoverySound;

        [Header("BGM")]
        [Tooltip("通常時(未発見時)の BGM")]
        [SerializeField]
        private SoundCue _ambientBgm;

        [Tooltip("見つかった後の戦闘曲")]
        [SerializeField]
        private SoundCue _battleBgm;

        [Tooltip("戦闘曲へ切り替えるフェードの時間(秒)")]
        [SerializeField]
        [Min(0f)]
        private float _toBattleFadeDuration = 1f;

        [Tooltip("通常時の BGM へ戻すフェードの時間(秒)")]
        [SerializeField]
        [Min(0f)]
        private float _toAmbientFadeDuration = 3f;

        private TutorialBossController _boss;
        private bool _wasAlerted;

        private void Awake()
        {
            _boss = GetComponent<TutorialBossController>();
        }

        private void Start()
        {
            _wasAlerted = _boss.IsAlerted;
            BgmPlayer.Play(_wasAlerted ? _battleBgm : _ambientBgm);
        }

        private void Update()
        {
            bool isAlerted = _boss.IsAlerted;
            if (isAlerted == _wasAlerted)
                return;

            _wasAlerted = isAlerted;
            if (isAlerted)
            {
                SoundEffectPlayer.Play(_discoverySound, this);
                BgmPlayer.Play(_battleBgm, BgmLayer.Main, _toBattleFadeDuration);
            }
            else
            {
                BgmPlayer.Play(_ambientBgm, BgmLayer.Main, _toAmbientFadeDuration);
            }
        }
    }
}
