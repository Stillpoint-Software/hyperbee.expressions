using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class FinalTransition : Transition
{
    internal override StateNode FallThroughNode => null;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        // nop
    }

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        var (variable, value) = context.StateNode.Result;

        if ( variable == null )
        {
            return;
        }

        if ( expressions.Count <= 1 || expressions[^1].Type == typeof( void ) )
        {
            value ??= Constant( null, typeof( IVoidResult ) );
            expressions.Add( Assign( variable, value ) );
            return;
        }

        if ( expressions.Count > 1 )
        {
            var lastExpression = expressions[^1];

            if ( variable.Type.IsAssignableFrom( lastExpression.Type ) )
            {
                expressions[^1] = Assign( variable, lastExpression );
            }
        }
    }
}
