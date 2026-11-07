using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの攻撃を1つずつ確かめるためのデバッグ操作。シーンに置き、表示キー(既定: F6)で表示を切り替える。
    /// 表示中だけ数字キーで攻撃を出せる(普段の操作でうっかり出さないため)。自動の攻撃を止めればボスは周回し続けるので、
    /// 出したい攻撃だけを好きな時に出せる。ゲームの速さを落とせば、動きを細かく見られる。
    /// 開発用ビルドとエディタでだけ動く。画面は IMGUI で描くので、UI のプレハブやキャンバスには手を入れない。
    /// </summary>
    public class KurageDebugControls : MonoBehaviour
    {
        private static readonly float[] TimeScales = { 1f, 0.5f, 0.25f };

        // 表示はこの高さの画面として描いて、実際の画面の高さに合わせて拡大する
        private const float ReferenceHeight = 720f;

        // 高さ ReferenceHeight の画面での表示位置。左上のHPバーとその下のバフアイコン(下端がおよそ y=110)より下に置く
        private static readonly Rect PanelRect = new Rect(10f, 118f, 640f, 150f);
        private static readonly Color PanelBackground = new Color(0f, 0f, 0f, 0.7f);

        [Tooltip("操作するクラゲボス。未設定ならシーンから探す")]
        [SerializeField]
        private KurageBossController _boss;

        [Tooltip("デバッグ表示を切り替えるキー。F2〜F5 は他のデバッグ機能が使っている")]
        [SerializeField]
        private Key _toggleKey = Key.F6;

        [Tooltip("再生を始めた時から表示しておくか")]
        [SerializeField]
        private bool _visibleOnStart = true;

        [Tooltip(
            "プレイヤーのHPを減らさないか。攻撃を見ている途中で倒れて確認が止まらないようにする(- キーでも切り替える)"
        )]
        [SerializeField]
        private bool _keepPlayerHpFull;

        private bool _visible;
        private int _timeScaleIndex;
        private string _lastAction = "";
        private PlayerStatus _playerStatus;

        // GUIStyle は保存できる型なので、付けないとエディタの再読み込みで空のスタイルが入り、下の ??= で作られなくなる
        [System.NonSerialized]
        private GUIStyle _style;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            if (_boss == null)
                _boss = FindAnyObjectByType<KurageBossController>();
            if (_boss == null)
            {
                Debug.LogError(
                    $"{name}: シーンにクラゲボスが見つからないので、デバッグ操作は使えません"
                );
                enabled = false;
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            _playerStatus = player != null ? player.GetComponent<PlayerStatus>() : null;
            _visible = _visibleOnStart;
        }

        private void OnEnable()
        {
            if (_playerStatus != null)
                _playerStatus.OnHpChanged += PreventLethalDamage;
        }

        private void OnDisable()
        {
            if (_playerStatus != null)
                _playerStatus.OnHpChanged -= PreventLethalDamage;
            // 速さを落としたまま止めると、次に再生した時まで残らないよう戻す
            if (_timeScaleIndex != 0)
                Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[_toggleKey].wasPressedThisFrame)
                _visible = !_visible;
            if (!_visible || _boss == null)
                return;

            if (keyboard.digit4Key.wasPressedThisFrame)
                StartAttack(KurageAttackKind.Stab, null, "突き");
            if (keyboard.digit5Key.wasPressedThisFrame)
                StartTentacles<KurageTentacleTrackingPattern>("地面の触手(追いかける)");
            if (keyboard.digit6Key.wasPressedThisFrame)
                StartTentacles<KurageTentacleCirclePlayerPattern>("地面の触手(囲む)");
            if (keyboard.digit7Key.wasPressedThisFrame)
                StartTentacles<KurageTentacleRandomPattern>("地面の触手(ランダム)");
            if (keyboard.digit8Key.wasPressedThisFrame)
                StartAttack(KurageAttackKind.Ring, null, "わっか");
            if (keyboard.digit9Key.wasPressedThisFrame)
            {
                _boss.AutoAttacks = !_boss.AutoAttacks;
                _lastAction = _boss.AutoAttacks
                    ? "自動の攻撃を再開"
                    : "自動の攻撃を停止(周回し続ける)";
            }
            if (keyboard.digit0Key.wasPressedThisFrame)
            {
                _timeScaleIndex = (_timeScaleIndex + 1) % TimeScales.Length;
                Time.timeScale = TimeScales[_timeScaleIndex];
                _lastAction = $"速さ x{Time.timeScale}";
            }
            if (keyboard.minusKey.wasPressedThisFrame && _playerStatus != null)
            {
                _keepPlayerHpFull = !_keepPlayerHpFull;
                _lastAction = _keepPlayerHpFull
                    ? "プレイヤーのHPを減らさない"
                    : "プレイヤーのHPを普通に減らす";
            }
        }

        private void LateUpdate()
        {
            // 当たった時のダメージ表示や怯みはそのまま起こし、HPだけを戻す(倒れなければ締め付けのレバガチャも試せる)。
            // Heal は呼ぶたびにログを出すので使わない。倒れた後の処理と食い違うので、倒れた後は戻さない
            if (
                _keepPlayerHpFull
                && _playerStatus != null
                && !_playerStatus.IsDead
                && _playerStatus.CurrentHp < _playerStatus.CurrentMaxHp
            )
                _playerStatus.RestoreHp(_playerStatus.CurrentMaxHp);
        }

        /// <summary>
        /// 1フレームに何本もの触手が当たると、LateUpdate で戻す前にHPが尽きる。
        /// HPが0になった瞬間に戻せば、PlayerStatus.TakeDamage の倒れる判定より先に間に合う。
        /// </summary>
        private void PreventLethalDamage(float currentHp, float maxHp)
        {
            if (_keepPlayerHpFull && currentHp <= 0f && !_playerStatus.IsDead)
                _playerStatus.RestoreHp(maxHp);
        }

        private void StartTentacles<T>(string label)
            where T : KurageTentaclePattern
        {
            foreach (KurageTentaclePattern pattern in _boss.TentaclePatterns)
            {
                if (pattern is T)
                {
                    StartAttack(KurageAttackKind.Tentacles, pattern, label);
                    return;
                }
            }
            _lastAction = $"{label}: ボスにこの出し方が登録されていません";
        }

        private void StartAttack(
            KurageAttackKind attack,
            KurageTentaclePattern pattern,
            string label
        )
        {
            bool started = _boss.TryStartAttack(attack, pattern);
            _lastAction = started
                ? $"{label} を出した"
                : $"{label}: 今は出せません(倒れている・締めている・わっかが頭上に無い)";
        }

        private void OnGUI()
        {
            if (!_visible || _boss == null)
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white },
            };

            // 画面の大きさが変わっても同じ見え方にするため、高さ ReferenceHeight の画面として描いて拡大する
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.height / ReferenceHeight));

            string halo =
                _boss.Restraint.IsRestraining ? "締めている"
                : _boss.Halo.IsOnHead ? "頭上"
                : "投げている";
            string text =
                $"<b>クラゲボス デバッグ</b>  ({_toggleKey} で表示切替)\n"
                + $"状態: {_boss.CurrentStateName}   わっか: {halo}\n"
                + $"自動の攻撃: {(_boss.AutoAttacks ? "ON" : "OFF")}   速さ: x{Time.timeScale}   "
                + $"HPを減らさない: {(_keepPlayerHpFull ? "ON" : "OFF")}{(_playerStatus != null && _playerStatus.IsDead ? " (倒れている)" : "")}\n"
                + "4: 突き   5: 触手(追いかける)   6: 触手(囲む)   7: 触手(ランダム)\n"
                + "8: わっか   9: 自動の攻撃 ON/OFF   0: 速さ切替   -: HPを減らさない ON/OFF\n"
                + _lastAction;
            // 背景の赤い画面でも読めるよう、暗い下地を敷いて白い文字で書く
            Color previousColor = GUI.color;
            GUI.color = PanelBackground;
            GUI.DrawTexture(PanelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(PanelRect, text, _style);

            GUI.matrix = previousMatrix;
        }
    }
}
