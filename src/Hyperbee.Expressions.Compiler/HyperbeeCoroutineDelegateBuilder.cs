using System.Linq.Expressions;
using Hyperbee.Expressions.CompilerServices;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// Creates coroutine body delegates using the HEC IR pipeline.
/// Assign to <see cref="Hyperbee.Expressions.ExpressionRuntimeOptions.DelegateBuilder"/> to opt
/// into HEC-compiled coroutine bodies.
/// </summary>
/// <example>
/// <code>
/// var options = new ExpressionRuntimeOptions
/// {
///     DelegateBuilder = HyperbeeCoroutineDelegateBuilder.Instance
/// };
/// var block = BlockAsync( ..., options );
/// </code>
/// </example>
public sealed class HyperbeeCoroutineDelegateBuilder : ICoroutineDelegateBuilder
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly ICoroutineDelegateBuilder Instance = new HyperbeeCoroutineDelegateBuilder();

    private HyperbeeCoroutineDelegateBuilder() { }

    /// <inheritdoc/>
    public Delegate Create( LambdaExpression lambda ) => HyperbeeCompiler.Compile( lambda );
}
