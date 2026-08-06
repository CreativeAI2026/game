using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 武器切替UI(documents/Specification.md §5)。選択中の武器を中央に置くインラインHUD。
    ///
    /// 出し分けはモードではなく<b>武器の所持本数</b>: 0本で非表示・1本以上で表示(表示中は移動中・戦闘中とも)。
    /// GameObject ごと消すと <see cref="WeaponManager"/> の購読が切れて戻れないため、
    /// HudIconBar と同じく Canvas / GraphicRaycaster の enabled で出し入れする。
    /// </summary>
    public class WeaponHUDController : MonoBehaviour
    {
        [SerializeField]
        private List<RectTransform> panels; // 左・中央・右の順にセット

        [SerializeField]
        private float duration = 0.3f;

        [Header("所持本数で出し入れする自分の Canvas(0本=非表示)")]
        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private GraphicRaycaster _raycaster;

        private int currentIndex = 1; // 最初は中央
        private bool isAnimating = false;

        private PlayerInput _playerInput;
        private PlayerInputHandler _input;

        // 位置とカラーの定義
        private Vector2[] positions;
        private Color darkColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private Color lightColor = Color.white;

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
            positions = new Vector2[panels.Count];
            for (int i = 0; i < panels.Count; i++)
            {
                positions[i] = panels[i].anchoredPosition;

                panels[i].GetComponent<Image>().color =
                    (i == currentIndex) ? lightColor : darkColor;
                if (i == currentIndex)
                {
                    panels[i].SetAsLastSibling();
                }
            }

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
            if (isAnimating || positions == null || panels == null || panels.Count == 0)
                return;

            int selected = _weaponManager != null ? _weaponManager.CurrentWeaponIndex : -1;
            if (selected < 0 || selected >= panels.Count)
                return;

            currentIndex = selected;
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] == null)
                    continue;

                int targetIndex = (i - currentIndex + 1 + panels.Count) % panels.Count;
                panels[i].anchoredPosition = positions[targetIndex];

                var image = panels[i].GetComponent<Image>();
                if (image != null)
                    image.color = targetIndex == 1 ? lightColor : darkColor;
                if (targetIndex == 1)
                    panels[i].SetAsLastSibling();
            }
        }

        private void HandleWeaponSwitched(bool isLeftRotation)
        {
            // パネル未配線・Start 前(positions 未初期化)は回すものが無いので何もしない。
            if (isAnimating || panels == null || panels.Count == 0 || positions == null)
                return;
            MovePanels(isLeftRotation).Forget();
        }

        private async UniTask MovePanels(bool isLeftRotation)
        {
            isAnimating = true;

            if (isLeftRotation)
                currentIndex = (currentIndex + 1 + panels.Count) % panels.Count;
            else
                currentIndex = (currentIndex - 1) % panels.Count;

            // アニメーション開始前に各パネルの現在の位置・色を記録
            var startPositions = new Vector2[panels.Count];
            var startColors = new Color[panels.Count];
            var targetPositions = new Vector2[panels.Count];
            var targetColors = new Color[panels.Count];

            for (int i = 0; i < panels.Count; i++)
            {
                // currentIndexのパネルが中央（targetIndex=1）に来るように計算
                int targetIndex = (i - currentIndex + 1 + panels.Count) % panels.Count;

                startPositions[i] = panels[i].anchoredPosition;
                startColors[i] = panels[i].GetComponent<Image>().color;

                targetPositions[i] = positions[targetIndex];
                targetColors[i] = targetIndex == 1 ? lightColor : darkColor;

                if (targetIndex == 1)
                {
                    panels[i].SetAsLastSibling();
                }
            }

            // UniTask を使いフレームごとに Lerp でアニメーション
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // イーズ：SmoothStep（加速→減速）
                float smoothT = t * t * (3f - 2f * t);

                for (int i = 0; i < panels.Count; i++)
                {
                    panels[i].anchoredPosition = Vector2.Lerp(
                        startPositions[i],
                        targetPositions[i],
                        smoothT
                    );
                    panels[i].GetComponent<Image>().color = Color.Lerp(
                        startColors[i],
                        targetColors[i],
                        smoothT
                    );
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // アニメーション終了時に正確な値にスナップ
            for (int i = 0; i < panels.Count; i++)
            {
                panels[i].anchoredPosition = targetPositions[i];
                panels[i].GetComponent<Image>().color = targetColors[i];
            }

            isAnimating = false;
        }
    }
}
