using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace CreativeAI.Tests
{
    /// <summary>
    /// テストから private フィールド・private メソッド・静的 Instance を触るためのリフレクションヘルパ。
    /// 本番コードにテスト専用の口を増やさないため、EditMode / PlayMode のテストはすべてこれを使う。
    /// 名前を変えるとコンパイルでは気づけないので、見つからないときはどの型のどの名前かを出して落とす。
    /// </summary>
    public static class TestReflection
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

        /// <summary>private メソッドを呼ぶ。中で投げた例外は包まずにそのまま投げ直す。</summary>
        public static object Invoke(object target, string name, params object[] args)
        {
            try
            {
                return FindMethod(target.GetType(), name).Invoke(target, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        /// <summary>private set の静的プロパティ(Instance など)へ値を入れる。null で解除。</summary>
        public static void SetStaticProperty<T>(string name, T value)
        {
            var setter = typeof(T)
                .GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.GetSetMethod(nonPublic: true);
            if (setter == null)
                throw new MissingMemberException(typeof(T).FullName, name);
            setter.Invoke(null, new object[] { value });
        }
    }
}
