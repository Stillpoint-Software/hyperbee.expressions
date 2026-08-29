using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
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

    /// <summary>
    /// Emits a coroutine body into the state machine's own method. Matches
    /// <see cref="ICoroutineMethodBuilder"/>, but this builder does not advertise the
    /// capability yet -- see the remarks on that interface for what is outstanding.
    /// </summary>
    public object[] Emit(
        IReadOnlyList<ParameterExpression> parameters,
        Expression body,
        Type returnType,
        MethodBuilder method,
        FieldInfo constantsField )
    {
        return HyperbeeCompiler.CompileToInstanceMethod( parameters, body, returnType, method, constantsField );
    }
}
