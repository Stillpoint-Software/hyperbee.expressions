﻿using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

internal static class JumpTableBuilder
{
    public static Expression Build( StateContext.Scope current, IReadOnlyList<StateContext.Scope> scopes, Expression stateField )
    {
        var jumpCases = current.JumpCases;

        if ( jumpCases.Count == 0 )
            return Empty();

        var jumpTable = new List<SwitchCase>( jumpCases.Count );

        foreach ( var jumpCase in jumpCases )
        {
            // Go to the result of awaiter

            var resultJumpExpression = SwitchCase(
                Block(
                    Assign( stateField, Constant( -1 ) ),
                    Goto( jumpCase.ResultLabel )
                ),
                Constant( jumpCase.StateId )
            );

            jumpTable.Add( resultJumpExpression );
        }

        // Loop over scopes and flatten; nested by parent

        foreach ( var childScope in scopes.Where( x => x.Parent == current ) )
        {
            var testValues = GetNestedTestValues( childScope, scopes );

            if ( testValues.Count <= 0 )
                continue;

            var nestedJumpExpression = SwitchCase(
                Goto( childScope.InitialLabel ),
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

    private static List<ConstantExpression> GetNestedTestValues( StateContext.Scope current, IReadOnlyList<StateContext.Scope> scopes )
    {
        var testCases = current.JumpCases.Select( jumpCase => Constant( jumpCase.StateId ) ).ToList();
        var stack = new Stack<StateContext.Scope>();

        while ( true )
        {
            foreach ( var child in scopes.Where( scope => scope.Parent == current ) )
            {
                stack.Push( child );
            }

            if ( !stack.TryPop( out current ) )
                break;

            foreach ( var jumpCase in current.JumpCases )
            {
                testCases.Add( Constant( jumpCase.StateId ) );
            }
        }

        return testCases;
    }

}
