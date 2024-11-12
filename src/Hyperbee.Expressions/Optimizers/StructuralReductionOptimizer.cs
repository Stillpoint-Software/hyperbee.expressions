using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// StructuralReductionOptimizer: Control Flow Simplification
//
// This optimizer manages blocks, labels, loops, and conditionals, removing unreachable
// code and dead branches based on control flow and constant conditions.

public class StructuralReductionOptimizer : BaseOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new StructuralReductionVisitor()
    ];
}

