using System.Diagnostics;
using System.Linq.Expressions;
namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof(void);
    public override bool CanReduce => true;

    protected override Expression VisitChildren( ExpressionVisitor visitor ) => this;

    internal abstract NodeExpression FallThroughNode { get; }
    
    internal abstract void OptimizeTransition( HashSet<LabelTarget> references );

    public virtual List<Expression> Reduce( NodeExpression node )
    {
        var expressions = new List<Expression> 
        { 
            Label( node.NodeLabel ) 
        };

        expressions.AddRange( node.Expressions );

        // add result assignment

        AssignResult( node, expressions );

        // add transition

        expressions.AddRange( ReduceTransition( node ) );

        return expressions;
    }
    
    protected abstract List<Expression> ReduceTransition( NodeExpression node );

    protected virtual void AssignResult( NodeExpression node, List<Expression> expressions )
    {
        if ( node.ResultValue != null && node.ResultVariable != null && node.ResultValue.Type == node.ResultVariable.Type )
        {
            expressions.Add( Assign( node.ResultVariable, node.ResultValue ) );
        }
        else if ( node.ResultVariable != null && expressions.Count > 1 && node.ResultVariable.Type == expressions[^1].Type )
        {
            expressions[^1] = Assign( node.ResultVariable, expressions[^1] );
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

