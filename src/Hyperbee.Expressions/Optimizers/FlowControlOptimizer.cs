using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// FlowControlOptimizer: Control Flow Simplification
//
// This optimizer manages blocks, labels, loops, and conditionals, removing unreachable
// code and dead branches based on control flow and constant conditions.

public class FlowControlOptimizer : BaseOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new BlockReducerVisitor(),
        new GotoReducerVisitor(),
        new FlowControlReducerVisitor(),
        new TryCatchSimplifierVisitor()
    ];
}
