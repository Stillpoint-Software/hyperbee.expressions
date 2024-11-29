using System.Diagnostics;
using System.Linq.Expressions;
namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    protected override Expression VisitChildren( ExpressionVisitor visitor ) => this;

    internal abstract NodeExpression FallThroughNode { get; }

    internal abstract void Optimize( HashSet<LabelTarget> references );

    internal NodeExpression Parent { get; set; }

    public override Expression Reduce()
    {
        if ( Parent == null )
            throw new InvalidOperationException( $"Transition Reduce requires a {nameof( Parent )} instance." );

        return Block(
            Reduce( Parent )
        );
    }

    private List<Expression> Reduce( NodeExpression node )
    {
        var expressions = new List<Expression>
        {
            Label( node.NodeLabel )
        };

        expressions.AddRange( node.Expressions );

        // add result assignment

        AssignResult( expressions );

        // add transition

        expressions.AddRange( GetExpressions() );

        return expressions;
    }

    protected abstract List<Expression> GetExpressions();

    protected virtual void AssignResult( List<Expression> expressions )
    {
        var resultValue = Parent.ResultValue;
        var resultVariable = Parent.ResultVariable;

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

