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

    private static MethodInfo AwaitTaskMethod { get; }

    private static MethodInfo AwaitTaskTMethod { get; }

    static AwaitExpression()
    {
        var methods = typeof(AwaitExpression)
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
            .Where( x => x.Name == nameof(Await) )
            .ToArray();

        AwaitTaskMethod = methods.Single( x => !x.IsGenericMethodDefinition );
        AwaitTaskTMethod = methods.Single( x => x.IsGenericMethodDefinition );
    }

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
            ? AwaitTaskMethod 
            : AwaitTaskTMethod.MakeGenericMethod( Type ), _asyncExpression, Constant( _configureAwait ) );
    }

    private static void Await( Task task, bool configureAwait )
    {
        task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
    }

    private static T Await<T>( Task<T> task, bool configureAwait )
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
