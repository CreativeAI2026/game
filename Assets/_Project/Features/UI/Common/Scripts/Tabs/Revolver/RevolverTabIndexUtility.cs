using UnityEngine;

namespace CreativeAI.UI
{
    public static class RevolverTabIndexUtility
    {
        public static int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return -1;

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        public static float SignedWrappedDistance(int itemIndex, float selectionPosition, int count)
        {
            if (count <= 0 || float.IsNaN(selectionPosition) || float.IsInfinity(selectionPosition))
                return 0f;

            float distance = itemIndex - selectionPosition;
            float halfCount = count * 0.5f;
            while (distance > halfCount)
                distance -= count;
            while (distance < -halfCount)
                distance += count;
            return distance;
        }

        // For an even item count, an exact tie consistently moves in the positive direction.
        public static int ShortestStep(int fromIndex, int toIndex, int count)
        {
            if (count <= 0)
                return 0;

            int from = WrapIndex(fromIndex, count);
            int to = WrapIndex(toIndex, count);
            int forward = WrapIndex(to - from, count);
            int halfCount = count / 2;
            return forward > halfCount ? forward - count : forward;
        }
    }
}
