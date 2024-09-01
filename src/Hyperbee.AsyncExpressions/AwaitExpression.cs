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

    private static readonly MethodInfo AwaitMethod = typeof(AwaitExpression).GetMethod( nameof(Await), BindingFlags.NonPublic | BindingFlags.Static );
    private static readonly MethodInfo AwaitResultMethod = typeof(AwaitExpression).GetMethod( nameof(AwaitResult), BindingFlags.NonPublic | BindingFlags.Static );

    internal AwaitExpression( Expression asyncExpression, bool configureAwait )
    {
        _asyncExpression = asyncExpression ?? throw new ArgumentNullException( nameof( asyncExpression ) );
        _configureAwait = configureAwait;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type
    {
        get
        {
            return _asyncExpression.Type.IsGenericType switch
            {
                true when _asyncExpression.Type.GetGenericTypeDefinition() == typeof( Task<> ) => _asyncExpression.Type.GetGenericArguments()[0],
                false when _asyncExpression.Type == typeof( Task ) => typeof( void ),
                _ => throw new InvalidOperationException( $"Unsupported type in {nameof(AwaitExpression)}." )
            };
        }
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return Call( Type == typeof( void ) 
            ? AwaitMethod 
            : AwaitResultMethod.MakeGenericMethod( Type ), _asyncExpression, Constant( _configureAwait ) );
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
        public Type ReturnType => node.Type;
    }
}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait )
    {
        return new AwaitExpression( expression, configureAwait );
    }
}
