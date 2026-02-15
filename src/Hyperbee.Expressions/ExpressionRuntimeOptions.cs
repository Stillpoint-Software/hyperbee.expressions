using System.Linq.Expressions;

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
    /// Gets or sets an optional callback to receive the generated state machine expression source.
    /// When set, the lowered expression tree is passed to this action for debugging and inspection.
    /// </summary>
    public Action<Expression> SourceHandler { get; init; }
}
