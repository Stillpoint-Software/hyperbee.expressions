using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Shared;

/// <summary>
/// Reads the internal <c>Expression.DebugView</c> property.
/// </summary>
/// <remarks>
/// <c>UnsafeAccessor</c> is a .NET 8 runtime feature with no netstandard2.1 equivalent:
/// it is resolved by the runtime, not the compiler, so no polyfill can supply it. On
/// targets that have it the accessor is used as before, with no reflection and no
/// delegate; on netstandard2.1 this falls back to a cached reflected getter.
/// </remarks>
internal static class ExpressionDebugView
{
#if NETSTANDARD
    private static readonly MethodInfo Getter = typeof( Expression )
        .GetProperty( "DebugView", BindingFlags.Instance | BindingFlags.NonPublic )
        ?.GetGetMethod( nonPublic: true );

    /// <summary>
    /// Gets the debug view, or a placeholder when the runtime hides the property.
    /// </summary>
    public static string Get( Expression expression )
    {
        // DebugView is internal and so not part of the contract. Returning a marker
        // beats throwing: the only caller is an opt-in diagnostic handler, and losing
        // the text must not fail a compile.
        if ( Getter == null )
            return "<DebugView unavailable on this runtime>";

        return (string) Getter.Invoke( expression, null );
    }
#else
    /// <summary>
    /// Gets the debug view.
    /// </summary>
    public static string Get( Expression expression ) => GetDebugView( expression );

    [UnsafeAccessor( UnsafeAccessorKind.Method, Name = "get_DebugView" )]
    private static extern string GetDebugView( Expression expression );
#endif
}
