using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncBlockExpression : AsyncBaseExpression
{
    private readonly BlockExpression _reducedBlock;
    private readonly Type _finalResultType;

    public AsyncBlockExpression( Expression[] expressions ) : base( expressions )
    {
        if ( expressions == null || expressions.Length == 0 )
        {
            throw new ArgumentException( "AsyncBlockExpression must contain at least one expression.", nameof(expressions) );
        }

        _reducedBlock = ReduceBlock( expressions, out _finalResultType );
    }

    protected override Type GetFinalResultType()
    {
        return _finalResultType;
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        builder.GenerateMoveNextMethod( _reducedBlock );
    }

    private static BlockExpression ReduceBlock( Expression[] expressions, out Type finalResultType )
    {
        var parentBlockExpressions = new List<Expression>();
        var currentBlockExpressions = new List<Expression>();
        var awaitEncountered = false;

        // Collect all variables declared in the block
        var variables = new HashSet<ParameterExpression>();
        finalResultType = typeof(void); // Default to void, adjust if task found

        foreach ( var expr in expressions )
        {
            if ( expr is AsyncBlockExpression asyncBlock )
            {
                // Recursively reduce the inner async block
                var reducedInnerBlock = asyncBlock.Reduce();
                currentBlockExpressions.Add( reducedInnerBlock );
                continue;
            }

            currentBlockExpressions.Add( expr );

            switch ( expr )
            {
                case BinaryExpression binaryExpr when binaryExpr.Left is ParameterExpression varExpr:
                    variables.Add( varExpr );
                    break;
                case AwaitExpression:
                {
                    awaitEncountered = true;
                    var currentBlock = Block( currentBlockExpressions );
                    parentBlockExpressions.Add( currentBlock );
                    currentBlockExpressions = [];
                    break;
                }
            }
        }

        if ( currentBlockExpressions.Count > 0 )
        {
            var finalBlock = Block( currentBlockExpressions );
            parentBlockExpressions.Add( finalBlock );

            // Update the final result type based on the last expression in the final block
            var lastExpr = currentBlockExpressions.Last();
            if ( IsTask( lastExpr.Type ) )
            {
                finalResultType = lastExpr.Type.IsGenericType 
                    ? lastExpr.Type.GetGenericArguments()[0] 
                    : typeof(void); // Task without a result
            }
        }

        if ( !awaitEncountered )
        {
            throw new InvalidOperationException( $"{nameof(AsyncBlockExpression)} must contain at least one {nameof(AwaitExpression)}." );
        }

        // Combine all child blocks into a single parent block, with variables declared at the parent level
        return Block( variables, parentBlockExpressions ); // Declare variables only once at the top level
    }
}

public static partial class AsyncExpression
{
    public static AsyncBlockExpression BlockAsync( params Expression[] expressions )
    {
        return new AsyncBlockExpression( expressions );
    }
}

