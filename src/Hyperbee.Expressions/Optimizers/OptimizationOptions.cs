namespace Hyperbee.Expressions.Optimizers;

public class OptimizationOptions
{
    public bool EnableInliningOptimization { get; set; } = true;
    public bool EnableValueBindingOptimization { get; set; } = true;
    public bool EnableFlowControlOptimization { get; set; } = true;
    public bool EnableExpressionResultCaching { get; set; } = true;
    public bool EnableExpressionReduction { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;
}
