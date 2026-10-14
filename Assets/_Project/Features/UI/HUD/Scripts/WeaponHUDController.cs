using System.Collections.Generic;
using System.Threading;
using CreativeAI.Gameplay;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 武器切替UI(documents/Specification.md §5)。入手ずみの武器のパネルを楕円上に並べ、選択中の武器を上に置く。
    ///
    /// ■ パネルと武器の対応
    ///   パネルは <b>武器1本につき1枚</b>、雛形(<see cref="_panelTemplate"/>)から武器の数だけ複製して作る。
    ///   アイコンはその武器のデータ(WeaponData の Icon)から設定する。
    ///   パネルとアイコンは常に同じ武器の組で、回転してもパネル同士でアイコンが入れ替わることはない。
    ///   武器が増えても WeaponManager の一覧に足すだけでパネルも増える(Inspector でパネルを並べ直さなくてよい)。
    ///
    /// ■ 配置は「状態」から毎回決める
    ///   入手ずみ武器(WeaponManager.OwnedIndices)と選択中(CurrentWeaponIndex)から各パネルの位置を計算する。
    ///   以前は「切り替わった」通知のたびに1枠ずつ回していたため、未入手の武器を飛ばした切替・入手・セーブ復元・
    ///   アニメーション中の連打で、表示と実際の武器がずれていた。
    ///   通知は「いつ並べ直すか」と「どちら回りに動かすか」にしか使わない。
    ///
    /// ■ 出し分け
    ///   武器の所持本数: 0本で非表示・1本以上で表示。未入手の武器のパネルは出さない。
    ///   GameObject ごと消すと <see cref="WeaponManager"/> の購読が切れて戻れないため、
    ///   Canvas / GraphicRaycaster の enabled で出し入れする。
    ///
    /// ■ アニメーション
    ///   全パネルが楕円上を弧を描いて移動する（角度補間）。並びは「選択中が上、そこから反時計回りに次の武器」なので、
    ///   E(次の武器) を押すと左隣のパネルが上へ来る = パネルは時計回りに動く。Q(前の武器) はその逆。
    ///   アニメーション中に次の切替が来たら、その時点の位置から新しい配置へ向けて動き直す(入力を捨てない)。
    ///
    /// ■ 描画順(アニメーション中も毎フレーム更新)
    ///   switchUI → 常に最前面 / 選択中パネル → その直下 /
    ///   以降は楕円の上下(真上・真下)に近いほど手前、左右の端ほど奥。
    ///   4本なら「上(選択) → 下 → 左右」の順に重なる。
    /// </summary>
    public class WeaponHUDController : MonoBehaviour
    {
        [Header("パネル")]
        [Tooltip(
            "武器1本分のパネルの雛形。武器の数だけ複製して使う(雛形自体は非表示になる)。"
                + "背景の Image をこのオブジェクトに持ち、子にアイコン用の Image を持つこと"
        )]
        [SerializeField]
        private RectTransform _panelTemplate;

        [Tooltip("雛形の中の、武器アイコンを表示する Image(雛形の子孫)")]
        [SerializeField]
        private Image _templateIcon;

        [SerializeField]
        private float duration = 0.3f;

        [SerializeField]
        [Tooltip("左右方向の楕円半径（px）。count=3 の左下・右下、count=4 の左右に使用。")]
        private float panelSpacingX = 150f;

        [SerializeField]
        [Tooltip(
            "上下方向の楕円半径（px）。選択スロット（上固定）の高さ、count=2/4 の下スロットに使用。switchUI は Y=panelSpacingY に配置すること。"
        )]
        private float panelSpacingY = 100f;

        [Header("所持本数で出し入れする自分の Canvas(0本=非表示)")]
        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private GraphicRaycaster _raycaster;

        [SerializeField]
        [Tooltip(
            "Q/E・矢印をまとめた親オブジェクト。入手ずみが2本以上のとき表示、1本以下は非表示。常に最前面。"
                + "パネルの雛形と同じ親の下に置くこと"
        )]
        private GameObject switchUI;

        /// <summary>武器1本分のパネル。weaponIndex は WeaponManager の武器の並び(WeaponKeys)の番号。</summary>
        private sealed class WeaponPanel
        {
            public int weaponIndex;
            public RectTransform rect;
            public Image background;

            /// <summary>楕円上の現在の角度(ラジアン)。上(選択位置)が π/2。</summary>
            public float angle;

            /// <summary>前回の並べ直しで表示されていたか(新しく入手した武器は動かさずにその場へ出す)。</summary>
            public bool wasShown;
        }

        private readonly List<WeaponPanel> _panels = new();
        private readonly List<WeaponPanel> _depthOrderBuffer = new(); // 描画順の並べ替え用(毎フレームの確保を避ける)
        private readonly Color darkColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private readonly Color lightColor = Color.white;

        // 並べ直しのたびに増やし、古いアニメーションを止めるための番号
        private int _layoutVersion;

        // 依存。プレイヤーは Prefab から実行時に生成されるので、PlayerRegistry から受け取る(未生成の間は null)
        private WeaponManager _weaponManager;

        private void Awake()
        {
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();
            if (_raycaster == null)
                _raycaster = GetComponent<GraphicRaycaster>();
        }

        private void Start()
        {
            Assert.IsNotNull(_panelTemplate, $"{name}: パネルの雛形(_panelTemplate)が未設定です");
            Assert.IsNotNull(
                _templateIcon,
                $"{name}: 雛形のアイコン Image(_templateIcon)が未設定です"
            );
            Assert.IsTrue(
                _templateIcon.transform != _panelTemplate
                    && _templateIcon.transform.IsChildOf(_panelTemplate),
                $"{name}: _templateIcon は雛形(_panelTemplate)の子孫(雛形自身以外)にしてください"
            );
            _panelTemplate.gameObject.SetActive(false);

            BuildPanels();
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += HandlePlayerChanged;
            if (PlayerRegistry.Current != null)
                _weaponManager = PlayerRegistry.Current.GetComponent<WeaponManager>();

            SubscribeWeaponManager();
            // Start より先に来ても効くように、購読と同時に現状を反映する。
            ApplyOwnedCount();
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= HandlePlayerChanged;
            UnsubscribeWeaponManager();
        }

        /// <summary>プレイヤーが生成・破棄されたときに、購読先の WeaponManager を付け替えてパネルを作り直す。</summary>
        private void HandlePlayerChanged(PlayerController player)
        {
            UnsubscribeWeaponManager();
            _weaponManager = player != null ? player.GetComponent<WeaponManager>() : null;
            SubscribeWeaponManager();
            BuildPanels();
        }

        private void SubscribeWeaponManager()
        {
            if (_weaponManager == null)
                return; // プレイヤー未生成
            _weaponManager.OnWeaponSwitched += HandleWeaponSwitched;
            _weaponManager.OnOwnedCountChanged += HandleOwnedCountChanged;
        }

        private void UnsubscribeWeaponManager()
        {
            if (_weaponManager == null)
                return;
            _weaponManager.OnWeaponSwitched -= HandleWeaponSwitched;
            _weaponManager.OnOwnedCountChanged -= HandleOwnedCountChanged;
        }

        /// <summary>所持本数が変わったとき(giveWeapon / セーブ復元)に出し入れし、アニメーションなしで並べ直す。</summary>
        private void HandleOwnedCountChanged(int ownedCount)
        {
            ApplyVisibility(ownedCount);
            Layout(animate: false, clockwise: true);
        }

        /// <summary>選択が変わったときに、その方向へ回して並べ直す。true: 前の武器(Q) = 反時計回り。</summary>
        private void HandleWeaponSwitched(bool isLeftRotation)
        {
            Layout(animate: true, clockwise: !isLeftRotation);
        }

        private void ApplyOwnedCount() =>
            ApplyVisibility(_weaponManager != null ? _weaponManager.OwnedCount : 0);

        /// <summary>
        /// 武器0本=非表示 / 1本以上=表示(モードでは変えない)。
        /// GameObject ごと止めると WeaponManager の購読が切れて戻れないため Canvas を無効化するだけにする。
        /// </summary>
        private void ApplyVisibility(int ownedCount)
        {
            bool show = ownedCount > 0;
            if (_canvas != null)
                _canvas.enabled = show;
            if (_raycaster != null)
                _raycaster.enabled = show;
        }

        /// <summary>
        /// 武器1本につき1枚、雛形からパネルを作り直し、アイコンを武器のデータから設定する。
        /// 雛形が未設定(EditMode テスト等)・プレイヤー未生成のときは作らない。
        /// </summary>
        private void BuildPanels()
        {
            foreach (var panel in _panels)
                Destroy(panel.rect.gameObject);
            _panels.Clear();

            if (_panelTemplate == null || _weaponManager == null)
                return;

            string iconPath = RelativePath(_templateIcon.transform, _panelTemplate);
            for (int i = 0; i < _weaponManager.WeaponCount; i++)
            {
                RectTransform rect = Instantiate(_panelTemplate, _panelTemplate.parent);
                rect.name =
                    i < WeaponManager.WeaponKeys.Count
                        ? $"Panel_{WeaponManager.WeaponKeys[i]}"
                        : $"Panel_{i}";

                // 複製したパネルの中で、雛形のアイコンと同じ位置にある Image にこの武器のアイコンを入れる
                Image icon = rect.Find(iconPath).GetComponent<Image>();
                Sprite sprite = _weaponManager.GetIcon(i);
                icon.sprite = sprite;
                icon.enabled = sprite != null; // アイコン未設定の武器は空のパネルにする

                _panels.Add(
                    new WeaponPanel
                    {
                        weaponIndex = i,
                        rect = rect,
                        background = rect.GetComponent<Image>(),
                    }
                );
            }

            ApplyOwnedCount();
            Layout(animate: false, clockwise: true);
        }

        /// <summary>
        /// 入手ずみの武器と選択中の武器から、各パネルの表示・位置・色・描画順を決める。
        /// </summary>
        /// <param name="animate">true なら現在位置から楕円に沿って動かす。</param>
        /// <param name="clockwise">動かす向き。true で時計回り(E = 次の武器。左隣が上へ来る)。</param>
        private void Layout(bool animate, bool clockwise)
        {
            if (_panels.Count == 0 || _weaponManager == null)
                return;

            IReadOnlyList<int> owned = _weaponManager.OwnedIndices;
            int count = owned.Count;
            int selectedPosition = Mathf.Max(0, IndexOf(owned, _weaponManager.CurrentWeaponIndex));

            if (switchUI != null)
                switchUI.SetActive(count >= 2);

            var startAngles = new float[_panels.Count];
            var endAngles = new float[_panels.Count];

            foreach (var panel in _panels)
            {
                int position = IndexOf(owned, panel.weaponIndex);
                bool shown = position >= 0;
                panel.rect.gameObject.SetActive(shown);
                if (!shown)
                {
                    panel.wasShown = false;
                    continue;
                }

                // 選択中の武器が slot 0(上)。入手ずみの並び(切り替え順)に、上から反時計回りに並べる
                int slot = (position - selectedPosition + count) % count;
                float target = SlotAngle(slot, count);

                float start = panel.wasShown ? panel.angle : target;
                startAngles[panel.weaponIndex] = start;
                endAngles[panel.weaponIndex] = UnwrapToward(start, target, clockwise);
                panel.wasShown = true;

                if (panel.background != null)
                    panel.background.color = slot == 0 ? lightColor : darkColor;
            }

            _layoutVersion++;
            if (animate)
            {
                AnimatePanels(startAngles, endAngles, _layoutVersion, destroyCancellationToken)
                    .Forget();
            }
            else
            {
                foreach (var panel in _panels)
                {
                    if (panel.wasShown)
                        SetAngle(panel, endAngles[panel.weaponIndex]);
                }
                ApplyDepthOrder();
            }
        }

        private async UniTaskVoid AnimatePanels(
            float[] startAngles,
            float[] endAngles,
            int version,
            CancellationToken token
        )
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 途中で次の並べ直しが始まったら、この動きは捨てる(新しい動きが現在位置から引き継ぐ)
                if (version != _layoutVersion)
                    return;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = t * t * (3f - 2f * t);

                foreach (var panel in _panels)
                {
                    if (panel.wasShown)
                        SetAngle(
                            panel,
                            Mathf.Lerp(
                                startAngles[panel.weaponIndex],
                                endAngles[panel.weaponIndex],
                                smoothT
                            )
                        );
                }
                ApplyDepthOrder();

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (version != _layoutVersion)
                return;
            foreach (var panel in _panels)
            {
                if (panel.wasShown)
                    SetAngle(panel, endAngles[panel.weaponIndex]);
            }
            ApplyDepthOrder();
        }

        private void SetAngle(WeaponPanel panel, float angle)
        {
            panel.angle = angle;
            panel.rect.anchoredPosition = new Vector2(
                panelSpacingX * Mathf.Cos(angle),
                panelSpacingY * Mathf.Sin(angle)
            );
        }

        /// <summary>slot 番目の角度。slot 0 = 上(π/2)、そこから反時計回りに等間隔(4本なら 上→左→下→右)。</summary>
        private static float SlotAngle(int slot, int count) =>
            Mathf.PI * 0.5f + slot * (2f * Mathf.PI / count);

        /// <summary>
        /// start から target へ、指定の向きに 1 周未満で回る角度を返す(target ± 2π の中から選ぶ)。
        /// 時計回り = 角度が減る向き。
        /// </summary>
        private static float UnwrapToward(float start, float target, bool clockwise)
        {
            const float fullTurn = 2f * Mathf.PI;
            float delta = Mathf.Repeat(target - start, fullTurn); // 反時計回りに進む量 [0, 2π)
            if (delta < 0.0001f)
                return start; // 位置が変わらない
            return clockwise ? start - (fullTurn - delta) : start + delta;
        }

        /// <summary>
        /// 描画順: 左右の端(奥)から順に最前面へ送り、上下に近いものほど手前にする。
        /// 最後に選択中のパネル、さらに switchUI を送ると、それらが一番手前になる。
        /// パネルの現在角度で決めるので、回転アニメーション中も重なりが自然に入れ替わる。
        /// </summary>
        private void ApplyDepthOrder()
        {
            _depthOrderBuffer.Clear();
            foreach (var panel in _panels)
            {
                if (panel.wasShown && panel.weaponIndex != _weaponManager.CurrentWeaponIndex)
                    _depthOrderBuffer.Add(panel);
            }

            // |sin| が小さい(左右の端)ほど奥 = 先に送る
            _depthOrderBuffer.Sort(
                (a, b) => Mathf.Abs(Mathf.Sin(a.angle)).CompareTo(Mathf.Abs(Mathf.Sin(b.angle)))
            );
            foreach (var panel in _depthOrderBuffer)
                panel.rect.SetAsLastSibling();

            int selected = _weaponManager.CurrentWeaponIndex;
            if (selected >= 0 && selected < _panels.Count && _panels[selected].wasShown)
                _panels[selected].rect.SetAsLastSibling();

            if (switchUI != null)
                switchUI.transform.SetAsLastSibling();
        }

        private static int IndexOf(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return i;
            }
            return -1;
        }

        /// <summary>root から見た target の相対パス(Transform.Find 用)。</summary>
        private static string RelativePath(Transform target, Transform root)
        {
            string path = target.name;
            for (Transform t = target.parent; t != root; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }
    }
}
