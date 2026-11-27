using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 弓を構えている間だけクロスヘアーを表示する。クロスヘアーの Image と同じ GameObject に置く。
    /// プレイヤーは入れ替わるので PlayerRegistry から受け取り、構えの変化を購読する。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CrosshairView : MonoBehaviour
    {
        private Image _image;
        private PlayerController _player;

        private void Awake() => _image = GetComponent<Image>();

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        private void Bind(PlayerController player)
        {
            if (_player != null)
                _player.AimingChanged -= Show;
            _player = player;
            if (_player != null)
                _player.AimingChanged += Show;
            Show(_player != null && _player.IsAiming);
        }

        private void Show(bool aiming) => _image.enabled = aiming;
    }
}
