using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal static class JumpTableBuilder
{
    public static Expression Build( StateContext.Scope current, List<StateContext.Scope> scopes, Expression stateField )
    {
        var jumpCases = current.JumpCases;
        var jumpTable = new List<SwitchCase>( jumpCases.Count );

        for ( var index = 0; index < jumpCases.Count; index++ )
        {
            var jumpCase = jumpCases[index];

            // Go to the result of awaiter

            var resultJumpExpression = Expression.SwitchCase(
                Expression.Block(
                    Expression.Assign( stateField, Expression.Constant( -1 ) ),
                    Expression.Goto( jumpCase.ResultLabel )
                ),
                Expression.Constant( jumpCase.StateId )
            );

            jumpTable.Add( resultJumpExpression );

            // Go to nested jump cases

            var testValues = JumpCaseTests( current, jumpCase.StateId, scopes );

            if ( testValues.Count <= 0 )
                continue;

            var nestedJumpExpression = Expression.SwitchCase(
                Expression.Block(
                    Expression.Assign( stateField, Expression.Constant( -1 ) ),
                    Expression.Goto( jumpCase.ContinueLabel )
                ),
                testValues
            );

            jumpTable.Add( nestedJumpExpression );
        }

        return Expression.Switch(
            stateField,
            Expression.Empty(),
            [.. jumpTable]
        );
    }

    // Iterative function to build jump table cases
    private static List<Expression> JumpCaseTests( StateContext.Scope scope, int stateId, List<StateContext.Scope> scopes )
    {
        var testValues = new List<Expression>();
        var stack = new Stack<(StateContext.Scope, int)>();

        while ( true )
        {
            for ( var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++ )
            {
                var nestedScope = scopes[scopeIndex];

                if ( nestedScope.Parent != scope )
                    continue;

                for ( var jumpIndex = 0; jumpIndex < nestedScope.JumpCases.Count; jumpIndex++ )
                {
                    var childJumpCase = nestedScope.JumpCases[jumpIndex];

                    if ( childJumpCase.ParentId != stateId )
                        continue;

                    // Return self
                    testValues.Add( Expression.Constant( childJumpCase.StateId ) );

                    // Push nested jump cases onto the stack
                    stack.Push( (nestedScope, childJumpCase.StateId) );
                }
            }

            if ( !stack.TryPop( out var item ) )
                break;

            (scope, stateId) = item;
        }

        return testValues;
    }
}
