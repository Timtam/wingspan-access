// The reference assemblies in lib\interop expose UnityEngine.CoreModule's embedded NullableAttribute
// without its constructors. When the compiler needs the attribute (some lambdas) it would pick that
// definition and fail; a definition in this assembly takes precedence over referenced ones.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;

        public NullableAttribute(byte flag) => NullableFlags = new[] { flag };

        public NullableAttribute(byte[] flags) => NullableFlags = flags;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Interface |
        AttributeTargets.Method | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;

        public NullableContextAttribute(byte flag) => Flag = flag;
    }
}
