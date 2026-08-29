using Hyperbee.Expressions.CompilerServices;

namespace Hyperbee.Expressions;

/// <summary>
/// Configuration options for expression runtime behavior.
/// </summary>
public class ExpressionRuntimeOptions
{
    /// <summary>
    /// Gets or sets the ModuleBuilder provider for this expression.
    /// Defaults to <see cref="DefaultModuleBuilderProvider"/>
    /// </summary>
    public IModuleBuilderProvider ModuleBuilderProvider { get; init; } = DefaultModuleBuilderProvider.Instance;

    /// <summary>
    /// Gets or sets whether state machine optimizations are enabled.
    /// When false, the goto optimizer is skipped, preserving the raw lowered state graph.
    /// Defaults to true.
    /// </summary>
    public bool Optimize { get; init; } = true;

    /// <summary>
    /// Gets or sets whether the coroutine body may be emitted into the state machine's own
    /// method, when the compiler in use supports it.
    /// When false, the body is compiled to a delegate the machine holds in a field and
    /// invokes on every resume. Defaults to true.
    /// </summary>
    /// <remarks>
    /// Emitting into the type removes a field, a delegate, and an indirection per resume.
    /// It also declines on its own for a body that reaches a non-public member, which only a
    /// <c>DynamicMethod</c> may do. This switch forces the delegate form regardless, and
    /// exists to isolate the two paths -- for measurement, or to sidestep the newer one.
    /// The System compiler always uses the delegate form; it cannot emit into a method.
    /// </remarks>
    public bool EmitMoveNextIntoType { get; init; } = true;

    /// <summary>
    /// Gets or sets an optional callback to receive the generated state machine source.
    /// When set, the state machine expression debug view is passed as a string for inspection.
    /// </summary>
    public Action<string> SourceHandler { get; init; }
}
