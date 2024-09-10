using System.Diagnostics;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression : AsyncBaseExpression
{
    private readonly Expression[] _expressions;
    private readonly ParameterExpression[] _initialVariables;

    private bool _isReduced;
    private Expression _stateMachine;
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

        var splitVisitor = new AwaitSplitVisitor();
        foreach ( var expr in expressions )
        {
            splitVisitor.Visit( expr );
        }
        _expressions = splitVisitor.Expressions.ToArray();
    }

    public override Type Type
    {
        get
        {
            if ( !_isReduced )
                Reduce();

            return _stateMachine.Type;
        }
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachine;

        var reducedBlock = ReduceBlock( out _resultType );
        _stateMachine = StateMachineBuilder.Create( reducedBlock, _resultType, createRunner: true );

        _isReduced = true;

        return _stateMachine;
    }

    internal BlockExpression ReduceBlock( out Type resultType )
    {
        var childBlockExpressions = new List<Expression>();
        var currentBlockExpressions = new List<Expression>();
        var awaitEncountered = false;

        var variables = new HashSet<ParameterExpression>( _initialVariables );
        
        resultType = typeof(void);

        foreach ( var expr in _expressions )
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
                case BinaryExpression { Left: ParameterExpression varExpr }:
                    variables.Add( varExpr );
                    break;
                case AwaitResultExpression { InnerVariable: ParameterExpression parameter }:
                    // TODO: Review with BF (tracking variables)
                    variables.Add( parameter );
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

    private class AsyncBlockExpressionDebuggerProxy
    {
        private readonly AsyncBlockExpression _node;

        public AsyncBlockExpressionDebuggerProxy( AsyncBlockExpression node ) => _node = node;

        public Expression StateMachine => _node._stateMachine;
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node._resultType;

        public Expression[] Expressions => _node._expressions;
        public ParameterExpression[] InitialVariables => _node._initialVariables;
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

