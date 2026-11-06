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

        public void Clear()
        {
            for (int i = _cleanups.Count - 1; i >= 0; i--)
                _cleanups[i]();
            _cleanups.Clear();
        }
    }
}
