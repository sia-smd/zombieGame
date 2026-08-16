// Polyfill for C# 9 records / init-only setters when targeting .NET Standard 2.1.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
  internal static class IsExternalInit { }
}
#endif
