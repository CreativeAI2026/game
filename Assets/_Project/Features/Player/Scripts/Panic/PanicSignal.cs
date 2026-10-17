using System;
using UnityEngine;

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
        [Tooltip(
            "この指標の信頼度（0〜1）。満点のとき総合焦り度をどこまで押し上げるか（1つだけ満点なら 重み×100）。\n"
                + "誤検知しやすい指標ほど低くする"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float _weight;

        /// <param name="defaultWeight">Inspector で未設定の時の重み。指標ごとの信頼度の初期値</param>
        protected PanicSignal(float defaultWeight)
        {
            _weight = defaultWeight;
        }

        /// <summary>この指標の信頼度（0〜1）。</summary>
        public float Weight => _weight;

        /// <summary>この指標が属する焦りの系統。</summary>
        public abstract PanicCategory Category { get; }

        /// <summary>デバッグ表示・シグナル通知に使う指標名。</summary>
        public abstract string DisplayName { get; }

        /// <summary>現在の強さ（0〜1）。</summary>
        public abstract float Score { get; }

        /// <summary>
        /// スコアに換算する前の計測値（継続秒・回/秒など）。スコアは満点で頭打ちになるため、
        /// しきい値の調整（PanicSessionLogger の記録）では満点を超えた分も見えるようにこちらを使う。
        /// </summary>
        public abstract float RawValue { get; }

        /// <summary><see cref="RawValue"/> の単位名（ログの列名に使う）。</summary>
        public abstract string RawValueUnit { get; }

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
