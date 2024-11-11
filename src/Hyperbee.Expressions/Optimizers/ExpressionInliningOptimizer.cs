using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// InliningOptimizer: Inlining Functions, Constants, and Conditional Values
//
// This optimizer inlines constants and simplifies expressions by propagating known values.
// It focuses on inlining lambda expressions, boolean short-circuiting, and simplifying conditional
// expressions based on constant values.
//
// Before:
//   .Invoke(lambda, .Constant(5))
//
// After:
//   .LambdaExpression(
//       .Parameter(x),
//       .Add(.Constant(5), .Constant(5))
//   )
//
// Before:
//   .IfThenElse(.Constant(true), .Constant("True Branch"), .Constant("False Branch"))
//
// After:
//   .Constant("True Branch")
//
public class ExpressionInliningOptimizer : BaseOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new ExpressionInliningVisitor()
    ];
}

