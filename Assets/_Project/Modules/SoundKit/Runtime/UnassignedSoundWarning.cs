using System.Collections.Generic;
using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 音素材がまだ割り当てられていないときの警告。音素材が揃う前でも鳴らす処理を先に組めるよう、
    /// 未設定は止めずに警告だけにする。同じ内容を毎回出すとログが埋まるため1度だけにする。
    /// </summary>
    public static class UnassignedSoundWarning
    {
        private static readonly HashSet<string> _warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _warned.Clear();

        /// <param name="owner">どこから鳴らそうとしたか</param>
        /// <param name="what">何が未設定か（同じ owner でも別の音なら別に警告する）</param>
        public static void WarnOnce(Object owner, string what = null)
        {
            string ownerName = owner != null ? $"{owner.GetType().Name}（{owner.name}）" : "不明";
            string subject = string.IsNullOrEmpty(what) ? "音" : what;
            string message =
                $"[音] {ownerName} の{subject}が未設定のため鳴らさない。音素材ができたらインスペクタで割り当てること。";

            if (_warned.Add(message))
                Debug.LogWarning(message, owner);
        }
    }
}
