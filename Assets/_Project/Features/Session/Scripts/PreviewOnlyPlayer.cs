using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    [DefaultExecutionOrder(10000)]
    public sealed class PreviewOnlyPlayer : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new();

        private void OnEnable() => Hide();

        private void LateUpdate() => Hide();

        private void Hide()
        {
            GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
                renderer.forceRenderingOff = true;
        }
    }
}
