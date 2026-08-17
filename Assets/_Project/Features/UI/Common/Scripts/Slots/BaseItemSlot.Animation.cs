using System;
using CreativeAI.Gameplay;

namespace CreativeAI.UI
{
    public abstract partial class BaseItemSlot
    {
        private const float ItemTransitionDuration = 0.2f;

        public void SetItemAnimated(ItemData item, int count = 1)
        {
            SetItem(item, count);
            IconView?.PlayAppear(ItemTransitionDuration);
            CountBadgeView?.AnimateAppear(ItemTransitionDuration);
        }

        public void ClearAnimated(Action onComplete = null)
        {
            KillItemTransition();
            _item = null;
            _count = 0;

            CountBadgeView?.PlayHide(ItemTransitionDuration);
            if (
                IconView == null
                || !IconView.PlayHide(
                    ItemTransitionDuration,
                    () =>
                    {
                        Clear();
                        onComplete?.Invoke();
                    }
                )
            )
            {
                Clear();
                onComplete?.Invoke();
            }
        }

        private void KillItemTransition()
        {
            IconView?.KillTween();
            CountBadgeView?.KillTween();
        }

        private void ResetItemVisuals()
        {
            IconView?.ResetVisual();
            CountBadgeView?.ResetVisual();
        }
    }
}
