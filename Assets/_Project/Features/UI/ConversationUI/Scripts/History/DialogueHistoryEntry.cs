using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    internal enum DialogueHistoryEntryKind
    {
        Dialogue,
        Choice,
        Narration,
        RewardItem,
        RewardWeapon,
    }

    internal readonly struct DialogueHistoryEntry
    {
        public DialogueHistoryEntry(
            string speaker,
            string body,
            Sprite portrait,
            DialoguePortraitSide side,
            bool portraitObscured,
            int sequence
        )
        {
            Speaker = speaker;
            Body = body;
            Portrait = portrait;
            Side = side;
            PortraitObscured = portraitObscured;
            Kind = string.IsNullOrWhiteSpace(speaker)
                ? DialogueHistoryEntryKind.Narration
                : DialogueHistoryEntryKind.Dialogue;
            Sequence = sequence;
        }

        public DialogueHistoryEntry(string choiceText, int sequence)
        {
            Speaker = string.Empty;
            Body = choiceText;
            Portrait = null;
            Side = DialoguePortraitSide.Left;
            PortraitObscured = false;
            Kind = DialogueHistoryEntryKind.Choice;
            Sequence = sequence;
        }

        public DialogueHistoryEntry(string rewardText, bool weapon, int sequence)
        {
            Speaker = string.Empty;
            Body = rewardText;
            Portrait = null;
            Side = DialoguePortraitSide.Left;
            PortraitObscured = false;
            Kind = weapon
                ? DialogueHistoryEntryKind.RewardWeapon
                : DialogueHistoryEntryKind.RewardItem;
            Sequence = sequence;
        }

        public string Speaker { get; }
        public string Body { get; }
        public Sprite Portrait { get; }
        public DialoguePortraitSide Side { get; }
        public bool PortraitObscured { get; }
        public DialogueHistoryEntryKind Kind { get; }
        public int Sequence { get; }
    }
}
