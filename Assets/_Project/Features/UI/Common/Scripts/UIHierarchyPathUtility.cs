using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    public static class UIHierarchyPathUtility
    {
        public static string GetPath(Transform target)
        {
            if (target == null)
                return "<null>";

            var names = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
                names.Push(current.name);

            return string.Join("/", names);
        }
    }
}
