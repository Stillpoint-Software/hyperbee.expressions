using System.Collections.ObjectModel;

namespace Hyperbee.Expressions.Shared;

/// <summary>
/// An empty <see cref="ReadOnlyCollection{T}"/> singleton per element type.
/// </summary>
/// <remarks>
/// <c>ReadOnlyCollection&lt;T&gt;.Empty</c> arrived in .NET 8 and is a static member on a
/// framework type, so it cannot be polyfilled. On targets that have it this forwards to
/// the framework singleton, so the same instance is handed out; on netstandard2.1 it
/// caches one wrapper over an empty array, which is the same allocation the framework
/// makes once.
/// </remarks>
internal static class EmptyReadOnly<T>
{
    public static readonly ReadOnlyCollection<T> Collection =
#if NETSTANDARD
        new ReadOnlyCollection<T>( [] );
#else
        ReadOnlyCollection<T>.Empty;
#endif
}
