using System.Collections.Generic;
using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// マップに出すピンの記号を、表示中のピンの数だけ型から複製して使い回す。ミニマップと全画面マップで共用する。
    /// 位置の決め方と「いま見ている階か」はマップ側(<see cref="IPlacement"/>)が決め、別の階のピンは薄く出す。
    /// </summary>
    public sealed class MapPinIconPool
    {
        /// <summary>ピンの記号の置き方。マップごとに実装する。</summary>
        public interface IPlacement
        {
            /// <summary>ワールド座標のピンを、記号の anchoredPosition に変える。</summary>
            Vector2 ToIconPosition(Vector3 world);

            /// <summary>階の番号(<see cref="MinimapLayout.FindLevel"/> の結果)が、いま見ている階に当たるか。</summary>
            bool IsShownLevel(int levelIndex);
        }

        // 記号1つ分。ピンは置いた後に動かないので、どの階にあるかは担当のピンが替わったときだけ調べ直す
        private struct Slot
        {
            public MinimapIconGraphic Icon;
            public MapPin Pin;
            public Vector3 Anchor;
            public int Level;
        }

        private readonly MinimapIconGraphic _template;
        private readonly List<Slot> _slots = new();

        /// <summary>型を受け取り、型そのものは隠しておく。型が無ければ何も出さない。</summary>
        public MapPinIconPool(MinimapIconGraphic template)
        {
            _template = template;
            if (_template != null)
                _template.gameObject.SetActive(false);
        }

        /// <summary>表示中のピンの数だけ記号を出して置き、余った記号を隠す。</summary>
        public void Refresh(MinimapLayout layout, IPlacement placement, float otherLevelAlpha)
        {
            if (_template == null)
                return;

            var shown = 0;
            // IReadOnlyList の foreach は毎フレーム数え上げ用のオブジェクトを作るので、添字で回す
            var active = MapPin.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var pin = active[i];
                if (pin == null || !pin.Shown)
                    continue;

                var slot = GetSlot(shown);
                var anchor = pin.Anchor;
                if (slot.Pin != pin || slot.Anchor != anchor)
                {
                    slot.Pin = pin;
                    slot.Anchor = anchor;
                    slot.Level = layout.FindLevel(anchor);
                    _slots[shown] = slot;
                }
                shown++;

                slot.Icon.rectTransform.anchoredPosition = placement.ToIconPosition(anchor);
                var tint = slot.Icon.color;
                tint.a = placement.IsShownLevel(slot.Level) ? 1f : otherLevelAlpha;
                slot.Icon.color = tint;
            }

            for (var i = shown; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                slot.Icon.gameObject.SetActive(false);
                slot.Pin = null; // 消えたピンを持ち続けない
                _slots[i] = slot;
            }
        }

        private Slot GetSlot(int index)
        {
            while (_slots.Count <= index)
                _slots.Add(
                    new Slot
                    {
                        Icon = Object.Instantiate(_template, _template.transform.parent, false),
                    }
                );
            var slot = _slots[index];
            slot.Icon.gameObject.SetActive(true);
            return slot;
        }
    }
}
