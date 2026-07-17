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
}
