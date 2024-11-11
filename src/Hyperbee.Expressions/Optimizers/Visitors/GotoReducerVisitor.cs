using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// LabelSimplifier: Label and Goto Simplification
//
// This visitor removes unreferenced `LabelExpression`s and optimizes `GotoExpression`s
// by eliminating unnecessary gotos, typically those that do not have a corresponding label
// or are never used. It scans for labels in the expression tree to determine which ones
// are required and removes those that are unreferenced.
//
// Before:
//
//   .Block(
//       .Label(label1),
//       .Goto(label1),
//       .Constant(5)
//   )
//
// After:
//
//   .Block(
//       .Constant(5)
//   )
//
public class GotoReducerVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    private readonly HashSet<LabelTarget> _usedLabels = [];

    protected override Expression VisitLabel( LabelExpression node )
    {
        return _usedLabels.Contains( node.Target ) ? node : Expression.Empty();
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        _usedLabels.Add( node.Target );

        if ( node.Target.Name == null )
        {
            return Expression.Empty();
        }

        var expression = Visit( node.Value );
        return Expression.MakeGoto( node.Kind, node.Target, expression, node.Type );
    }
}
