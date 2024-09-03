using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_body}" )]
[DebuggerTypeProxy( typeof(AsyncBaseExpressionDebuggerProxy) )]
public abstract class AsyncBaseExpression : Expression
{
    private readonly Expression[] _body;
    protected bool _isReduced;
    protected Expression _stateMachineBody; 

    protected AsyncBaseExpression( Expression[] body )
    {
        _body = body;
    }

    public override Type Type => GetFinalResultType();

    public override ExpressionType NodeType => ExpressionType.Extension;

    protected abstract Type GetFinalResultType();

    protected abstract void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder );

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachineBody;

        var finalResultType = GetFinalResultType();
        var stateMachineResultType = finalResultType == typeof(void) ? typeof(VoidResult) : finalResultType;

        var buildStateMachine = typeof(AsyncBaseExpression)
            .GetMethod( nameof(BuildStateMachine), BindingFlags.NonPublic | BindingFlags.Instance )!
            .MakeGenericMethod( stateMachineResultType );

        _stateMachineBody = (Expression) buildStateMachine.Invoke( this, null );
        _isReduced = true;

        return _stateMachineBody!;
    }

    private MethodCallExpression BuildStateMachine<TResult>()
    {
        // Create a dynamic assembly and module for the state machine
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        // Create a state machine builder
        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );

        // Delegate to the derived class to configure the builder
        ConfigureStateMachine( stateMachineBuilder );

        // Create the state machine type
        var stateMachineType = stateMachineBuilder.CreateStateMachineType();

        // Create a proxy expression for handling MoveNext and SetStateMachine calls
        var proxyConstructor = typeof(StateMachineProxy).GetConstructor( new[] { typeof(IAsyncStateMachine) } );
        var stateMachineInstance = Expression.New( stateMachineType );
        var proxyInstance = Expression.New( proxyConstructor!, stateMachineInstance );

        // Build an expression that represents invoking the MoveNext method on the proxy
        var moveNextMethod = typeof(IAsyncStateMachine).GetMethod( nameof(IAsyncStateMachine.MoveNext) );
        var moveNextCall = Expression.Call( proxyInstance, moveNextMethod! );

        return moveNextCall;
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

        public Expression[] Body => _node._body;
        public Expression StateMachineBody => _node._stateMachineBody; 
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node.Type;
    }
}
