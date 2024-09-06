using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AwaitVisitor : ExpressionVisitor
{
    private readonly List<Expression> _expressions = [];
    private int _variableCounter;

    public IReadOnlyList<Expression> Expressions => _expressions;

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.Right is not AwaitExpression awaitExpression)
        {
            _expressions.Add( node );
            return base.VisitBinary( node );
        }

        // Create a variable to hold the result of the Await expression
        var variable = Expression.Variable( awaitExpression.Type, TempVariableName() );

        // Create a new block with the variable and the await expression
        var assignAwait = Expression.Assign( variable, awaitExpression );
        var assignBlock = Expression.Block( [variable], assignAwait, variable );
        var awaitBlock = AsyncExpression.Await( assignBlock, false );
        _expressions.Add( awaitBlock );

        var newAssignment = Expression.Assign( node.Left, variable );
        _expressions.Add( newAssignment );

        return base.VisitBinary( node );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var arguments = new List<Expression>();
        var variables = new List<ParameterExpression>();

        // Visit each argument in the method call
        foreach ( var argument in node.Arguments )
        {
            if ( argument is AwaitExpression )
            {
                var variable = Expression.Variable( argument.Type, TempVariableName() );
                var assign = Expression.Assign( variable, argument );
                var awaitBlock = AsyncExpression.Awaitable( Expression.Block( [variable], assign ) );
                _expressions.Add( awaitBlock );

                // Replace the AwaitExpression in the method call with the variable
                arguments.Add( variable );
                variables.Add( variable );
            }
            else
            {
                // If not an AwaitExpression, just add the original argument
                arguments.Add( Visit( argument ) );
            }
        }

        // Rewrite the method call
        var updatedCall = node.Update( Visit( node.Object ), arguments );

        // Create a new block that represents the rewritten method call
        if ( variables.Count > 0 )
        {
            _expressions.Add( Expression.Block( variables, updatedCall ) );
        }

        return base.VisitMethodCall( node );
    }


    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( node.Test is not AwaitExpression )
            return base.VisitConditional( node );

        // Create a variable to hold the result of the Await expression
        var variable = Expression.Variable( node.Test.Type, TempVariableName() );

        // Create a new block with the variable and the await expression
        var assignAwait = Expression.Assign( variable, node.Test );
        var awaitBlock = AsyncExpression.Awaitable( Expression.Block( [variable], assignAwait ) );
        _expressions.Add( awaitBlock );

        var updateConditional = node.Update( assignAwait, node.IfTrue, node.IfFalse );
        _expressions.Add( updateConditional );

        return base.VisitConditional( node );
    }

    private string TempVariableName() => $"__var{_variableCounter++}";
}
