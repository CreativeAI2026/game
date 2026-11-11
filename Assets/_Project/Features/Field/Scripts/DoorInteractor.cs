using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 扉に近づくと「扉を開ける」を出し、キーで開閉する。近接判定とプロンプトは <see cref="ProximityInteractor"/> が受け持つ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorInteractor : ProximityInteractor
    {
        [Tooltip("開閉する扉。未設定なら同じ GameObject から探す。")]
        [SerializeField]
        private SwingDoor _door;

        [Tooltip("閉じているときに出す文言。")]
        [SerializeField]
        private string _openLabel = "扉を開ける";

        [Tooltip("開いているときに出す文言。")]
        [SerializeField]
        private string _closeLabel = "扉を閉じる";

        protected override bool HasTarget => _door != null;

        protected override string ActionLabel => _door.IsOpen ? _closeLabel : _openLabel;

        private void Awake()
        {
            if (_door == null)
                TryGetComponent(out _door);
        }

        protected override void Perform() => _door.Toggle();
    }
}
