using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ControlFlowSimplificationOptimizer: Control Flow Optimization
//
// This optimizer removes unreachable branches, simplifies conditionals with constant conditions,
// and eliminates loops with constant-false conditions. It also removes code after control flow exits,
// such as `return`, `throw`, and `goto`, reducing the depth and complexity of control flow structures.

public class ControlFlowSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableControlFlowSimplification ? Visit( expression ) : expression;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( node.Test is not ConstantExpression testConst )
        {
            return base.VisitConditional( node );
        }

        // Eliminate dead code in conditionals based on constant conditions
        var condition = (bool) testConst.Value!;
        return condition ? Visit( node.IfTrue ) : Visit( node.IfFalse );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var expressions = new List<Expression>();
        var hasControlFlowExit = false;

        foreach ( var expr in node.Expressions )
        {
            // Skip expressions after a control flow exit (e.g., Goto, Throw)
            if ( hasControlFlowExit )
                continue;

            // Track if we encounter a control flow exit
            if ( expr is GotoExpression ||
                 (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Throw) )
            {
                hasControlFlowExit = true;
            }

            expressions.Add( Visit( expr ) );
        }

        return Expression.Block( node.Variables, expressions );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        // Remove loops with a constant false condition (never executes)
        if ( node.Body is ConditionalExpression conditional && conditional.Test is ConstantExpression testConst && (bool) testConst.Value == false )
        {
            return Expression.Empty();
        }

        return base.VisitLoop( node );
    }
}
