using System.Collections.Generic;
using CreativeAI.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI.HUD
{
    /// <summary>
    /// Field シーン上の常駐 HUD。右上の Character / Inventory / Save ボタンから各パネルを直接開く。
    /// 後でアイコンスプライトに差し替え予定。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Button / Panel Containers")]
        [SerializeField]
        private Transform _buttonsRoot;

        [SerializeField]
        private Transform _panelsRoot;

        private readonly List<(Button button, UnityAction action)> _bindings = new();

        private void Awake()
        {
            if (_buttonsRoot == null || _panelsRoot == null)
                return;

            int pairCount = Mathf.Min(_buttonsRoot.childCount, _panelsRoot.childCount);
            for (int i = 0; i < pairCount; i++)
            {
                var button = _buttonsRoot.GetChild(i).GetComponent<Button>();
                var panel = _panelsRoot.GetChild(i).GetComponent<UIPanelStub>();
                if (button == null || panel == null)
                    continue;

                UnityAction action = panel.Open;
                button.onClick.AddListener(action);
                _bindings.Add((button, action));
            }

            if (_buttonsRoot.childCount != _panelsRoot.childCount)
            {
                Debug.LogWarning(
                    $"{nameof(HUDController)}: ボタン数とパネル数が一致していません。",
                    this
                );
            }
        }

        private void OnDestroy()
        {
            foreach (var binding in _bindings)
                binding.button.onClick.RemoveListener(binding.action);
            _bindings.Clear();
        }
    }
}
