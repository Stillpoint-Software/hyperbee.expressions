namespace Hyperbee.Expressions.Optimizers;

public class OptimizationOptions
{
    public bool EnableConstantSimplification { get; set; } = true;
    public bool EnableInlining { get; set; } = true;
    public bool EnableControlFlowSimplification { get; set; } = true;
    public bool EnableVariableOptimization { get; set; } = true;
    public bool EnableStructuralSimplification { get; set; } = true;
    public bool EnableFlowControlOptimization { get; set; } = true;
    public bool EnableExpressionCaching { get; set; } = true;
    public bool EnableExpressionSimplification { get; set; } = true;
    public bool EnableAccessSimplification { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;
}
