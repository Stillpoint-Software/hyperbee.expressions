using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_asyncExpression}" )]
[DebuggerTypeProxy( typeof(AwaitExpressionProxy) )]
public class AwaitExpression : Expression
{
    private readonly Expression _asyncExpression;
    private readonly bool _configureAwait;
    private readonly Type _resultType;

    private static readonly MethodInfo AwaitMethod = typeof(AwaitExpression).GetMethod( nameof(Await), BindingFlags.NonPublic | BindingFlags.Static );
    private static readonly MethodInfo AwaitResultMethod = typeof(AwaitExpression).GetMethod( nameof(AwaitResult), BindingFlags.NonPublic | BindingFlags.Static );

    internal AwaitExpression( Expression asyncExpression, bool configureAwait )
    {
        _asyncExpression = asyncExpression ?? throw new ArgumentNullException( nameof( asyncExpression ) );
        _configureAwait = configureAwait;
        _resultType = ResultType( asyncExpression.Type );
    }

    
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => _resultType;
    
    private Type ResultType( Type taskType )
    {
        if ( ReturnTask )
            return taskType;

        return taskType.IsGenericType switch
        {
            true when taskType.GetGenericTypeDefinition() == typeof(Task<>) => taskType.GetGenericArguments()[0],
            false => typeof(void),
            _ => throw new InvalidOperationException( $"Unsupported type in {nameof(AwaitExpression)}." )
        };
    }

    public bool ReturnTask { get; set; }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( ReturnTask )
            return _asyncExpression;

        var awaitExpression = Call( _resultType == typeof( void ) 
            ? AwaitMethod 
            : AwaitResultMethod.MakeGenericMethod( _resultType ), _asyncExpression, Constant( _configureAwait ) );

        return awaitExpression;
    }

    private static void Await( Task task, bool configureAwait )
    {
        task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
    }

    private static T AwaitResult<T>( Task<T> task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
    }

    private class AwaitExpressionProxy( AwaitExpression node )
    {
        public Expression Target => node._asyncExpression;
        public Type ReturnType => node._resultType;
    }
}


[DebuggerDisplay( "{_asyncExpression}" )]
[DebuggerTypeProxy( typeof(AwaitableExpressionProxy) )]
public class AwaitableExpression : Expression
{
    private readonly Expression _asyncExpression;

    internal AwaitableExpression( Expression asyncExpression )
    {
        _asyncExpression = asyncExpression;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => _asyncExpression.Type;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return _asyncExpression;
    }

    private class AwaitableExpressionProxy( AwaitableExpression node )
    {
        public Expression Target => node._asyncExpression;
        public Type ReturnType => node.Type;
    }
}


public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait )
    {
        return new AwaitExpression( expression, configureAwait );
    }

    public static AwaitableExpression Awaitable( Expression expression )
    {
        return new AwaitableExpression( expression );
    }
}
