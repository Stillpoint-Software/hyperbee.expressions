using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

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

/// <summary>
/// An optional capability on an <see cref="ICoroutineDelegateBuilder"/>: emitting the
/// coroutine body straight into the state machine's own method, rather than compiling it to
/// a delegate the machine holds in a field and invokes.
/// </summary>
/// <remarks>
/// The delegate exists because the System compiler cannot emit into a
/// <see cref="System.Reflection.Emit.MethodBuilder"/> -- <c>CompileToMethod</c> was never
/// ported past .NET Framework. A builder that can emit into one advertises it here, and the
/// state machine drops the field, the delegate, and the indirection on every resume.
///
/// The body is emitted before <c>CreateType()</c>, so it is written against a type that is
/// still open. That is why the shape of the body is passed rather than a
/// <see cref="LambdaExpression"/>: a delegate type cannot be formed over an open type.
/// Non-embeddable constants are returned for the caller to store in
/// <paramref name="constantsField"/> before the machine runs.
///
/// A DynamicMethod is created with visibility checks skipped; a MethodBuilder on a
/// TypeBuilder is not. So a body that reaches a non-public member keeps the delegate form --
/// see <see cref="VisibilityScanner"/>. Emitting into the type is an optimization and must
/// never narrow what compiles.
/// </remarks>
public interface ICoroutineMethodBuilder
{
    object[] Emit(
        IReadOnlyList<ParameterExpression> parameters,
        Expression body,
        Type returnType,
        System.Reflection.Emit.MethodBuilder method,
        System.Reflection.FieldInfo constantsField );
}
