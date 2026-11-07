using System;
using SoundKit;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 演出コマンド(sound.se / sound.bgm)のキーと SoundCue の対応。ConversationView に登録する。
    /// SoundCue 自体は Audio/SoundCue に置いたまま Inspector で割り当てる(SoundKit の使い方と同じ)。
    /// </summary>
    [Serializable]
    public struct DialogueSoundEntry
    {
        public string Key;
        public SoundCue Cue;
    }

    /// <summary>演出コマンド(background.image)のキーと背景画像の対応。ConversationView に登録する。</summary>
    [Serializable]
    public struct DialogueBackgroundEntry
    {
        public string Key;
        public Sprite Sprite;
    }
}
