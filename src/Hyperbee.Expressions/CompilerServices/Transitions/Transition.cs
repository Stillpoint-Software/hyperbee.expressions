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

            // A void expression yields no value to capture (e.g. a void method call before
            // an await). Note typeof(object).IsAssignableFrom(typeof(void)) is true, so the
            // check below would otherwise try to Convert void -> object and throw. Leave the
            // result variable untouched; a later state assigns the real result.

            if ( lastExpression.Type != typeof( void ) && variable.Type.IsAssignableFrom( lastExpression.Type ) )
            {
                expressions[^1] = Assign( variable, EnsureConvert( lastExpression, variable.Type ) );
            }

            return;
        }

        if ( value != null && variable.Type.IsAssignableFrom( value.Type ) )
        {
            expressions.Add( Assign( variable, EnsureConvert( value, variable.Type ) ) );
        }
    }

    protected static Expression EnsureConvert( Expression expression, Type targetType )
    {
        if ( expression.Type != targetType && expression.Type.IsValueType )
            return Convert( expression, targetType );

        return expression;
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

