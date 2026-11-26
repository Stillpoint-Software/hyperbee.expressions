#nullable enable

using System.Linq.Expressions;

namespace Hyperbee.Expressions;

/// <summary>
/// Abstracts the compilation of <see cref="LambdaExpression"/> trees into delegates.
/// Implement this interface to provide a DI-injectable expression compiler.
/// </summary>
/// <remarks>
/// Built-in implementations:
/// <list type="bullet">
///   <item><see cref="SystemExpressionCompiler"/> — wraps <see cref="LambdaExpression.Compile()"/></item>
///   <item><c>HyperbeeExpressionCompiler</c> in <c>Hyperbee.Expressions.Compiler</c> — uses the HEC IR pipeline</item>
/// </list>
/// Custom implementations that compile <see cref="AsyncBlockExpression"/> trees should use
/// <see cref="CompilerServices.CoroutineBuilderContext.SetScope"/> to scope the ambient
/// <see cref="CompilerServices.ICoroutineDelegateBuilder"/> for the duration of compilation.
/// </remarks>
public interface IExpressionCompiler
{
    /// <summary>Compiles the lambda. Throws on unsupported patterns.</summary>
    Delegate Compile( LambdaExpression lambda );

    /// <summary>Compiles the lambda. Throws on unsupported patterns.</summary>
    TDelegate Compile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;

    /// <summary>Compiles the lambda. Returns <c>null</c> on failure.</summary>
    Delegate? TryCompile( LambdaExpression lambda );

    /// <summary>Compiles the lambda. Returns <c>null</c> on failure.</summary>
    TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;
}

/// <summary>
/// <see cref="IExpressionCompiler"/> implementation that uses the System
/// (<see cref="LambdaExpression.Compile()"/>) compiler.
/// </summary>
public sealed class SystemExpressionCompiler : IExpressionCompiler
{
    /// <summary>Singleton instance.</summary>
    public static readonly IExpressionCompiler Instance = new SystemExpressionCompiler();

    private SystemExpressionCompiler() { }

    /// <inheritdoc/>
    public Delegate Compile( LambdaExpression lambda ) => lambda.Compile();

    /// <inheritdoc/>
    public TDelegate Compile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate => lambda.Compile();

    /// <inheritdoc/>
    public Delegate? TryCompile( LambdaExpression lambda )
    {
        try { return Compile( lambda ); }
        catch { return null; }
    }

    /// <inheritdoc/>
    public TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
    {
        try { return Compile( lambda ); }
        catch { return null; }
    }
}
