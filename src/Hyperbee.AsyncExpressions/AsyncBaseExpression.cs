using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions;

[DebuggerTypeProxy( typeof( AsyncBaseExpressionDebuggerProxy ) )]
public abstract class AsyncBaseExpression : Expression
{
    private Expression _stateMachineBody;
    private bool _isReduced;

    private static readonly MethodInfo BuildStateMachineMethod = 
        typeof(AsyncBaseExpression).GetMethod( nameof(BuildStateMachine), BindingFlags.NonPublic | BindingFlags.Instance );

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachineBody;

        _stateMachineBody = InvokeBuildStateMachine();
        _isReduced = true;

        return _stateMachineBody;
    }

    protected abstract Expression PreReduce();

    protected abstract void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder );

    protected abstract Type GetResultType();

    private Expression InvokeBuildStateMachine()
    {
        PreReduce();  // BF (ME) - Moving Reduce logic to the ctor caused
                      // the base reduce to not be called or to be cyclical. This is a workaround.


        var resultType = GetResultType();
        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );

        return (Expression) buildStateMachine.Invoke( this, null );
    }

    private Expression BuildStateMachine<TResult>()
    {
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );

        ConfigureStateMachine( stateMachineBuilder );

        return stateMachineBuilder.CreateStateMachine();
    }

    internal Expression StartStateMachine()
    {
        var reducedExpression = Reduce();

        var stateMachineVariable = Variable( reducedExpression.Type, "stateMachineVariable" );
        var builderFieldInfo = reducedExpression.Type.GetField( "_builder" )!;
        var taskFieldInfo = builderFieldInfo.FieldType.GetProperty( "Task" )!;

        return Block(
            [stateMachineVariable],
            Assign( stateMachineVariable, reducedExpression ),
            Call( stateMachineVariable, "MoveNext", Type.EmptyTypes ),
            Property( Field( stateMachineVariable, builderFieldInfo ), taskFieldInfo )
        );
    }

    internal static bool IsTask( Type type )
    {
        return typeof( Task ).IsAssignableFrom( type );
    }

    private class AsyncBaseExpressionDebuggerProxy
    {
        private readonly AsyncBaseExpression _node;

        public AsyncBaseExpressionDebuggerProxy( AsyncBaseExpression node ) => _node = node;

        public Expression StateMachineBody => _node._stateMachineBody;
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node.Type;
    }
}
