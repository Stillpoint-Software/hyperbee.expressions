using System.Linq.Expressions;
using Hyperbee.Expressions.Collections;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

internal static class JumpTableBuilder
{
    public static Expression Build( StateContext.Scope current, PooledArray<StateContext.Scope> scopes, Expression stateField )
    {
        var jumpCases = current.JumpCases;
        var jumpTable = new List<SwitchCase>( jumpCases.Count );

        var span = jumpCases.AsReadOnlySpan();

        for ( var index = 0; index < span.Length; index++ )
        {
            var jumpCase = span[index];

            // Go to the result of awaiter

            var resultJumpExpression = SwitchCase(
                Block(
                    Assign( stateField, Constant( -1 ) ),
                    Goto( jumpCase.ResultLabel )
                ),
                Constant( jumpCase.StateId )
            );

            jumpTable.Add( resultJumpExpression );

            // Go to nested jump cases

            var testValues = JumpCaseTests( current, jumpCase.StateId, scopes );

            if ( testValues.Count <= 0 )
                continue;

            var nestedJumpExpression = SwitchCase(
                Block(
                    Assign( stateField, Constant( -1 ) ),
                    Goto( jumpCase.ContinueLabel )
                ),
                testValues
            );

            jumpTable.Add( nestedJumpExpression );
        }

        return Switch(
            stateField,
            Empty(),
            [.. jumpTable]
        );
    }

    // Iterative function to build jump table cases
    private static List<Expression> JumpCaseTests( StateContext.Scope scope, int stateId, PooledArray<StateContext.Scope> scopes )
    {
        var testValues = new List<Expression>();
        var stack = new Stack<(StateContext.Scope, int)>();

        while ( true )
        {
            var scopeSpan = scopes.AsReadOnlySpan();

            for ( var scopeIndex = 0; scopeIndex < scopeSpan.Length; scopeIndex++ )
            {
                var nestedScope = scopeSpan[scopeIndex];

                if ( nestedScope.Parent != scope )
                    continue;

                var jumpCasesSpan = nestedScope.JumpCases.AsReadOnlySpan();

                for ( var jumpIndex = 0; jumpIndex < jumpCasesSpan.Length; jumpIndex++ )
                {
                    var childJumpCase = jumpCasesSpan[jumpIndex];

                    if ( childJumpCase.ParentId != stateId )
                        continue;

                    // Return self
                    testValues.Add( Constant( childJumpCase.StateId ) );

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
