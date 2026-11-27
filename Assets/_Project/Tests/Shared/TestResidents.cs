using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace CreativeAI.Tests
{
    /// <summary>
    /// 静的 Instance を持つ常駐クラスをテスト用に立て、まとめて片付ける。
    /// EditMode では Awake が走らず Instance が立たないので差し込む。片付けを1つでも書き忘れると
    /// Instance が次のテストへ持ち越され、無関係なテストが落ちるため、登録したものは <see cref="Clear"/> で全部戻す。
    /// </summary>
    public sealed class TestResidents
    {
        private readonly List<Action> _cleanups = new();

        public T Add<T>()
            where T : UnityEngine.MonoBehaviour
        {
            var go = new UnityEngine.GameObject(typeof(T).Name);
            var resident = go.AddComponent<T>();
            TestReflection.SetStaticProperty("Instance", resident);
            _cleanups.Add(() =>
            {
                TestReflection.SetStaticProperty<T>("Instance", null);
                Object.DestroyImmediate(go);
            });
            return resident;
        }

        /// <summary>
        /// <see cref="Add{T}"/> と同じく立てたうえで OnEnable も呼び、片付けで OnDisable を呼ぶ。
        /// 有効になったときに他の常駐を購読するもの(装備・即時食材の枠など)に使う。購読先より後に立てること。
        /// </summary>
        public T AddEnabled<T>()
            where T : UnityEngine.MonoBehaviour
        {
            var resident = Add<T>();
            TestReflection.Invoke(resident, "OnEnable");
            _cleanups.Add(() => TestReflection.Invoke(resident, "OnDisable"));
            return resident;
        }

        /// <summary>
        /// パネルを1つ(セーブ)だけ持つ <see cref="CreativeAI.Gameplay.UIManager"/> を立てる。
        /// 会話UI を差し込むときは <c>_dialogueView</c> を書き換える。
        /// </summary>
        public CreativeAI.Gameplay.UIManager AddUIManager()
        {
            var ui = Add<CreativeAI.Gameplay.UIManager>();
            var panel = new UnityEngine.GameObject("TestPanel");
            panel.transform.SetParent(ui.transform);
            TestReflection.SetField(ui, "_saveUI", panel);
            TestReflection.Invoke(ui, "Awake"); // パネル登録 + 全閉じ
            return ui;
        }

        public void Clear()
        {
            for (int i = _cleanups.Count - 1; i >= 0; i--)
                _cleanups[i]();
            _cleanups.Clear();
        }
    }
}
