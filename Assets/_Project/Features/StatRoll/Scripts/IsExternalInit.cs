namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// init アクセサのためのコンパイラ用シム。
    /// Unity 6(.NET Standard 2.1)には本型が無く、これが無いと
    /// init プロパティが CS0518 でコンパイルできないため定義する。
    /// </summary>
    internal static class IsExternalInit { }
}
