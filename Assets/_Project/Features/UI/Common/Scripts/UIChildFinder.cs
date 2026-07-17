using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public static class UIChildFinder
    {
        public static Transform Find(Transform root, string objectName)
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }

        public static GameObject FindGameObject(Transform root, string objectName)
        {
            var target = Find(root, objectName);
            return target != null ? target.gameObject : null;
        }

        public static Button FindButton(Transform root, string objectName)
        {
            return Find(root, objectName)?.GetComponent<Button>();
        }

        public static T FindComponent<T>(Transform root, string objectName)
            where T : Component
        {
            var target = Find(root, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }
    }

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
