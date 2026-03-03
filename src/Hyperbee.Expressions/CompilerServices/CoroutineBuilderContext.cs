#nullable enable

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Ambient and process-wide context for <see cref="ICoroutineDelegateBuilder"/> selection.
/// Follows the <see cref="System.Threading.SynchronizationContext.Current"/> naming convention.
/// </summary>
/// <remarks>
/// Compiler choice is never passed through <see cref="ExpressionRuntimeOptions"/>. Instead,
/// <see cref="AsyncStateMachineBuilder"/> reads <see cref="Current"/> at reduction time:
/// <list type="bullet">
///   <item><b>Per-compilation ambient</b> — set by the outer compiler (e.g. <c>HyperbeeCompiler.Compile()</c>)
///   via <see cref="Exchange"/> in a save/restore pattern. Scoped to the current async task chain.</item>
///   <item><b>Process-wide default</b> — set at startup via <see cref="SetDefault"/> (or
///   <c>HyperbeeCompiler.UseAsDefault()</c>). Used when no per-compilation ambient is active.</item>
///   <item><b>Null</b> — System compiler handles MoveNext in the outer compilation context.</item>
/// </list>
/// </remarks>
public static class CoroutineBuilderContext
{
    // volatile: Ensures that reads on any CPU (including ARM) always see the latest write
    // from another thread. Interlocked.Exchange provides the write barrier; volatile provides
    // the read barrier. Both are needed together.
    private static volatile ICoroutineDelegateBuilder? _default;

    private static readonly AsyncLocal<ICoroutineDelegateBuilder?> _current = new();

    /// <summary>
    /// Gets the effective builder: per-compilation ambient wins over process-wide default.
    /// Returns <c>null</c> if neither has been set (System compiler handles MoveNext).
    /// </summary>
    public static ICoroutineDelegateBuilder? Current => _current.Value ?? _default;

    /// <summary>
    /// Sets the per-compilation ambient builder and returns a scope that restores the previous
    /// value on dispose. Preferred over <see cref="Exchange"/> for custom <c>IExpressionCompiler</c>
    /// implementations.
    /// <code>
    /// using ( CoroutineBuilderContext.SetScope( myBuilder ) )
    /// {
    ///     /* compile */
    /// }
    /// </code>
    /// </summary>
    public static IDisposable SetScope( ICoroutineDelegateBuilder? builder )
    {
        var previous = Exchange( builder );
        return new Scope( previous );
    }

    /// <summary>
    /// Sets the per-compilation ambient builder and returns the previous raw <c>AsyncLocal</c> value.
    /// Prefer <see cref="SetScope"/> for automatic save/restore. Use <c>Exchange</c> when you need
    /// explicit control (e.g. <c>HyperbeeCompiler.Compile()</c> try/finally pattern).
    /// Returns the raw <c>AsyncLocal</c> value (not the computed <see cref="Current"/>).
    /// Restoring <c>null</c> clears the ambient, leaving only the process-wide default active.
    /// </summary>
    public static ICoroutineDelegateBuilder? Exchange( ICoroutineDelegateBuilder? builder )
    {
        var previous = _current.Value;  // raw AsyncLocal value, NOT _current.Value ?? _default
        _current.Value = builder;
        return previous;
    }

    /// <summary>
    /// Atomically sets the process-wide default builder and returns the previous default.
    /// Call at application startup or in <c>[AssemblyInitialize]</c> / <c>[ClassInitialize]</c>.
    /// </summary>
    public static ICoroutineDelegateBuilder? SetDefault( ICoroutineDelegateBuilder? builder ) =>
        Interlocked.Exchange( ref _default, builder );

    private sealed class Scope( ICoroutineDelegateBuilder? previous ) : IDisposable
    {
        public void Dispose() => Exchange( previous );
    }
}
