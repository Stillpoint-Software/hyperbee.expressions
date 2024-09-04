using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

[DebuggerTypeProxy( typeof( AsyncBaseExpressionDebuggerProxy ) )]
public abstract class AsyncBaseExpression : Expression
{
    private Expression _stateMachineBody;
    private bool _isReduced;

    public override Expression Reduce()
    {
        if ( !_isReduced )
        {
            _stateMachineBody = InvokeBuildStateMachine();
            _isReduced = true;
        }
        return _stateMachineBody;
    }

    protected abstract void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder );

    protected abstract Type GetFinalResultType();

    private Expression InvokeBuildStateMachine()
    {
        var finalResultType = GetFinalResultType();
        var buildMethod = typeof(AsyncBaseExpression)
            .GetMethods( BindingFlags.Instance | BindingFlags.NonPublic )
            .First( m => m.Name == nameof(BuildStateMachine) )
            .MakeGenericMethod( finalResultType );
        return (Expression) buildMethod.Invoke( this, null );
    }

    private Expression BuildStateMachine<TResult>()
    {
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );

        ConfigureStateMachine( stateMachineBuilder );

        var stateMachineType = stateMachineBuilder.CreateStateMachine();
        return stateMachineType;
    }

    // TODO: Implement this method
    public Expression StartStateMachine()
    {
        // return hot task that:
        //
        // creates a new instance of the state machine
        // calls the MoveNext method
        // returns the hot task

        return null;
    }

    internal static bool IsTask( Type type )
    {
        return typeof( Task ).IsAssignableFrom( type );
    }

    private class AsyncBaseExpressionDebuggerProxy
    {
        private readonly AsyncBaseExpression _node;

        public AsyncBaseExpressionDebuggerProxy( AsyncBaseExpression node )
        {
            _node = node;
        }

        //public Expression[] Body => _node._body;
        public Expression StateMachineBody => _node._stateMachineBody;
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node.Type;
    }
}
