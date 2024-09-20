using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "Await({_asyncExpression})" )]
[DebuggerTypeProxy( typeof(AwaitExpressionProxy) )]
public class AwaitExpression : Expression
{
    private readonly Expression _asyncExpression;
    private readonly bool _configureAwait;
    //private readonly Type _resultType;

    private static readonly MethodInfo AwaitMethod = typeof(AwaitExpression).GetMethod( nameof(Await), BindingFlags.NonPublic | BindingFlags.Static );
    private static readonly MethodInfo AwaitResultMethod = typeof(AwaitExpression).GetMethod( nameof(AwaitResult), BindingFlags.NonPublic | BindingFlags.Static );

    internal AwaitExpression( Expression asyncExpression, bool configureAwait )
    {
        _asyncExpression = asyncExpression ?? throw new ArgumentNullException( nameof( asyncExpression ) );
        _configureAwait = configureAwait;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    // TODO: Review with BF (fix caching the type)
    public override Type Type => ResultType( _asyncExpression.Type, false );
    public Type ReturnType => ResultType( _asyncExpression.Type, false );

    public Expression Target => _asyncExpression;

    public bool ReturnTask { get; set; }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( ReturnTask )
            return _asyncExpression;

        var resultType = ResultType( _asyncExpression.Type, ReturnTask );

        return Call( resultType == typeof(void) || resultType == typeof( IVoidTaskResult )  
            ? AwaitMethod 
            : AwaitResultMethod.MakeGenericMethod( resultType ), _asyncExpression, Constant( _configureAwait ) );
    }

    private Type ResultType( Type taskType, bool returnTask )
    {
        if ( returnTask )
            return taskType;

        return taskType.IsGenericType switch
        {
            true when taskType == typeof( Task<IVoidTaskResult> ) => typeof( void ),
            true when taskType.GetGenericTypeDefinition() == typeof( Task<> ) => taskType.GetGenericArguments()[0],
            false => typeof( void ),
            _ => throw new InvalidOperationException( $"Unsupported type in {nameof( AwaitExpression )}." )
        };
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
        public Type ReturnType => node.ReturnType;
    }
}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait = false )
    {
        return new AwaitExpression( expression, configureAwait );
    }
}
