using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被弾後の無敵状態を判定する。怯み中と、怯み終了後の猶予中は攻撃によるダメージを受け付けない。
    /// 猶予は終了時刻を記録して被弾時に比べるだけなので、毎フレームの処理は持たない。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHitInvincibility : MonoBehaviour
    {
        [Tooltip("怯み後の追加無敵時間（秒）")]
        [SerializeField]
        private float _graceDuration = 0.3f;

        // 猶予の無敵が切れる時刻(Time.time 基準)。ポーズで時間が止まると猶予も止まる
        private float _graceEndTime = float.NegativeInfinity;

        private PlayerController _controller;

        /// <summary>被弾を受け付けない状態か。怯み中と、怯み終了後の猶予中。</summary>
        public bool IsInvincible => _controller.IsFlinching || Time.time < _graceEndTime;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        /// <summary>怯みの終了時に呼ぶ。ここから猶予の無敵が始まる。</summary>
        public void StartGrace() => _graceEndTime = Time.time + _graceDuration;
    }
}
