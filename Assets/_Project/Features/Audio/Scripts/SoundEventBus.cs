using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 音イベントを発行・購読するための静的バスクラス。
    /// 発行側（足音、矢の着弾など）と受信側（敵AI）を疎結合に繋ぐ。
    /// 使い方:
    ///   発行 → SoundEventBus.Emit(new SoundEventData(...))
    ///   購読 → SoundEventBus.OnSoundEmitted += MyHandler
    ///   解除 → SoundEventBus.OnSoundEmitted -= MyHandler
    /// </summary>
    public static class SoundEventBus
    {
        /// <summary>
        /// 音イベントが発生したときに発行されるイベント。
        /// 引数として SoundEventData（種別・位置・半径）が渡される。
        /// </summary>
        public static event Action<SoundEventData> OnSoundEmitted;

        /// <summary>
        /// 音イベントを発行する。
        /// 発生源スクリプト（足音、矢の着弾など）から呼び出す。
        /// </summary>
        /// <param name="data">発行する音イベントデータ</param>
        public static void Emit(SoundEventData data)
        {
            OnSoundEmitted?.Invoke(data);
        }
    }
}
