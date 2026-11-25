#if NETSTANDARD
using System.Reflection;

// Everything netstandard2.1 needs from the compiler and from reflection that newer
// targets have in box. Hand-written rather than taken from the Polyfill package:
// Polyfill compiles all of its internal polyfills into the consuming assembly and the
// compiler does not strip unused internal types, which measured at 320,000 bytes
// against 6,656 for these shims. This repo packs three assemblies, so that is close to
// a megabyte of dead code across the netstandard2.1 assets. Only three things are
// actually used, and all three are small and stable.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Marker the compiler requires to emit an <c>init</c> accessor.
    /// </summary>
    /// <remarks>
    /// The compiler only needs this type to exist by name, so an empty internal class
    /// is the whole contract. Added in .NET 5.
    /// </remarks>
    internal static class IsExternalInit;

    /// <summary>
    /// Lets a parameter default to the caller's source text for another argument.
    /// </summary>
    /// <remarks>
    /// The compiler reads the parameter name and fills the argument in at the call site,
    /// so, as with <see cref="IsExternalInit"/>, only the shape matters. Added in .NET 6.
    /// </remarks>
    [AttributeUsage( AttributeTargets.Parameter, AllowMultiple = false, Inherited = false )]
    internal sealed class CallerArgumentExpressionAttribute( string parameterName ) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}

namespace Hyperbee.Expressions.Shared
{
    /// <summary>
    /// Stands in for the generic <c>MethodInfo.CreateDelegate&lt;T&gt;()</c> added in .NET 5.
    /// </summary>
    /// <remarks>
    /// netstandard2.1 has only the non-generic overload taking a <see cref="Type"/>. On
    /// targets that have the instance method it wins over this extension anyway, but the
    /// shim is compiled only for netstandard so there is never a second candidate.
    /// </remarks>
    internal static class MethodInfoExtensions
    {
        public static T CreateDelegate<T>( this MethodInfo method )
            where T : Delegate
            => (T) method.CreateDelegate( typeof( T ) );
    }
}
#endif
