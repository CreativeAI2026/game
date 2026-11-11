using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>クラゲボスのクリップに置くアニメーションイベントの種類。</summary>
    public enum KurageAnimationEvent
    {
        /// <summary>突きの触手が伸び始める(当たり判定を入れる)。</summary>
        StabHitStart,

        /// <summary>突きの触手が戻り始める(当たり判定を切る)。</summary>
        StabHitEnd,

        /// <summary>地面攻撃で触手をポータルへ突っ込んだ。ここから地面に出現位置を出す。</summary>
        TentaclesDive,

        /// <summary>輪っか攻撃で触手が頭上のわっかを掴んだ。</summary>
        RingGrab,

        /// <summary>輪っかを投げ上げた。</summary>
        RingThrow,

        /// <summary>突きの触手を伸ばす直前。先端に閃きを出す。</summary>
        StabGlint,
    }

    /// <summary>
    /// アニメーションイベントは Animator と同じ GameObject のスクリプトしか呼べない。
    /// クラゲはモデル(FBX)の子に Animator があるため、ここで受けて親の <see cref="KurageBossController"/> へ渡す。
    /// クリップのイベントは関数名 OnKurageEvent、引数(Int)に <see cref="KurageAnimationEvent"/> の値を入れる。
    /// </summary>
    public class KurageAnimationEventRelay : MonoBehaviour
    {
        private KurageBossController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<KurageBossController>();
            Assert.IsNotNull(_controller, $"{name}: 親に KurageBossController がありません");
        }

        public void OnKurageEvent(int eventId)
        {
            _controller.HandleAnimationEvent((KurageAnimationEvent)eventId);
        }
    }
}
