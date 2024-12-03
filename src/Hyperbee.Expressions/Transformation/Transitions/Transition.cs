using System.Diagnostics;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

internal abstract class Transition
{
    internal abstract NodeExpression FallThroughNode { get; }

    public virtual void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        SetResult( expressions, context );
    }

    private static void SetResult( List<Expression> expressions, StateMachineContext context )
    {
        var (variable, value) = context.NodeInfo.Result;

        if ( variable == null )
        {
            return;
        }

        if ( value != null && variable.Type.IsAssignableFrom( value.Type ) )
        {
            expressions.Add( Assign( variable, value ) );
        }
        else if ( expressions.Count > 1 )
        {
            var lastExpression = expressions[^1];

            if ( variable.Type.IsAssignableFrom( lastExpression.Type ) )
            {
                expressions[^1] = Assign( variable, lastExpression );
            }
        }
    }

    internal abstract void Optimize( HashSet<LabelTarget> references );

    protected static NodeExpression OptimizeGotos( NodeExpression node )
    {
        while ( node.IsNoOp && node.Transition is GotoTransition gotoTransition )
        {
            node = gotoTransition.TargetNode;
        }

        return node;
    }

    protected static Expression GotoOrFallThrough( int order, NodeExpression node, bool allowNull = false )
    {
        if ( order + 1 == node.StateOrder )
        {
            return allowNull ? null : Empty();
        }

        return Goto( node.NodeLabel );
    }
}

