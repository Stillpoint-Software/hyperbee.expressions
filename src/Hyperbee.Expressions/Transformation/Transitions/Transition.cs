using System.Diagnostics;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

internal abstract class Transition
{
    protected static readonly List<Expression> EmptyBody = [Empty()];

    internal abstract NodeExpression FallThroughNode { get; }

    internal abstract void Optimize( HashSet<LabelTarget> references );

    public void GetExpressions( NodeExpression parent, List<Expression> expressions )
    {
        if ( parent == null )
            throw new InvalidOperationException( $"Transition {nameof(GetExpressions)} requires a {nameof(parent)} instance." );

        SetResult( expressions, parent );
        SetBody( expressions, parent );
    }

    protected virtual void SetResult( List<Expression> expressions, NodeExpression parent )
    {
        var resultValue = parent.ResultValue;
        var resultVariable = parent.ResultVariable;

        if ( resultValue != null && resultVariable != null && resultValue.Type == resultVariable.Type )
        {
            expressions.Add( Assign( resultVariable, resultValue ) );
        }
        else if ( resultVariable != null && expressions.Count > 1 && resultVariable.Type == expressions[^1].Type )
        {
            expressions[^1] = Assign( resultVariable, expressions[^1] );
        }
    }

    protected abstract void SetBody( List<Expression> expressions, NodeExpression parent );

    protected static Expression GotoOrFallThrough( int order, NodeExpression node, bool allowNull = false )
    {
        return order + 1 == node.StateOrder
            ? allowNull ? null : Empty()
            : Goto( node.NodeLabel );
    }

    protected static NodeExpression OptimizeGotos( NodeExpression node )
    {
        while ( node.IsNoOp && node.Transition is GotoTransition gotoTransition )
        {
            node = gotoTransition.TargetNode;
        }

        return node;
    }
}

