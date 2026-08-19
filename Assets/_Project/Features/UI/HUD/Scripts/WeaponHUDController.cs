using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 所持武器数（0〜4）に応じてパネルの表示・位置・レイヤーを自動管理する。
    /// 武器切替UI(documents/Specification.md §5)。選択中の武器を中央に置くインラインHUD。
    ///
    /// ■ スロット設計
    ///   スロット0 = 選択中 = 常に上の固定座標 (0, +panelSpacingY)
    ///   武器数に応じたスロット座標は CalculateSlotPositions() で自動計算。
    ///   currentIndex は「panels[] のうち、現在スロット0（選択位置）にいる要素のインデックス」を示す。
    ///
    /// ■ 出し分けと表示
    ///   出し分けはモードではなく<b>武器の所持本数</b>: 0本で非表示・1本以上で表示(表示中は移動中・戦闘中とも)。
    ///   GameObject ごと消すと <see cref="WeaponManager"/> の購読が切れて戻れないため、
    ///   HudIconBar と同じく Canvas / GraphicRaycaster の enabled で出し入れする。
    ///
    /// ■ アニメーション
    ///   全パネルが楕円上を弧を描いて移動する（角度 Lerp）。
    ///   E(次の武器) → 時計回り(CW) / Q(前の武器) → 反時計回り(CCW)
    ///
    /// ■ レイヤー管理（ApplyLayerOrder）
    ///   switchUI          → 常に最前面（SetAsLastSibling）
    ///   選択中パネル(slot0) → switchUI の直下
    ///   以降スロット順      → 遠いほど奥（SetSiblingIndex が低い）
    ///   切替アニメーション開始時・終了後の両方で必ず再適用し、
    ///   アニメーション中の重なり崩れを防ぐ。
    /// </summary>
    public class WeaponHUDController : MonoBehaviour
    {
        [SerializeField]
        private List<RectTransform> panels; // 最大4枚をInspectorで登録

        [SerializeField]
        private float duration = 0.3f;

        [SerializeField]
        [Tooltip("左右方向の楕円半径（px）。count=3 の左下・右下、count=4 の左右に使用。")]
        private float panelSpacingX = 150f;

        [Header("所持本数で出し入れする自分の Canvas(0本=非表示)")]
        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private GraphicRaycaster _raycaster;

        [SerializeField]
        [Tooltip(
            "上下方向の楕円半径（px）。選択スロット（上固定）の高さ、count=2/4 の下スロットに使用。switchUI は Y=panelSpacingY に配置すること。"
        )]
        private float panelSpacingY = 100f;

        [SerializeField]
        [Tooltip(
            "Q/E・矢印をまとめた親オブジェクト。武器数が2以上のとき表示、1以下は非表示。常に最前面。"
        )]
        private GameObject switchUI;

        // 内部状態
        /// <summary>panels[currentIndex] が現在スロット0（選択中・上固定）にいる。</summary>
        private int currentIndex = 0;
        private bool isAnimating = false;
        private int _weaponCount = 0;

        /// <summary>スロット番号 → anchoredPosition のテーブル。Start で武器数に応じて生成。</summary>
        private Vector2[] _slotPositions;

        private readonly Color darkColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private readonly Color lightColor = Color.white;

        // 依存
        private PlayerInput _playerInput;
        private PlayerInputHandler _input;
        private WeaponManager _weaponManager;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _input = GetComponent<PlayerInputHandler>();
            _weaponManager = GetComponent<WeaponManager>();

            if (_canvas == null)
                _canvas = GetComponent<Canvas>();
            if (_raycaster == null)
                _raycaster = GetComponent<GraphicRaycaster>();
        }

        private void Start()
        {
            _weaponCount = _weaponManager != null ? _weaponManager.WeaponCount : 0;
            _slotPositions = CalculateSlotPositions(_weaponCount);

            for (int i = 0; i < panels.Count; i++)
                panels[i].gameObject.SetActive(i < _weaponCount);

            for (int i = 0; i < _weaponCount; i++)
            {
                int slot = SlotOf(i);
                panels[i].anchoredPosition = _slotPositions[slot];
                panels[i].GetComponent<Image>().color = (slot == 0) ? lightColor : darkColor;
            }

            if (switchUI != null)
                switchUI.SetActive(_weaponCount >= 2);

            // 選択中の武器を中央に据えてから、所持本数で出し入れする(0本なら非表示)。
            SnapToSelection();
            ApplyOwnedCount();
        }

        private void OnEnable()
        {
            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponSwitched += HandleWeaponSwitched;
                _weaponManager.OnOwnedCountChanged += HandleOwnedCountChanged;
            }
            // Start より先に来ても効くように、購読と同時に現状を反映する。
            ApplyOwnedCount();
        }

        private void OnDisable()
        {
            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponSwitched -= HandleWeaponSwitched;
                _weaponManager.OnOwnedCountChanged -= HandleOwnedCountChanged;
            }
        }

        /// <summary>
        /// 所持本数が変わったとき(giveWeapon / セーブ復元)に出し入れし、選択中の武器を中央へ据え直す。
        /// </summary>
        private void HandleOwnedCountChanged(int ownedCount)
        {
            SnapToSelection();
            ApplyVisibility(ownedCount);
        }

        private void ApplyOwnedCount() =>
            ApplyVisibility(_weaponManager != null ? _weaponManager.OwnedCount : 0);

        /// <summary>
        /// 武器0本=非表示 / 1本以上=表示(documents/Specification.md §5。モードでは変えない)。
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
        /// 選択中の武器のパネルを中央へ置き直す(アニメーションなし)。入手直後・セーブ復元直後の初期表示合わせ。
        /// パネル未配線・アニメーション中は何もしない。
        /// </summary>
        private void SnapToSelection()
        {
            if (isAnimating || _slotPositions == null || panels == null || panels.Count == 0)
                return;

            int selected = _weaponManager != null ? _weaponManager.CurrentWeaponIndex : -1;
            if (selected < 0 || selected >= panels.Count)
                return;

            currentIndex = selected;
            for (int i = 0; i < _weaponCount; i++)
            {
                if (panels[i] == null)
                    continue;

                int slot = SlotOf(i);
                panels[i].anchoredPosition = _slotPositions[slot];

                var image = panels[i].GetComponent<Image>();
                if (image != null)
                    image.color = slot == 0 ? lightColor : darkColor;
            }
            ApplyLayerOrder();
        }

        private void HandleWeaponSwitched(bool isLeftRotation)
        {
            // パネル未配線・Start 前(_slotPositions 未初期化)は回すものが無いので何もしない。
            if (
                isAnimating
                || panels == null
                || panels.Count == 0
                || _slotPositions == null
                || _weaponCount < 2
            )
                return;
            MovePanels(isLeftRotation).Forget();
        }

        private async UniTask MovePanels(bool isLeftRotation)
        {
            isAnimating = true;

            // currentIndex 更新前にスロット角度を記録
            // スロット i の角度: pi/2 - i*(2pi/count)  スロット0=上=pi/2
            float arcStep = 2f * Mathf.PI / _weaponCount;
            float[] startAngles = new float[_weaponCount];
            for (int i = 0; i < _weaponCount; i++)
                startAngles[i] = Mathf.PI * 0.5f - SlotOf(i) * arcStep;

            // isLeftRotation=true  (Q/前の武器) → currentIndex-1 → 反時計回り(CCW)
            // isLeftRotation=false (E/次の武器) → currentIndex+1 → 時計回り(CW)
            if (isLeftRotation)
                currentIndex = (currentIndex - 1 + _weaponCount) % _weaponCount;
            else
                currentIndex = (currentIndex + 1) % _weaponCount;

            // E(CW)  → -arcStep（角度減少=時計回り）
            // Q(CCW) → +arcStep（角度増加=反時計回り）
            float deltaAngle = isLeftRotation ? arcStep : -arcStep;

            float[] endAngles = new float[_weaponCount];
            Color[] targetColors = new Color[_weaponCount];

            for (int i = 0; i < _weaponCount; i++)
            {
                endAngles[i] = startAngles[i] + deltaAngle;
                targetColors[i] = (SlotOf(i) == 0) ? lightColor : darkColor;

                // アニメーション開始時に即座に色を適用し、回転中は次に選択されるパネルのみが光るようにする
                panels[i].GetComponent<Image>().color = targetColors[i];
            }

            // アニメーション開始前にレイヤーを確定
            ApplyLayerOrder();

            // 楕円弧アニメーション: 角度 Lerp で各パネルが楕円弧を描いて移動
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = t * t * (3f - 2f * t);

                for (int i = 0; i < _weaponCount; i++)
                {
                    float angle = Mathf.Lerp(startAngles[i], endAngles[i], smoothT);
                    panels[i].anchoredPosition = new Vector2(
                        panelSpacingX * Mathf.Cos(angle),
                        panelSpacingY * Mathf.Sin(angle)
                    );
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // スナップ
            for (int i = 0; i < _weaponCount; i++)
            {
                panels[i].anchoredPosition = _slotPositions[SlotOf(i)];
                panels[i].GetComponent<Image>().color = targetColors[i];
            }

            // アニメーション終了後もレイヤーを再確定
            ApplyLayerOrder();

            isAnimating = false;
        }

        private int SlotOf(int panelIndex)
        {
            return (panelIndex - currentIndex + _weaponCount) % _weaponCount;
        }

        /// <summary>
        /// パネルの描画順を強制適用する。
        ///   switchUI              → 常に最前面（SetAsLastSibling）
        ///   選択中パネル(slot 0)   → switchUI の直下
        ///   slot n-1              → 最背面
        /// </summary>
        private void ApplyLayerOrder()
        {
            for (int i = 0; i < _weaponCount; i++)
            {
                int slot = SlotOf(i);
                panels[i].SetSiblingIndex(_weaponCount - 1 - slot);
            }

            if (switchUI != null)
                switchUI.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 全スロットを楕円上に均等配置。
        /// スロット i の角度 = pi/2 - i*(2pi/count)（スロット0が常に上=pi/2）。
        /// </summary>
        private Vector2[] CalculateSlotPositions(int count)
        {
            if (count == 0)
                return new Vector2[0];

            var positions = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 0.5f - i * (2f * Mathf.PI / count);
                positions[i] = new Vector2(
                    panelSpacingX * Mathf.Cos(angle),
                    panelSpacingY * Mathf.Sin(angle)
                );
            }
            return positions;
        }
    }
}
