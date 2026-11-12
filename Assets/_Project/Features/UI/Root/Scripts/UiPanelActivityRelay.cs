using System;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// パネルの GameObject がオン・オフになったことを知らせる。<see cref="UiRouter"/> が実行時に付ける。
    /// パネルが自分の戻るボタンなどで閉じても、<see cref="UiRouter"/> が毎フレーム見に行かずに済むようにするためのもの。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPanelActivityRelay : MonoBehaviour
    {
        /// <summary>オン・オフが切り替わったときに呼ばれる。</summary>
        public event Action Changed;

        private void OnEnable() => Changed?.Invoke();

        private void OnDisable() => Changed?.Invoke();
    }
}
