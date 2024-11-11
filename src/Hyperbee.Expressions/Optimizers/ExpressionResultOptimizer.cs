using Hyperbee.Expressions.Optimizers.Visitors;

namespace Hyperbee.Expressions.Optimizers;

// ExpressionCachingOptimizer: Expression Subexpression Caching
//
// This optimizer performs subexpression caching to reduce repeated computation in complex expressions. 
// By identifying and reusing common subexpressions, it improves execution efficiency, especially in cases
// where identical subexpressions are evaluated multiple times within an expression tree.
//
// Example: 
//
// Before Optimization:
//
// .Lambda #Lambda1<System.Func`1[System.Int32]> {
//     5 * (3 + 2) + 5 * (3 + 2)
// }
//
// After Optimization:
//
// .Lambda #Lambda1<System.Func`1[System.Int32]> {
//     .Block(System.Int32 $cacheVar) {
//         $cacheVar = 5 * (3 + 2);
//         $cacheVar + $cacheVar
//     }
// }
//
// In this example, the optimizer identifies the subexpression `5 * (3 + 2)` as a repeated, cacheable part.
// It creates a variable `$cacheVar` to hold the computed value of `5 * (3 + 2)`, and replaces occurrences
// of this subexpression with `$cacheVar` in the resulting `BlockExpression`. This optimization reduces
// redundant calculations, resulting in a more efficient expression execution.

public class ExpressionResultOptimizer : BaseOptimizer
{
    public override IExpressionTransformer[] Dependencies =>
    [
        new ExpressionResultVisitor()
    ];
}

