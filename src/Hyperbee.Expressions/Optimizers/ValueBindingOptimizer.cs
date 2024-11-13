using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// VariableBindingOptimizer: Variable, Constant, and Member Access Optimization
//
// This optimizer handles optimizations related to variables, constants, and member accesses,
// performing constant folding, variable inlining, and simplifying member access.

public class ValueBindingOptimizer : ExpressionOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new VariableReducerVisitor(),
        new ConstantFoldingVisitor(),
        new MemberAccessVisitor()
    ];
}
