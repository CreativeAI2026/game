namespace CreativeAI.UI
{
    public static class SlotKeyboardFocus
    {
        private static object _focusedOwner;

        public static void Claim(object owner)
        {
            _focusedOwner = owner;
        }

        public static bool IsFocused(object owner)
        {
            return owner != null && ReferenceEquals(_focusedOwner, owner);
        }

        public static void Release(object owner)
        {
            if (IsFocused(owner))
                _focusedOwner = null;
        }
    }
}
