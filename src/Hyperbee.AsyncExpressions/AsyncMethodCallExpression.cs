using System.Reflection;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncMethodCallExpression : AsyncBaseExpression
{
    private readonly MethodCallExpression _methodCallExpression;
    private bool _isReduced;
    private Expression _stateMachine;

    public AsyncMethodCallExpression( MethodCallExpression methodCallExpression )
    {
        _methodCallExpression = methodCallExpression ?? throw new ArgumentNullException( nameof(methodCallExpression) );
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachine;

        _stateMachine = StateMachineBuilder.Create( Block( _methodCallExpression ), Type, createRunner: true );
        _isReduced = true;

        return _stateMachine;
    }

    public override Type Type
    {
        get
        {
            var returnType = _methodCallExpression.Type;

            return IsTask( returnType ) && returnType.IsGenericType
                ? returnType.GetGenericArguments()[0]
                : typeof(void);
        }
    }
}


public static partial class AsyncExpression
{
    public static AsyncBaseExpression CallAsync( MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method does not return a Task.", nameof( methodInfo ) );

        return new AsyncMethodCallExpression( Expression.Call( methodInfo, arguments ) );
    }

    public static AsyncBaseExpression CallAsync( Expression instance, MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method does not return a Task.", nameof( methodInfo ) );

        return new AsyncMethodCallExpression( Expression.Call( instance, methodInfo, arguments ) );
    }
}
