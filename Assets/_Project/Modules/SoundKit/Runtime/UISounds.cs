using System;
using Object = UnityEngine.Object;

namespace SoundKit
{
    /// <summary>
    /// 共通の UI 音（選択・決定・戻る）を鳴らす入口。音そのものは UISoundSet が持つ。
    /// uGUI のボタンは UIButtonSound が自動で鳴らすので、ここを直接呼ぶのはボタンを使わない操作
    /// （キャンセルキーで閉じる、左右キーで項目を切り替えるなど）だけ。
    /// </summary>
    public static class UISounds
    {
        public static void PlaySelect(Object owner) => Play(set => set.Select, owner, "選択音");

        public static void PlayDecide(Object owner) => Play(set => set.Decide, owner, "決定音");

        public static void PlayBack(Object owner) => Play(set => set.Back, owner, "戻る音");

        private static void Play(Func<UISoundSet, SoundCue> pick, Object owner, string what)
        {
            var soundSet = UISoundSet.Instance;
            SoundCue cue = soundSet != null ? pick(soundSet) : null;
            if (cue == null || !cue.HasClip)
            {
                UnassignedSoundWarning.WarnOnce(owner, $"UI の{what}（Resources/UISoundSet）");
                return;
            }
            SoundEffectPlayer.Play(cue, owner);
        }
    }
}
