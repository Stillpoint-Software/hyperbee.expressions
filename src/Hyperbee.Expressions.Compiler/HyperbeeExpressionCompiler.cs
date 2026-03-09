using System.Linq.Expressions;
using Hyperbee.Expressions;
using Hyperbee.Expressions.CompilerServices;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// <see cref="IExpressionCompiler"/> implementation that uses the HEC IR pipeline.
/// Use this class for DI registration or as a singleton where an injectable compiler is needed.
/// </summary>
/// <remarks>
/// Delegates to <see cref="HyperbeeCompiler"/> for all compilation. The per-compilation ambient
/// (<see cref="CoroutineBuilderContext"/>) is set by <see cref="HyperbeeCompiler.Compile(LambdaExpression)"/>
/// automatically, so any <see cref="AsyncBlockExpression"/> encountered during compilation uses HEC
/// for MoveNext bodies without explicit configuration.
/// </remarks>
/// <example>
/// <code>
/// // DI registration:
/// services.AddSingleton&lt;IExpressionCompiler, HyperbeeExpressionCompiler&gt;();
///
/// // Or process-wide default:
/// HyperbeeExpressionCompiler.UseAsDefault();
/// </code>
/// </example>
public sealed class HyperbeeExpressionCompiler : IExpressionCompiler
{
    /// <summary>Singleton instance.</summary>
    public static readonly IExpressionCompiler Instance = new HyperbeeExpressionCompiler();

    private HyperbeeExpressionCompiler() { }

    /// <inheritdoc/>
    public Delegate Compile( LambdaExpression lambda ) => HyperbeeCompiler.Compile( lambda );

    /// <inheritdoc/>
    public TDelegate Compile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate => HyperbeeCompiler.Compile( lambda );

    /// <inheritdoc/>
    public Delegate? TryCompile( LambdaExpression lambda ) => HyperbeeCompiler.TryCompile( lambda );

    /// <inheritdoc/>
    public TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate => HyperbeeCompiler.TryCompile( lambda );

    /// <summary>
    /// Sets HEC as the process-wide default <see cref="ICoroutineDelegateBuilder"/>.
    /// Returns the previous default (useful for test cleanup).
    /// Equivalent to <see cref="HyperbeeCompiler.UseAsDefault"/>.
    /// </summary>
    public static ICoroutineDelegateBuilder? UseAsDefault() => HyperbeeCompiler.UseAsDefault();
}
