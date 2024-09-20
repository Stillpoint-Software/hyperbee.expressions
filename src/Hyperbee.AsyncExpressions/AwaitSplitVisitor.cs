using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AwaitSplitVisitor : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _variables = [];
    private int _variableCounter;

    private bool _awaitEncountered;
    private int _awaitCounter;

    public bool AwaitEncountered => _awaitEncountered;
    public IReadOnlyCollection<ParameterExpression> Variables => _variables;

    protected override Expression VisitExtension( Expression node )
    {
        switch ( node )
        {
            case AwaitableResultExpression:
                // Always ignore AwaitResultExpression nodes
                return node;

            case AwaitExpression awaitExpression:
                _awaitEncountered = true;

                // Visit internal nodes of await expression
                Visit( awaitExpression.Target );

                _awaitCounter++;

                awaitExpression.ReturnTask = true;
                //_containedAwait = true;

                // Then ignore?
                return node;

            default:
                return base.VisitExtension( node );
        }
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var updatedNode = base.VisitBinary( node );

        var containedAwait = (_awaitCounter--) != 0;

        if ( !containedAwait || updatedNode is not BinaryExpression binaryExpression )
        {
            return updatedNode;
        }

        if ( binaryExpression.Left is ParameterExpression parameterExpression )
        {
            _variables.Add( parameterExpression );
        }

        return AwaitBlock(
            Expression.Block(
                binaryExpression.Right
            ),
            Expression.Block(
                new AwaitableResultExpression( binaryExpression.Left )
            )
        );
    }

    // protected override Expression VisitBlock( BlockExpression node )
    // {
    //     var updatedNode = base.VisitBlock( node );
    //
    //     if ( !_containedAwait || updatedNode is not BlockExpression blockExpression )
    //     {
    //         return updatedNode;
    //     }
    //
    //     _containedAwait = false;
    //
    //     return Expression.Block(
    //     );
    // }

    // protected override Expression VisitLabel( LabelExpression node )
    // {
    //     var updatedNode = base.VisitLabel( node );
    //
    //     if ( !_containedAwait || updatedNode is not LabelExpression labelExpression )
    //     {
    //         return updatedNode;
    //     }
    //
    //     _containedAwait = false;
    //
    //     return Expression.Block(
    //     );
    // }


    protected override Expression VisitGoto( GotoExpression node )
    {
        var updatedNode = base.VisitGoto( node );

        var containedAwait = (_awaitCounter--) != 0;

        if ( !containedAwait || updatedNode is not GotoExpression gotoExpression )
        {
            return updatedNode;
        }

        if ( gotoExpression.Value is not AwaitExpression awaitExpression )
        {
            return updatedNode;
        }

        var variable = CreateVariable( awaitExpression );
        return AwaitBlock(
            Expression.Block(
                awaitExpression
            ),
            Expression.Block(
                new AwaitableResultExpression( variable ),
                gotoExpression.Update( gotoExpression.Target, variable )
            )
        );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var updatedNode = base.VisitMethodCall( node );

        var containedAwait = (_awaitCounter--) != 0;

        if ( !containedAwait || updatedNode is not MethodCallExpression methodCallExpression )
        {
            return updatedNode;
        }

        //_containedAwait = false;

        var arguments = new List<Expression>();
        var variables = new List<ParameterExpression>();
        var blockExpression = new List<Expression>();

        // Visit each argument in the method call
        foreach ( var argument in methodCallExpression.Arguments )
        {
            if ( argument is AwaitExpression awaitExpression )
            {
                var variable = CreateVariable( awaitExpression );
                blockExpression.Add(
                    AwaitBlock(
                        Expression.Block(
                            awaitExpression
                        ),
                        Expression.Block(
                            new AwaitableResultExpression( variable )
                        )
                    ) );

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
        
        // Rewrite the method call
        var updatedCall = methodCallExpression.Update( methodCallExpression.Object, arguments );
        blockExpression.Add( updatedCall );

        return Expression.Block( variables, blockExpression );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var updatedNode = base.VisitConditional( node );

        var containedAwait = (_awaitCounter--) != 0;

        if ( !containedAwait ||
             updatedNode is not ConditionalExpression conditionalExpression ||
             conditionalExpression.Test is not AwaitExpression awaitExpression )
        {
            return updatedNode;
        }

        var variable = CreateVariable( awaitExpression );
        return AwaitBlock(
            Expression.Block(
                awaitExpression
            ),
            Expression.Block(
                new AwaitableResultExpression( variable ),
                node.Update( variable, conditionalExpression.IfTrue, conditionalExpression.IfFalse )
            )
        );
    }

    private static Expression AwaitBlock( Expression before, Expression after )
    {
        return new AwaitableBlockExpression( before, after );
    }

    private ParameterExpression CreateVariable(AwaitExpression awaitExpression)
    {
        var variable = Expression.Variable( awaitExpression.ReturnType, $"__awaitResult<{_variableCounter++}>" );
        _variables.Add( variable );
        return variable;
    }
}
