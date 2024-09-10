using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AwaitSplitVisitor : ExpressionVisitor
{
    private readonly Stack<Expression> _currentExpressions = [];
    private readonly List<Expression> _expressions = [];
    private int _variableCounter;

    public IReadOnlyList<Expression> Expressions => _expressions;

    protected override Expression VisitConstant( ConstantExpression node )
    {
        if ( _currentExpressions.Count == 0 ) 
            _expressions.Add( node );

        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( _currentExpressions.Count == 0 )
            _expressions.Add( node );

        return node;
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        if ( _currentExpressions.Count == 0 ) _expressions.Add( node );
        return node;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        if ( _currentExpressions.Count == 0 ) _expressions.Add( node );
        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.Right is not AwaitExpression awaitExpression )
        {
            if ( _currentExpressions.Count == 0 ) _expressions.Add( node );
            return node;
        }

        _expressions.Add( awaitExpression );
        _expressions.Add( new AwaitResultExpression( node.Left ) );

        return node;
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var arguments = new List<Expression>();
        var variables = new List<ParameterExpression>();
        var containedAwait = false;

        // Visit each argument in the method call
        foreach ( var argument in node.Arguments )
        {
            if ( argument is AwaitExpression awaitExpression )
            {
                containedAwait = true;
                _expressions.Add( awaitExpression );

                var variable = Expression.Variable( argument.Type, TempVariableName() );
                _expressions.Add( new AwaitResultExpression( variable ) );

                // Replace the AwaitExpression in the method call with the variable
                arguments.Add( variable );
                variables.Add( variable );
            }
            else
            {
                // If not an AwaitExpression, just add the original argument
                arguments.Add( argument );
            }
        }

        if(!containedAwait )
        {
            if ( _currentExpressions.Count == 0 ) _expressions.Add( node );
            return node;
        }

        // Rewrite the method call
        _currentExpressions.Push( node );
        var updatedCall = node.Update( node.Object, arguments );
        _currentExpressions.Pop();

        // Create a new block that represents the rewritten method call
        if ( variables.Count > 0 )
        {
            _expressions.Add( Expression.Block( variables, updatedCall ) );
        }

        return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( node.Test is not AwaitExpression awaitExpression )
            return node;

        _expressions.Add( awaitExpression );

        var variable = Expression.Variable( awaitExpression.Type, TempVariableName() );
        _expressions.Add( new AwaitResultExpression( variable ) );

        var updateConditional = node.Update( variable, node.IfTrue, node.IfFalse );
        _expressions.Add( updateConditional );

        return node;
    }

    private string TempVariableName() => $"__await_var{_variableCounter++}";
}
