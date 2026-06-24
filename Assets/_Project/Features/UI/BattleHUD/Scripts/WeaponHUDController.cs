using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private Vector2[] positions =
        {
            new Vector2(-122, 60),
            new Vector2(0, 0),
            new Vector2(122, 60),
        };
        private Color darkColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private Color lightColor = Color.white;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _input = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            if (isAnimating)
                return;
        }

        private async UniTask MovePanels(bool isLeftRotation)
        {
            isAnimating = true;

            // インデックス更新（循環させる）
            if (isLeftRotation)
                currentIndex = (currentIndex - 1 + panels.Count) % panels.Count;
            else
                currentIndex = (currentIndex + 1) % panels.Count;

            // アニメーション実行
            var sequence = DOTween.Sequence();
            for (int i = 0; i < panels.Count; i++)
            {
                // インデックスの差分に応じて配置を計算
                int targetIndex = (i - currentIndex + panels.Count) % panels.Count;

                sequence.Join(panels[i].DOAnchorPos(positions[targetIndex], duration));
                sequence.Join(
                    panels[i]
                        .GetComponent<Image>()
                        .DOColor(targetIndex == 1 ? lightColor : darkColor, duration)
                );
            }

            await sequence.ToUniTask();
            isAnimating = false;
        }
    }
}
