using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標7: 硬直（すくみ）
    /// 恐怖への反応「戦う・逃げる・すくむ」のうち「すくむ」を測る。
    /// 交戦中の敵の攻撃が始まってから終わるまでの間、近くにいたのに移動・防御・攻撃・構えのどれも
    /// しなかったら1回と数える。
    ///
    /// 攻撃が終わった時点でまとめて判定するので、ぎりぎりまで引きつけてからパリィや回避をする上手な操作は数えない。
    /// 操作の押下（攻撃・防御）は PanicDetector が <see cref="RecordPlayerAction"/> で知らせる。
    /// </summary>
    [Serializable]
    public class FreezeSignal : PanicSignal
    {
        // 攻撃を待ち構えて最後まで動かない冷静な立ち回り（攻撃が届かず空振りする場合など）も含まれうるため、逃避より少し低くする
        private const float DefaultWeight = 0.6f;

        [Tooltip("攻撃中の敵がこの水平距離（m）以内に来た攻撃だけを数える。遠くの空振りに無反応なのは硬直ではないため")]
        [SerializeField]
        private float _threatDistance = 15f;

        [Tooltip("硬直を数える直近の時間幅（秒）")]
        [SerializeField]
        [Min(0.1f)]
        private float _window = 10f;

        [Tooltip("直近の時間幅でこの回数以上硬直したら満点")]
        [SerializeField]
        [Min(1)]
        private int _maxCount = 2;

        // 判定ウィンドウ内の硬直時刻（古い順）
        private readonly Queue<float> _freezeTimes = new Queue<float>();

        // 今見ている敵の攻撃1回分の状態
        private bool _isWatchingAttack;
        private bool _wasThreatened;
        private bool _hasActed;

        public FreezeSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Freeze;

        public override string DisplayName => "硬直";

        public override float Score => Mathf.Clamp01((float)_freezeTimes.Count / _maxCount);

        public override float RawValue => _freezeTimes.Count;

        public override string RawValueUnit => "直近回数";

        /// <summary>直近の時間幅での硬直回数</summary>
        public int Count => _freezeTimes.Count;

        /// <summary>攻撃・防御などのボタンが押された時に呼ぶ（敵の攻撃中なら「反応した」とみなす）。</summary>
        public void RecordPlayerAction()
        {
            if (_isWatchingAttack)
            {
                _hasActed = true;
            }
        }

        public override void Tick(PanicContext context, float deltaTime)
        {
            bool isAttacking = context.IsInCombat && context.IsEnemyAttacking;

            if (isAttacking)
            {
                if (!_isWatchingAttack)
                {
                    _isWatchingAttack = true;
                    _wasThreatened = false;
                    _hasActed = false;
                }

                if (context.EnemyDistance <= _threatDistance)
                {
                    _wasThreatened = true;
                }
                if (IsActing(context))
                {
                    _hasActed = true;
                }
            }
            else if (_isWatchingAttack)
            {
                // 敵の攻撃が終わった（または交戦が切れた）ので、この攻撃の間に何もしなかったかを確定する
                _isWatchingAttack = false;
                if (context.IsInCombat && _wasThreatened && !_hasActed)
                {
                    _freezeTimes.Enqueue(Time.time);
                    RaiseDetected();
                }
            }

            while (_freezeTimes.Count > 0 && Time.time - _freezeTimes.Peek() > _window)
            {
                _freezeTimes.Dequeue();
            }
        }

        public override void ResetState()
        {
            _freezeTimes.Clear();
            _isWatchingAttack = false;
            _wasThreatened = false;
            _hasActed = false;
        }

        // 押し続ける操作（移動・防御・構え）と、自分の攻撃モーション中。押した瞬間だけの操作は RecordPlayerAction で受け取る
        private static bool IsActing(PanicContext context)
        {
            return context.HasMoveInput
                || context.IsSubActionHeld
                || context.IsAiming
                || context.IsBusyWithOwnAction;
        }
    }
}
