using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// LabelSimplifier: Simplify Conditional and Loop Expressions
// TryCatchSimplifier: Simplify TryCatch and TryFinally Blocks


// GotoLabelTryOptimizer: Control Flow Optimization
//
// This optimizer removes unused `Label` and `Goto` expressions and simplifies empty `TryCatch` 
// and `TryFinally` blocks. It also eliminates unreferenced labels and unused `Goto` targets, 
// reducing unnecessary jumps and error-handling structures to improve control flow clarity.

public class GotoLabelTryOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    private readonly HashSet<LabelTarget> _usedLabels = [];

    public Expression Optimize( Expression expression )
    {
        return Visit( expression );
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        return (TExpr) Visit( expression );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        // Track used labels
        _usedLabels.Add( node.Target );

        // Remove redundant jumps if they point to empty labels
        return node.Target.Name == null ? Expression.Empty() : base.VisitGoto( node );
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        // Remove unreferenced labels
        return _usedLabels.Contains( node.Target ) ? base.VisitLabel( node ) : Expression.Empty();
    }

    protected override Expression VisitTry( TryExpression node )
    {
        // Simplify empty TryCatch and TryFinally blocks
        if ( node.Body is BlockExpression bodyBlock && bodyBlock.Expressions.Count == 0 )
        {
            return Expression.Empty();
        }

        // Simplify if catch/finally block is empty
        if ( (node.Finally == null || node.Finally is BlockExpression { Expressions.Count: 0 })
             && node.Handlers.Count == 0 )
        {
            return Visit( node.Body );
        }

        return base.VisitTry( node );
    }
}
