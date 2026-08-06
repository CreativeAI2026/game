using System;
using System.Reflection;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// EditMode テスト用の小さなリフレクションヘルパ。
    ///
    /// EditMode では Awake/Start/Update が走らないため、
    /// - Awake で立つ静的 Instance(ProgressManager 等)
    /// - Awake/Start で解決される private フィールド(Canvas 参照など)
    /// - Update から呼ばれる private メソッド
    /// をテストから直接触る必要がある。本番コードにテスト専用の口を増やさないための逃げ道。
    /// </summary>
    internal static class TestReflection
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, InstanceFlags);
                if (f != null)
                    return f;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, InstanceFlags);
                if (m != null)
                    return m;
            }
            throw new MissingMethodException(type.FullName, name);
        }

        public static void SetField(object target, string name, object value) =>
            FindField(target.GetType(), name).SetValue(target, value);

        public static T GetField<T>(object target, string name) =>
            (T)FindField(target.GetType(), name).GetValue(target);

        public static object Invoke(object target, string name, params object[] args) =>
            FindMethod(target.GetType(), name).Invoke(target, args);

        /// <summary>private set の静的プロパティ(Instance など)へ値を入れる。null で解除。</summary>
        public static void SetStaticProperty<T>(string name, T value) =>
            typeof(T)
                .GetProperty(name, BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { value });
    }
}
