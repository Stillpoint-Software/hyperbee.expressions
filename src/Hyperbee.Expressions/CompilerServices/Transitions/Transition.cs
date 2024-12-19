using System.Diagnostics;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

internal abstract class Transition
{
    internal abstract StateNode FallThroughNode { get; }

    public virtual void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        SetResult( expressions, context );
    }

    private static void SetResult( List<Expression> expressions, StateMachineContext context )
    {
        var (variable, value) = context.StateNode.Result;

        if ( variable == null )
        {
            return;
        }

        if ( expressions.Count > 1 )
        {
            var lastExpression = expressions[^1];

            if ( variable.Type.IsAssignableFrom( lastExpression.Type ) )
            {
                expressions[^1] = Assign( variable, lastExpression );
            }

            return;
        }

        if ( value != null && variable.Type.IsAssignableFrom( value.Type ) )
        {
            expressions.Add( Assign( variable, value ) );
        }
    }

    internal abstract void Optimize( HashSet<LabelTarget> references );

    protected static StateNode OptimizeGotos( StateNode node )
    {
        while ( IsNoOp( node ) && node.Transition is GotoTransition gotoTransition )
        {
            node = gotoTransition.TargetNode;
        }

        return node;

        static bool IsNoOp( StateNode node ) => node.Expressions.Count == 0 && node.Result.Variable == null;
    }

    protected static Expression GotoOrFallThrough( int order, StateNode node, bool allowNull = false )
    {
        if ( order + 1 == node.StateOrder )
        {
            return allowNull ? null : Empty();
        }

        return Goto( node.NodeLabel );
    }
}

