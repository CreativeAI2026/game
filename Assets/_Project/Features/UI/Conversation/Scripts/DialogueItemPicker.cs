using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// useItem の持ち物ピッカー。持ち物をアイコンのグリッドで並べ、選択中の名前と所持数を下に出し、「やめる」を置く。
    /// 選択はマウスと、上下左右 + 決定 / キャンセルの入力。使わない間は非表示。
    /// </summary>
    public sealed class DialogueItemPicker : MonoBehaviour
    {
        private const string DefaultPrompt = "何を使う?";
        private const string EmptyMessage = "使えそうな持ち物がない。";

        [SerializeField]
        private TMP_Text _prompt;

        [SerializeField]
        private RectTransform _viewport;

        [SerializeField]
        private GridLayoutGroup _grid; // 持ち物のマスを並べる親

        [SerializeField]
        private TMP_Text _nameText; // 選択中の持ち物の名前と所持数

        [SerializeField]
        private DialogueItemPickerCell _cellTemplate;

        [SerializeField]
        private DialogueItemPickerCell _cancel;

        private readonly List<DialogueItemPickerCell> _cells = new(); // 添字は ItemPickerGridNavigation と同じ(最後が「やめる」)
        private readonly List<Action> _actions = new();
        private readonly List<string> _names = new(); // 持ち物ごとの「名前 ×所持数」。「やめる」は含めない
        private int _selectedIndex = -1;
        private string _rememberedKey; // 直前に選んだ itemKey。会話を閉じるまで覚える

        private int ItemCount => _names.Count;
        private RectTransform Content => (RectTransform)_grid.transform;

        /// <summary>
        /// 一覧を出す。選ぶと選んだ itemKey(キーを持たないアイテムは空文字)、やめるなら null で <paramref name="onPicked"/> を呼ぶ。
        /// 持ち物が無くても「やめる」だけは出す。
        /// </summary>
        internal void Show(
            string prompt,
            IReadOnlyList<ItemPickerEntries.Entry> entries,
            Action<string> onPicked
        )
        {
            Clear();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _prompt.text = string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt;
            Content.anchoredPosition = Vector2.zero;

            // 呼び出し元の再入を防ぐ: 一度選んだら以降の選択は無視する。
            bool picked = false;
            void Pick(string value)
            {
                if (picked)
                    return;
                picked = true;
                if (value != null)
                    _rememberedKey = value; // 選び直しのとき、さっき選んだ物にカーソルを合わせる
                onPicked?.Invoke(value);
            }

            foreach (var entry in entries ?? Array.Empty<ItemPickerEntries.Entry>())
            {
                var cell = Instantiate(_cellTemplate, Content);
                cell.gameObject.SetActive(true);
                cell.Set(entry);
                string key = entry.ItemKey;
                Add(cell, () => Pick(key));
                _names.Add($"{entry.Name}  ×{entry.Count}");
            }
            Add(_cancel, () => Pick(null));

            Select(StartIndex(entries));
        }

        /// <summary>上下左右で選択を動かし、決定で選び、キャンセルで「やめる」を選ぶ。毎フレーム呼ぶ。</summary>
        public void HandleKeyboardInput()
        {
            if (!gameObject.activeSelf || _actions.Count == 0)
                return;

            if (UIInputActionMap.WasCancelPressedThisFrame())
            {
                _actions[^1].Invoke();
                return;
            }
            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
                Select(
                    ItemPickerGridNavigation.Move(
                        _selectedIndex,
                        step,
                        ItemCount,
                        _grid.constraintCount
                    )
                );
            if (UIInputActionMap.WasSubmitPressedThisFrame() && _selectedIndex >= 0)
                _actions[_selectedIndex].Invoke();
        }

        /// <summary>パネルを片付け、選んだ位置の記憶も忘れる(会話を閉じるとき)。</summary>
        public void Dismiss()
        {
            Clear();
            _rememberedKey = null;
        }

        /// <summary>パネルを片付けて隠す。</summary>
        public void Clear()
        {
            foreach (var cell in _cells)
                if (cell != _cancel)
                    Destroy(cell.gameObject);
            _cells.Clear();
            _actions.Clear();
            _names.Clear();
            _selectedIndex = -1;
            gameObject.SetActive(false);
        }

        private void Add(DialogueItemPickerCell cell, Action onClick)
        {
            int index = _cells.Count;
            // マウスを乗せたら、その位置を選択する(キーボードの選択と同じ見た目にそろえる)。
            // 色と名前だけ変える。スクロール位置は動かさない(動かすと、止めたマウスの下に別のマスが来て連鎖する)。
            cell.Bind(onClick, () => Select(index, false));
            _cells.Add(cell);
            _actions.Add(onClick);
        }

        // 開いたときにカーソルを置く位置。さっき選んだ物があればそこ、なければ先頭。
        private int StartIndex(IReadOnlyList<ItemPickerEntries.Entry> entries)
        {
            if (_rememberedKey == null || entries == null)
                return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ItemKey == _rememberedKey)
                    return i;
            }
            return 0;
        }

        // 選択位置を変える。scroll が true のときだけ、選んだマスが見える範囲に入るよう最小限スクロールする
        // (キーボード用。マウスのホバーでは色と名前だけ変える)。
        private void Select(int index, bool scroll = true)
        {
            if (_cells.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, _cells.Count - 1);
            _selectedIndex = index;
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetFocused(i == index);
            _nameText.text =
                index < ItemCount ? _names[index] : (ItemCount == 0 ? EmptyMessage : string.Empty);

            if (scroll && index < ItemCount)
                ScrollToKeepVisible(index);
        }

        private void ScrollToKeepVisible(int index)
        {
            int columns = _grid.constraintCount;
            float cellHeight = _grid.cellSize.y;
            float pitch = cellHeight + _grid.spacing.y;
            float viewportHeight = _viewport.rect.height;
            int rows = (ItemCount + columns - 1) / columns;
            float rowTop = index / columns * pitch;
            float rowBottom = rowTop + cellHeight;
            float contentHeight = rows * pitch - _grid.spacing.y;
            float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);

            // Content は上端基準で、anchoredPosition.y が下へスクロールした量になる。
            float offset = Content.anchoredPosition.y;
            if (rowTop < offset)
                offset = rowTop;
            else if (rowBottom > offset + viewportHeight)
                offset = rowBottom - viewportHeight;
            Content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, maxOffset));
        }
    }

    /// <summary>
    /// useItem の持ち物ピッカーの選択位置を、上下左右の入力で動かす計算。
    /// 添字 0..itemCount-1 がグリッドの各マス(左上から行ごと)、itemCount が「やめる」。
    /// </summary>
    internal static class ItemPickerGridNavigation
    {
        /// <summary>
        /// 入力 <paramref name="step"/>(y が正で上)で動いた後の添字を返す。動けない向きなら current のまま。
        /// 縦の入力があるときは横を見ない。行をまたぐ回り込みはしない。
        /// </summary>
        public static int Move(int current, Vector2Int step, int itemCount, int columns)
        {
            int cancel = itemCount;
            if (itemCount <= 0 || columns <= 0)
                return cancel;

            if (current >= cancel)
                return step.y > 0 ? itemCount - 1 : cancel;

            int lastRow = (itemCount - 1) / columns;
            int row = current / columns;
            int column = current % columns;

            if (step.y > 0)
                return row > 0 ? current - columns : current;
            if (step.y < 0)
                return row < lastRow ? Mathf.Min(current + columns, itemCount - 1) : cancel;
            if (step.x > 0)
                return column < columns - 1 && current + 1 < itemCount ? current + 1 : current;
            if (step.x < 0)
                return column > 0 ? current - 1 : current;
            return current;
        }
    }
}
