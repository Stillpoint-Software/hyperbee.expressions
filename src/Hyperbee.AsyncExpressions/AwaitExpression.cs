using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "Await {Target?.ToString(),nq}" )]
[DebuggerTypeProxy( typeof(AwaitExpressionDebuggerProxy) )]
public class AwaitExpression : Expression
{
    private readonly bool _configureAwait;
    private readonly Type _resultType;

    private static readonly MethodInfo AwaitMethod = typeof(AwaitExpression)
        .GetMethod( nameof(Await), BindingFlags.NonPublic | BindingFlags.Static );

    private static readonly MethodInfo AwaitResultMethod = typeof(AwaitExpression)
        .GetMethod( nameof(AwaitResult), BindingFlags.NonPublic | BindingFlags.Static );

    internal AwaitExpression( Expression asyncExpression, bool configureAwait )
    {
        Target = asyncExpression ?? throw new ArgumentNullException( nameof( asyncExpression ) );
        
        _configureAwait = configureAwait;
        _resultType = ResultType( Target.Type );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;
    public override Type Type => _resultType; 

    public Expression Target { get; }

    public override Expression Reduce()
    {
        return Call( 
            _resultType == typeof(void) || _resultType == typeof( IVoidTaskResult )  
                ? AwaitMethod 
                : AwaitResultMethod.MakeGenericMethod( _resultType ), 
            Target, 
            Constant( _configureAwait ) 
        );
    }

    private static Type ResultType( Type taskType )
    {
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

    private class AwaitExpressionDebuggerProxy( AwaitExpression node )
    {
        public Expression Target => node.Target;
        public Type Type => node.Type;
    }
}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait = false )
    {
        if ( expression is AsyncBlockExpression )
            return new AwaitExpression( expression, configureAwait );

        if ( !typeof(Task).IsAssignableFrom( expression.Type ) )
            throw new ArgumentException( "Expression must be of type Task.", nameof(expression) );

        return new AwaitExpression( expression, configureAwait );
    }
}
