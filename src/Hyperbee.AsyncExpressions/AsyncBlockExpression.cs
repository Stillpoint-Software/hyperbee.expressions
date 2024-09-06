using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncBlockExpression : AsyncBaseExpression
{
    private readonly Expression[] _expressions;
    private readonly ParameterExpression[] _initialVariables;

    private bool _isReduced;
    private BlockExpression _reducedBlock;
    private Type _resultType;

    public AsyncBlockExpression( Expression[] expressions )
        : this( [], expressions )
    {
    }

    public AsyncBlockExpression( ParameterExpression[] variables, Expression[] expressions )
    {
        if ( expressions == null || expressions.Length == 0 )
        {
            throw new ArgumentException( "AsyncBlockExpression must contain at least one expression.",
                nameof(expressions) );
        }

        _initialVariables = variables;
        _expressions = expressions;
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _reducedBlock;

        _reducedBlock = ReduceBlock( _expressions, out _resultType );
        _isReduced = true;

        return _reducedBlock;
    }

    protected override Type GetResultType()
    {
        if ( !_isReduced )
            Reduce();

        return _resultType;
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        if ( !_isReduced )
            Reduce();

        builder.SetSource( _reducedBlock ); 
    }

    private BlockExpression ReduceBlock( Expression[] expressions, out Type resultType )
    {
        var childBlockExpressions = new List<Expression>();
        var currentBlockExpressions = new List<Expression>();
        var awaitEncountered = false;

        var variables = new HashSet<ParameterExpression>( _initialVariables );
        
        resultType = typeof(void);

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
                case AwaitableExpression: // BF - Think this can be removed
                    awaitEncountered = true;
                    var currentBlock1 = Block( currentBlockExpressions );
                    childBlockExpressions.Add( currentBlock1 );
                    currentBlockExpressions = [];
                    break;
                case AwaitExpression awaitExpression:
                    awaitExpression.ReturnTask = true; // BF - Set the return task flag to true
                    awaitEncountered = true;
                    var currentBlock2 = Block( currentBlockExpressions );
                    childBlockExpressions.Add( currentBlock2 );
                    currentBlockExpressions = [];
                    break;
            }
        }

        // Get the result type
        if ( currentBlockExpressions.Count > 0 )
        {
            var finalBlock = Block( currentBlockExpressions );
            childBlockExpressions.Add( finalBlock );

            var lastExpr = currentBlockExpressions[^1];
            
            if ( IsTask( lastExpr.Type ) )
            {
                resultType = lastExpr.Type.IsGenericType
                    ? lastExpr.Type.GetGenericArguments()[0]
                    : typeof(void); // Task without a result
            }
            else
            {
                resultType = lastExpr.Type;
            }
        }

        // Ensure that at least one await is present in the block
        if ( !awaitEncountered )
        {
            throw new InvalidOperationException( $"{nameof(AsyncBlockExpression)} must contain at least one await." );
        }

        // Combine all child blocks into a single parent block, with variables declared at the parent level
        return Block( variables, childBlockExpressions ); 
    }
}

public static partial class AsyncExpression
{
    public static AsyncBlockExpression BlockAsync( params Expression[] expressions )
    {
        return new AsyncBlockExpression( expressions );
    }

    public static AsyncBlockExpression BlockAsync( ParameterExpression[] variables, params Expression[] expressions )
    {
        return new AsyncBlockExpression( variables, expressions );
    }
}

