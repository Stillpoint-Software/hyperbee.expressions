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

    public Expression Reduce( NodeExpression parent )
    {
        if ( parent == null )
            throw new InvalidOperationException( $"Transition Reduce requires a {nameof( parent )} instance." );

        var reduced = ReduceInternal( parent );

        return (reduced.Count == 1)
            ? reduced[0]
            : Block( reduced );
    }

    private List<Expression> ReduceInternal( NodeExpression parent )
    {
        var expressions = new List<Expression>( 8 ) // Label, Expressions, AssignResult, Transition
        {
            Label( parent.NodeLabel )
        };

        expressions.AddRange( parent.Expressions );

        // add result assignment

        AssignResult( parent, expressions );

        // add transition body

        expressions.AddRange( GetBody( parent ) );

        return expressions;
    }

    protected abstract List<Expression> GetBody( NodeExpression parent );

    protected virtual void AssignResult( NodeExpression parent, List<Expression> expressions )
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

