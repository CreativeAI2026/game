namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 会話イベント再生の指揮役。EventTrigger が条件成立時に発火を託す。
    /// 実際の非同期シグネチャ(UniTask / CancellationToken)は EventPlayer 実装時に確定する。
    /// </summary>
    public interface IEventPlayer
    {
        /// <summary>
        /// イベントを再生する。battle ステップがあれば <paramref name="battle"/> の Prefab を
        /// トリガー位置に出して戦う(敵未配線なら警告してスキップ)。battle が無いイベントでは
        /// <paramref name="battle"/> は使われない(default で可)。
        /// </summary>
        void Play(EventDefinition ev, BattleSetup battle = default);
    }

    /// <summary>
    /// 実行時に有効な IEventPlayer を Core 側へ登録する seam。EventPlayer が EnsureResident 時に
    /// 自身を登録し、EventTrigger は Inspector 未配線時のフォールバックとしてここを見る
    /// (ItemGiverService / BattleRunnerService と同じ思想)。EventPlayer は Title フローで常駐生成され
    /// シーンから drag 配線しないため、非常駐の EventTrigger はこの契約経由で受け取る。
    /// </summary>
    public static class EventPlayerService
    {
        public static IEventPlayer Current { get; set; }
    }

    /// <summary>
    /// 会話イベント再生中(= 操作不能)かどうかを UI に伝える seam。EventPlayer が再生の開始/終了で
    /// 更新し、HudIconBar が購読して会話中は右上ナビ(セーブ/インベ入口)を隠す
    /// (documents/Specification.md §2.2, §5: 会話UI中はセーブ・インベントリ使用不可)。
    /// </summary>
    public static class EventPlaybackService
    {
        public static bool IsPlaying { get; private set; }
        public static event System.Action<bool> PlayingChanged;

        public static void SetPlaying(bool playing)
        {
            if (IsPlaying == playing)
                return;
            IsPlaying = playing;
            PlayingChanged?.Invoke(playing);
        }
    }
}
