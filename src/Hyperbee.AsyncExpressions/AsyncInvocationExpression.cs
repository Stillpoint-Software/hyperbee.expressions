using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncBaseExpression
{
    private readonly InvocationExpression _invocationExpression;
    private bool _isReduced;
    private Expression _stateMachine;

    public AsyncInvocationExpression( InvocationExpression invocationExpression )
    {
        _invocationExpression = invocationExpression ?? throw new ArgumentNullException( nameof(invocationExpression) );
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachine;

        var resultType = ResultType( _invocationExpression.Type );

        var transformer = new GotoTransformerVisitor();
        var transformResult = transformer.Transform( this );
        transformer.PrintStateMachine();

        _stateMachine = StateMachineBuilder.Create( transformResult, resultType, createRunner: true );
        _isReduced = true;

        return _stateMachine;

        static Type ResultType( Type returnType )
        {
            return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? returnType.GetGenericArguments()[0]
                : typeof(void);
        }
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
}

public static partial class AsyncExpression
{
    public static AsyncBaseExpression InvokeAsync( LambdaExpression lambdaExpression, params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( lambdaExpression.ReturnType ) )
            throw new ArgumentException( "The specified lambda does not return a Task.", nameof( lambdaExpression ) );

        return new AsyncInvocationExpression( Expression.Invoke( lambdaExpression, arguments ) );
    }
}
