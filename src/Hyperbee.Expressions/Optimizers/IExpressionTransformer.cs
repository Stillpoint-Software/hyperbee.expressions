using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

public interface IExpressionTransformer
{
    int Priority { get; }

    Expression Transform( Expression expression );
}

public static class PriorityGroup
{
    public const int ConstantEvaluationAndSimplification = 0x0100;
    public const int ControlFlowAndVariableSimplification = 0x0200;
    public const int StructuralReductionAndConsolidation = 0x0400;
    public const int ExpressionReductionAndCaching = 0x0800;
}
