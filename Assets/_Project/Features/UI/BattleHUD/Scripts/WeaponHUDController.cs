using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    public class WeaponHUDController : MonoBehaviour
    {
        [SerializeField]
        private List<RectTransform> panels; // 左・中央・右の順にセット

        [SerializeField]
        private float duration = 0.3f;

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
        }

        private void OnEnable()
        {
            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponSwitched += HandleWeaponSwitched;
            }
        }

        private void OnDisable()
        {
            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponSwitched -= HandleWeaponSwitched;
            }
        }

        private void HandleWeaponSwitched(bool isLeftRotation)
        {
            if (isAnimating)
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
