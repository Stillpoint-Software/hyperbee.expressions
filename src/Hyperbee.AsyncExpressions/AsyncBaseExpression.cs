using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_body}" )]
[DebuggerTypeProxy( typeof(AsyncBaseExpressionDebuggerProxy) )]
public abstract class AsyncBaseExpression : Expression
{
    protected Expression _body;
    protected bool _isReduced;
    protected Expression _stateMachineBody; 

    protected AsyncBaseExpression( Expression body )
    {
        _body = body;
    }

    public override Type Type => GetFinalResultType();

    public override ExpressionType NodeType => ExpressionType.Extension;

    protected abstract Type GetFinalResultType();

    protected abstract Expression BuildStateMachine<TResult>();

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachineBody;

        var finalResultType = GetFinalResultType();
        
        var buildStateMachine = typeof(AsyncBaseExpression)
            .GetMethod( nameof(BuildStateMachine), BindingFlags.NonPublic | BindingFlags.Instance )!
            .MakeGenericMethod( finalResultType );

        _stateMachineBody = (Expression) buildStateMachine.Invoke( this, null );
        _isReduced = true;

        return _stateMachineBody!;
    }

    internal static bool IsTask( Type returnType )
    {
        return returnType == typeof(Task) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    private class AsyncBaseExpressionDebuggerProxy
    {
        private readonly AsyncBaseExpression _node;

        public AsyncBaseExpressionDebuggerProxy( AsyncBaseExpression node )
        {
            _node = node;
        }

        public Expression Body => _node._body;
        public Expression StateMachineBody => _node._stateMachineBody; 
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node.Type;
    }
}
