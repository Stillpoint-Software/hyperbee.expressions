﻿using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// ExpressionReduction: Arithmetic and Logical Reduction
//
// This optimizer removes trivial arithmetic and logical expressions that
// have no effect, simplifies nested expressions, and combines sequential
// expressions where possible.
//
// Examples:
// Before:
//   .Add(.Parameter(x), .Constant(0))
//
// After:
//   .Parameter(x)
//
// Before:
//   .Multiply(.Parameter(x), .Constant(1))
//
// After:
//   .Parameter(x)
//
public class OperatorReductionOptimizer : ExpressionOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new ConstantFoldingVisitor(),
        new OperatorReductionVisitor()
    ];
}
