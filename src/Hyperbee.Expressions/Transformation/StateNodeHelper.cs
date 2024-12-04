using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal static class StateNodeHelper
{
    internal static List<Expression> MergeStates( IReadOnlyList<IStateNode> nodes, StateMachineContext context )
    {
        var mergedExpressions = new List<Expression>( 32 );

        for ( var index = 0; index < nodes.Count; index++ )
        {
            var node = nodes[index];
            var expression = node.GetExpression( context );

            if ( expression is BlockExpression innerBlock )
                mergedExpressions.AddRange( innerBlock.Expressions.Where( expr => !IsDefaultVoid( expr ) ) );
            else
                mergedExpressions.Add( expression );
        }

        return mergedExpressions;

        static bool IsDefaultVoid( Expression expression )
        {
            return expression is DefaultExpression defaultExpression &&
                   defaultExpression.Type == typeof( void );
        }
    }
}
