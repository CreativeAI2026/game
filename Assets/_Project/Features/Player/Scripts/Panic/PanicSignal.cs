using System;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度を構成する1つの指標。設定値（Inspector に出す）と計測中の状態をまとめて持つ。
    /// 毎フレームの計測は <see cref="Tick"/> で行い、入力や被弾などのイベントで数える指標は
    /// 派生クラス側の記録メソッドを PanicDetector が呼ぶ。
    /// </summary>
    [Serializable]
    public abstract class PanicSignal
    {
        /// <summary>デバッグ表示・シグナル通知に使う指標名。</summary>
        public abstract string DisplayName { get; }

        /// <summary>現在の強さ（0〜1）。</summary>
        public abstract float Score { get; }

        /// <summary>焦りの兆候を検知した時に発火する。継続する指標は兆候が出始めた瞬間に1回だけ発火する。</summary>
        public event Action<PanicSignal> Detected;

        /// <summary>1フレーム分の計測を行う。イベントだけで数える指標は時間経過の処理だけを行う。</summary>
        public abstract void Tick(PanicContext context, float deltaTime);

        /// <summary>計測中の状態をすべて初期化する（設定値はそのまま）。</summary>
        public abstract void ResetState();

        protected void RaiseDetected()
        {
            Detected?.Invoke(this);
        }

        /// <summary>
        /// 継続する兆候が「出始めた瞬間」だけ通知する。毎フレーム通知すると購読側が処理しきれないため。
        /// </summary>
        protected void UpdateActiveState(ref bool isActive, bool isActiveNow)
        {
            if (isActiveNow && !isActive)
            {
                RaiseDetected();
            }
            isActive = isActiveNow;
        }
    }
}
