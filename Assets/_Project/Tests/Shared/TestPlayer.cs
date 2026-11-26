using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.Tests
{
    /// <summary>
    /// テスト用のプレイヤー。PlayerController を付けて PlayerRegistry に登録する
    /// (EditMode では Awake が走らず、自分では登録されないため)。
    /// </summary>
    public static class TestPlayer
    {
        /// <summary>プレイヤーを作って登録する。</summary>
        public static GameObject Create(string name = "TestPlayer")
        {
            var go = new GameObject(name) { tag = "Player" };
            PlayerRegistry.Register(go.AddComponent<PlayerController>());
            return go;
        }

        /// <summary>登録を外して破棄する。</summary>
        public static void Destroy(GameObject player)
        {
            if (player == null)
                return;
            if (player.TryGetComponent(out PlayerController controller))
                PlayerRegistry.Unregister(controller);
            Object.DestroyImmediate(player);
        }
    }
}
