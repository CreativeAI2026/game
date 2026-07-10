using System;
using System.Collections;
using System.Collections.Generic;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 会話UIの seam。実体は UI アセンブリ(CreativeAI.UI)で実装し、EventPlayer に注入する。
    /// Core を最下層に保つため、EventPlayer は具象UIではなくこの契約に依存する。
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>1行表示し、プレイヤーが送るまで待つ(コルーチン)。</summary>
        IEnumerator ShowLine(string speaker, string portrait, string text);

        /// <summary>選択肢を提示し、選ばれた値を onSelected で返す(コルーチン)。</summary>
        IEnumerator ShowChoice(IReadOnlyList<ChoiceOption> options, Action<string> onSelected);
    }
}
