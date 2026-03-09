using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Internal plumbing interface — builds the coroutine execution expression for a given
/// async body. Reserved for Milestone 3 (CompileToMethod / Strategy B). Currently unused;
/// the default implementation is <see cref="AsyncStateMachineBuilder"/>.
/// Kept internal because the <see cref="AsyncLoweringTransformer"/> delegate type
/// is an internal implementation detail. The user-facing customization point is
/// <see cref="ICoroutineDelegateBuilder"/>.
/// </summary>
internal interface ICoroutineImplementationBuilder
{
    Expression Create( Type resultType, AsyncLoweringTransformer loweringTransformer, int id, ExpressionRuntimeOptions options );
}

/// <summary>
/// Creates the coroutine body delegate from a <see cref="LambdaExpression"/>.
/// The produced delegate is stored in the coroutine's entry-point field and invoked
/// each time the coroutine is resumed (e.g. the async state machine's MoveNext).
/// Implement this interface to plug in a custom compiler for the coroutine body
/// (e.g. <see cref="T:Hyperbee.Expressions.Compiler.HyperbeeCoroutineDelegateBuilder"/>
/// for HEC-compiled coroutine bodies).
/// </summary>
/// <remarks>
/// "Coroutine" is the CS term for the suspend/resume pattern that underlies both
/// async/await and yield-return. This abstraction is not tied to state machines —
/// it remains valid for runtime-native coroutine implementations (e.g. .NET 11+).
/// </remarks>
public interface ICoroutineDelegateBuilder
{
    Delegate Create( LambdaExpression lambda );
}
