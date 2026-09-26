namespace CreativeAI.Core.Interaction
{
    /// <summary>
    /// 近づいた対象の操作プロンプトを1つだけ出す静的サービス。ワールド側(Gameplay)は UI を参照できないので、
    /// ワールド側は出す/消すだけ、UI 側は購読して描くだけにする。
    /// 最後に Show した対象が勝ち、<see cref="Hide"/> は自分が出しているときだけ消す。
    /// </summary>
    public static class InteractPromptService
    {
        /// <summary>いま出ているラベル。何も出ていなければ null。</summary>
        public static string Label { get; private set; }

        /// <summary>いまプロンプトを出している対象(重複表示の調停用)。</summary>
        public static object Owner { get; private set; }

        /// <summary>ラベルが変わった(消えた場合は null)。</summary>
        public static event System.Action<string> LabelChanged;

        public static void Show(object owner, string label)
        {
            if (owner == null || string.IsNullOrEmpty(label))
                return;
            if (ReferenceEquals(Owner, owner) && Label == label)
                return;
            Owner = owner;
            Label = label;
            LabelChanged?.Invoke(Label);
        }

        /// <summary>自分が出しているプロンプトを消す(他人のものには触らない)。</summary>
        public static void Hide(object owner)
        {
            if (owner == null || !ReferenceEquals(Owner, owner))
                return;
            Owner = null;
            Label = null;
            LabelChanged?.Invoke(null);
        }

        /// <summary>シーン遷移などで持ち主ごと消えたとき用の強制クリア。</summary>
        public static void Clear()
        {
            if (Owner == null && Label == null)
                return;
            Owner = null;
            Label = null;
            LabelChanged?.Invoke(null);
        }
    }
}
