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

        _stateMachine = StateMachineBuilder.Create( Block( _invocationExpression ), Type, createRunner: true );
        _isReduced = true;

        return _stateMachine;
    }

    public override Type Type
    {
        get
        {
            var returnType = _invocationExpression.Type;

            return IsTask( returnType ) && returnType.IsGenericType
                ? returnType.GetGenericArguments()[0]
                : typeof(void);
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
