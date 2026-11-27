using CreativeAI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests
{
    /// <summary>
    /// 手で組んだ会話UIに、Prefab と同じ4つの操作ボタン(AUTO / SKIP / SPEED / HIDE)を付ける。
    /// 会話UIは操作ボタンがそろっている前提で動くため、Prefab を使わないテストはこれで補う。
    /// </summary>
    public static class ConversationTestControls
    {
        public static void Attach(ConversationPanel view)
        {
            foreach (
                string field in new[]
                {
                    "_autoControl",
                    "_skipControl",
                    "_speedControl",
                    "_hideControl",
                }
            )
            {
                var go = new GameObject(
                    field,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button)
                );
                go.transform.SetParent(view.transform, false);
                TestReflection.SetField(view, field, go.AddComponent<ConversationControlButton>());
            }
        }
    }
}
