using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI
{
    /// <summary>
    /// 持ち物ピッカーのマス・ボタンにマウスが乗ったことを知らせる。EventTrigger はスクロールやドラッグまで受け取って
    /// 親の ScrollRect に届かなくしてしまうため、乗ったときだけを受ける専用の部品にしている。
    /// </summary>
    internal sealed class ItemPickerRowHover : MonoBehaviour, IPointerEnterHandler
    {
        private Action _onEnter;

        /// <summary>マウスが乗ったときに呼ぶ処理を設定する。</summary>
        public void Configure(Action onEnter) => _onEnter = onEnter;

        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();
    }
}
