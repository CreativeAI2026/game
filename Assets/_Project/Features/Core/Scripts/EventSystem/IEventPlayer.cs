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
}
