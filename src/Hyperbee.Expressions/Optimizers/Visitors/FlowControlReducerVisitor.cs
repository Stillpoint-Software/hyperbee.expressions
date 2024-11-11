using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// FlowControlReducerVisitor: Conditional and Loop Expression Optimization
//
// This visitor simplifies `ConditionalExpression` and `LoopExpression` nodes by reducing
// unnecessary conditions and loop constructs. For conditionals, it pre-evaluates static
// conditions, replacing them with the appropriate branch. For loops, it removes loops with
// a `false` condition.
//
// Before:
//
//   .IfThenElse(.Constant(true), .Constant(1), .Constant(0))
//
// After:
//
//   .Constant(1)
//
// Before:
//
//   .Loop(
//       .IfThenElse(.Constant(false), .Break(loop))
//   )
//
// After:
//
//   .Empty()
//
public class FlowControlReducerVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var test = Visit( node.Test );

        if ( test is ConstantExpression testConst )
        {
            var condition = (bool) testConst.Value!;
            var result = condition ? node.IfTrue : node.IfFalse;
            return Visit( result );
        }

        return base.VisitConditional( node );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var body = Visit( node.Body );

        if ( body is ConditionalExpression conditional && IsConstantFalse( conditional.Test ) )
        {
            return node.Type == typeof( void ) ? Expression.Empty() : Expression.Default( node.Type );
        }

        return node.Update( node.BreakLabel, node.ContinueLabel, body );
    }

    private static bool IsConstantFalse( Expression expression )
    {
        return expression is ConstantExpression constant &&
               constant.Type == typeof( bool ) &&
               !(bool) constant.Value!;
    }
}
