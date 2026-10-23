using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 検知が「焦り」と判定した場面1つ。ゲームの終了時の振り返りで、テストする人に見せて答えてもらう。
    /// 人は戦闘中の経過時間を覚えていないので、時刻ではなく画像と出来事の言葉で場面を思い出してもらう。
    /// </summary>
    public sealed class PanicMoment
    {
        /// <summary>この起動の中での通し番号（1から）。</summary>
        public int Index;

        /// <summary>判定した時刻（Time.time）。ログと突き合わせるためのもので、テストする人には見せない。</summary>
        public float Time;

        public PanicCategory Category;

        /// <summary>判定の中心になった指標の名前（「逃避」など）。</summary>
        public string MainSignal;

        /// <summary>場面の説明（「背を向けてボスから離れていたとき」など）。</summary>
        public string Description;

        /// <summary>直前の出来事（「ボスの鎌の攻撃の直後」など）。無ければ空。</summary>
        public string RecentEvent;

        public float PanicScore;

        /// <summary>保存した画像のファイル名（ログのフォルダからの相対パス）。保存できなければ空。</summary>
        public string ImageFileName = string.Empty;

        /// <summary>振り返りで見せる画像。撮影できなければ null。</summary>
        public Texture2D Thumbnail;

        /// <summary>振り返りで見せる文（説明に直前の出来事を添えたもの）。</summary>
        public string ReviewText =>
            string.IsNullOrEmpty(RecentEvent) ? Description : $"{Description}（{RecentEvent}）";
    }
}
