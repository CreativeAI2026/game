using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// エイム中のみクロスヘアーImageを表示するコンポーネント。
    ///
    /// 使い方：
    ///   1. Screen Space - Overlay の Canvas に Image（クロスヘアー画像）を配置する。
    ///   2. この CrosshairUI コンポーネントを同じ GameObject にアタッチする。
    ///   3. _crosshairImage に Image コンポーネントをアサインする。
    ///   4. _input に PlayerArmature の PlayerInputHandler をアサインする。
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Tooltip("クロスヘアーとして表示するImageコンポーネント")]
        [SerializeField]
        private Image _crosshairImage;

        [Tooltip("PlayerArmature の PlayerInputHandler")]
        [SerializeField]
        private PlayerInputHandler _input;

        private void Start()
        {
            // 初期状態は非表示
            SetVisible(false);
        }

        private void Update()
        {
            if (_input == null)
                return;
            SetVisible(_input.subAction);
        }

        private void SetVisible(bool visible)
        {
            if (_crosshairImage != null)
                _crosshairImage.enabled = visible;
        }
    }
}
