using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace CreativeAI.Core
{
    /// <summary>
    /// エディタと開発ビルドだけで出すログ。リリースビルドでは呼び出しごと消え、引数の文字列も作られない。
    /// 被弾やステート遷移など、頻繁に通る場所の確認用ログに使う。
    /// </summary>
    public static class DevLog
    {
        /// <summary>エディタと開発ビルドでだけ <see cref="Debug.Log(object, UnityEngine.Object)"/> を出す。</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(object message, UnityEngine.Object context = null) =>
            Debug.Log(message, context);
    }
}
