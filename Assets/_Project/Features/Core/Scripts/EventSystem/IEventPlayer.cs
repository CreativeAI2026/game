namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 会話イベント再生の指揮役。EventTrigger が条件成立時に発火を託す。
    /// 実際の非同期シグネチャ(UniTask / CancellationToken)は EventPlayer 実装時に確定する。
    /// </summary>
    public interface IEventPlayer
    {
        void Play(EventDefinition ev);
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
}
