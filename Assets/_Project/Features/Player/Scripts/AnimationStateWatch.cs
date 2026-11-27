using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Trigger を送ったアニメーションのステートに入ったか・入った後に抜けたかを見張る。
    /// 入れないまま猶予を過ぎた(遷移の設定漏れ)・終わりのイベントを受けないまま抜けた(イベントの付け忘れ)を
    /// ステート側で見分けて、操作不能のまま固まらないようにするのに使う。
    /// </summary>
    public struct AnimationStateWatch
    {
        private float _requestTime;
        private bool _hasStarted;

        /// <summary>見張りの結果。</summary>
        public enum Progress
        {
            /// <summary>まだ入っていない(猶予内)。</summary>
            Waiting,

            /// <summary>入れないまま猶予を過ぎた。</summary>
            TimedOut,

            /// <summary>今フレーム、初めてステートに入った。</summary>
            Started,

            /// <summary>前のフレームから続けてステートの中にいる。</summary>
            Playing,

            /// <summary>一度入った後、ステートから抜けた。</summary>
            Left,
        }

        /// <summary>Trigger を送った時点から見張り直す。</summary>
        public void Begin()
        {
            _requestTime = Time.time;
            _hasStarted = false;
        }

        /// <summary>
        /// 今フレームの様子を渡して進み具合を返す。
        /// </summary>
        /// <param name="isInState">今フレーム、目的のステートにいるか</param>
        /// <param name="enterTimeout">入るのを待つ猶予(秒)</param>
        public Progress Update(bool isInState, float enterTimeout)
        {
            if (isInState)
            {
                if (_hasStarted)
                    return Progress.Playing;
                _hasStarted = true;
                return Progress.Started;
            }

            if (_hasStarted)
                return Progress.Left;

            return Time.time - _requestTime > enterTimeout ? Progress.TimedOut : Progress.Waiting;
        }
    }
}
