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
    /// Gets or sets an optional callback that captures the generated state machine expression
    /// debug view as a string. When set, the expression tree's DebugView is passed for inspection.
    /// </summary>
    public Action<string> ExpressionCapture { get; init; }
}
