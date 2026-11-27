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
    /// 変化は <see cref="EnemyBaseController.AlertedChanged"/> で受け取る。
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

        private void Awake()
        {
            _boss = GetComponent<TutorialBossController>();
        }

        private void Start()
        {
            BgmPlayer.Play(_boss.IsAlerted ? _battleBgm : _ambientBgm);
            // 開始時の曲を決めてから変化を受け取る(それまでの変化は開始時の曲に含まれる)
            _boss.AlertedChanged += OnAlertedChanged;
        }

        private void OnDestroy()
        {
            // Start 前に破棄された場合は購読していないが、外すだけなので問題ない
            _boss.AlertedChanged -= OnAlertedChanged;
        }

        private void OnAlertedChanged(bool isAlerted)
        {
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
