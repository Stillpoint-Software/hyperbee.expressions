using System.Linq.Expressions;
using Hyperbee.Expressions.CompilerServices.Transitions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

internal static class JumpTableBuilder
{
    public static Expression Build( StateContext.Scope current, IReadOnlyList<StateContext.Scope> scopes, Expression stateField )
    {
        var jumpCases = current.JumpCases;

        var jumpTable = new List<SwitchCase>( jumpCases.Count );

        foreach ( var (label, stateId, _) in jumpCases )
        {
            // Go to the result of awaiter.
            //
            // Return to the running state before resuming. The state field is a
            // state-machine field, and a jump table inside a loop (the one a try region
            // emits) is re-evaluated on every iteration; a stale id would dispatch back
            // to a resume point that has already run.

            var resultJumpExpression = SwitchCase(
                Block(
                    Assign( stateField, Constant( Transition.RunningState ) ),
                    Goto( label )
                ),
                Constant( stateId )
            );

            jumpTable.Add( resultJumpExpression );
        }

        // Loop over scopes and flatten; nested by parent

        for ( var index = 0; index < scopes.Count; index++ )
        {
            var childScope = scopes[index];

            if ( childScope.Parent != current )
                continue;

            var testValues = GetNestedTestValues( childScope, scopes );

            if ( testValues.Count <= 0 )
                continue;

            var nestedJumpExpression = SwitchCase(
                Goto( childScope.InitialLabel ),
                testValues
            );

            jumpTable.Add( nestedJumpExpression );
        }

        // A scope may own no jump cases and still need a table: an await nested in a
        // child scope (e.g. a try region) resumes through the parent scope.

        if ( jumpTable.Count == 0 )
            return Empty();

        return Switch(
            stateField,
            [.. jumpTable]
        );
    }

    private static List<ConstantExpression> GetNestedTestValues( StateContext.Scope current, IReadOnlyList<StateContext.Scope> scopes )
    {
        var testCases = new List<ConstantExpression>( current.JumpCases.Count );

        for ( var index = 0; index < current.JumpCases.Count; index++ )
        {
            testCases.Add( Constant( current.JumpCases[index].StateId ) );
        }

        var stack = new Stack<StateContext.Scope>();

        while ( true )
        {
            for ( var index = 0; index < scopes.Count; index++ )
            {
                if ( scopes[index].Parent == current )
                    stack.Push( scopes[index] );
            }

            if ( !stack.TryPop( out current ) )
                break;

            foreach ( var (_, stateId, _) in current.JumpCases )
            {
                testCases.Add( Constant( stateId ) );
            }
        }

        return testCases;
    }
}
