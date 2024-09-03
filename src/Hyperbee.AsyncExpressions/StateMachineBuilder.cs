using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

// ReSharper disable once InconsistentNaming
internal interface VoidResult;

public class StateMachineBuilder<TResult>
{
    private readonly TypeBuilder _typeBuilder;
    private readonly FieldBuilder _stateField;
    private readonly FieldBuilder _builderField;
    private readonly FieldBuilder _finalResultField;
    private readonly MethodBuilder _moveNextMethod;
    private readonly FieldBuilder _proxyField;

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        // Define a new type that implements IAsyncStateMachine
        _typeBuilder = moduleBuilder.DefineType( typeName, TypeAttributes.Public, typeof( object ), [typeof( IAsyncStateMachine )] );
        _stateField = _typeBuilder.DefineField( "_state", typeof( int ), FieldAttributes.Private );
        _builderField = _typeBuilder.DefineField( "_builder", typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ), FieldAttributes.Private );
        _finalResultField = _typeBuilder.DefineField( "_finalResult", typeof( TResult ), FieldAttributes.Private );

        // Define a constructor for the state machine type
        var constructor = _typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes );
        var ilGenerator = constructor.GetILGenerator();
        ilGenerator.Emit( OpCodes.Ldarg_0 );
        ilGenerator.Emit( OpCodes.Call, typeof( object ).GetConstructor( Type.EmptyTypes )! );
        ilGenerator.Emit( OpCodes.Ret );

        // Define the MoveNext method that will contain the state machine logic
        _moveNextMethod = _typeBuilder.DefineMethod( nameof( IAsyncStateMachine.MoveNext ), MethodAttributes.Public | MethodAttributes.Virtual, typeof( void ), Type.EmptyTypes );

        // Define a field to store a proxy for the state machine, used for continuation
        _proxyField = _typeBuilder.DefineField( "_stateMachineProxy", typeof( StateMachineProxy ), FieldAttributes.Private );
    }

    public void GenerateMoveNextMethod( BlockExpression reducedBlock )
    {
        // Use variables from reducedBlock
        var variables = reducedBlock.Variables;

        // Define a parameter to represent the instance of the state machine class
        var stateMachineInstance = Expression.Parameter( _typeBuilder, "stateMachine" );

        var bodyExpressions = new List<Expression>
        {
            Expression.Assign( Expression.Field( stateMachineInstance, _builderField ),
                Expression.Call( typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null ) )
        };

        var blocks = reducedBlock.Expressions;
        for ( var i = 0; i < blocks.Count; i++ )
        {
            var blockExpr = blocks[i];

            var configuredTaskAwaitableType = typeof(ConfiguredTaskAwaitable<>).MakeGenericType( typeof(TResult) );
            var configuredTaskAwaiterType = configuredTaskAwaitableType.GetNestedType( "ConfiguredTaskAwaiter" );

            var awaiterField = _typeBuilder.DefineField( $"_awaiter_{i}", configuredTaskAwaiterType!, FieldAttributes.Private );

            var stateCheck = Expression.Equal( Expression.Field( stateMachineInstance, _stateField ), Expression.Constant( i ) );
            var assignAwaiter = Expression.Assign(
                Expression.Field( stateMachineInstance, awaiterField ),
                Expression.Call(
                    Expression.Call( blockExpr, nameof(Task.ConfigureAwait), null, Expression.Constant( false ) ),
                    nameof(ConfiguredTaskAwaitable<TResult>.GetAwaiter), null )
            );

            var stateMachineProxy = Expression.New( typeof(StateMachineProxy).GetConstructor( new[] { typeof(IAsyncStateMachine) } )!, stateMachineInstance );
            var assignProxy = Expression.Assign( Expression.Field( stateMachineInstance, _proxyField ), stateMachineProxy );

            var setupContinuation = Expression.Call(
                Expression.Field( stateMachineInstance, _builderField ),
                nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
                new Type[] { configuredTaskAwaiterType, typeof(IAsyncStateMachine) },
                Expression.Field( stateMachineInstance, awaiterField ),
                Expression.Field( stateMachineInstance, _proxyField )
            );

            var moveToNextState = Expression.Assign( Expression.Field( stateMachineInstance, _stateField ), Expression.Constant( i + 1 ) );

            var ifNotCompleted = Expression.IfThenElse(
                Expression.IsFalse( Expression.Property( Expression.Field( stateMachineInstance, awaiterField ), nameof(TaskAwaiter.IsCompleted) ) ),
                Expression.Block( assignAwaiter, assignProxy, setupContinuation, Expression.Return( Expression.Label( typeof(void) ) ) ),
                Expression.Block( assignAwaiter, moveToNextState )
            );

            bodyExpressions.Add( Expression.IfThen( stateCheck, ifNotCompleted ) );
        }

        var setResult = Expression.Call(
            Expression.Field( stateMachineInstance, _builderField ),
            nameof(AsyncTaskMethodBuilder<TResult>.SetResult),
            null,
            Expression.Field( stateMachineInstance, _finalResultField )
        );

        bodyExpressions.Add( setResult );

        // Include the variables in the block
        var stateMachineBody = Expression.Block( variables, bodyExpressions );

        // Emit the state machine body to the MoveNext method
        EmitCompileToMethod( stateMachineBody, _moveNextMethod );
    }


    private void EmitCompileToMethod( Expression stateMachineBody, MethodBuilder methodBuilder )
    {
        // compile the generated state machine into a method

        var lambda = Expression.Lambda<Action>( stateMachineBody );
        var compiledLambda = lambda.Compile();
        var ilGenerator = methodBuilder.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 );
        ilGenerator.Emit( OpCodes.Call, compiledLambda.Method );
        ilGenerator.Emit( OpCodes.Ret );
    }

    public Type CreateStateMachineType()
    {
        // finalize and create the state machine type
        _typeBuilder.DefineMethodOverride( _moveNextMethod, typeof( IAsyncStateMachine ).GetMethod( nameof( IAsyncStateMachine.MoveNext ) )! );
        return _typeBuilder.CreateTypeInfo().AsType();
    }
}

// Proxy class to delegate state transitions in the state machine
public sealed class StateMachineProxy( IAsyncStateMachine stateMachine ) : IAsyncStateMachine
{
    private IAsyncStateMachine _innerStateMachine = stateMachine ?? throw new ArgumentNullException( nameof( stateMachine ) );

    public void MoveNext() => _innerStateMachine.MoveNext();

    public void SetStateMachine( IAsyncStateMachine stateMachine ) => _innerStateMachine = stateMachine;
}
