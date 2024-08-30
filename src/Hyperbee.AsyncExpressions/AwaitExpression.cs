using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

public class AwaitExpression : Expression
{
    private readonly Expression _asyncExpression;
    private readonly bool _configureAwait;

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
            var taskType = _asyncExpression.Type;

            if ( taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof( Task<> ) )
            {
                return taskType.GetGenericArguments()[0];
            }

            return taskType == typeof( Task )
                ? typeof( void )
                : throw new InvalidOperationException( "Unsupported type in AwaitExpression." );
        }
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( Type == typeof( void ) )
        {
            var awaitMethod = typeof( AwaitExpression )
                .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
                .First( m => m.Name == nameof( Await ) && !m.IsGenericMethodDefinition );

            return Call( awaitMethod!, _asyncExpression, Constant( _configureAwait ) );
        }
        else
        {
            var awaitMethod = typeof( AwaitExpression )
                .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
                .First( m => m.Name == nameof( Await ) && m.IsGenericMethodDefinition );

            var genericAwaitMethod = awaitMethod.MakeGenericMethod( Type );

            return Call( genericAwaitMethod, _asyncExpression, Constant( _configureAwait ) );
        }
    }

    private static void Await( Task task, bool configureAwait )
    {
        task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
    }

    private static T Await<T>( Task<T> task, bool configureAwait )
    {
        var awaiter = task.ConfigureAwait( configureAwait ).GetAwaiter();
        var result = awaiter.GetResult();
        return result;
    }

}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait )
    {
        return new AwaitExpression( expression, configureAwait );
    }
}
