using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftFlowViewUtility
    {
        public static void StopCraftRoutine(
            MonoBehaviour owner,
            ref Coroutine routine,
            ref bool isCrafting
        )
        {
            if (routine != null && owner != null)
            {
                owner.StopCoroutine(routine);
                routine = null;
            }

            isCrafting = false;
        }

        public static void CompleteCraftRoutine(ref Coroutine routine, ref bool isCrafting)
        {
            routine = null;
            isCrafting = false;
        }
    }
}
