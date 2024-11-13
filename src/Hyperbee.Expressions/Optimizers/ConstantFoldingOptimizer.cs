using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// ConstantFoldingOptimizer: Constant Evaluation and Simplification
//
// This optimizer evaluates constant expressions and simplifies them to their simplest form.

public class ConstantFoldingOptimizer : ExpressionOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new ConstantFoldingVisitor()
    ];
}
