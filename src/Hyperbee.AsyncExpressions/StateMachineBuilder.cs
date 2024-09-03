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
        // Section: Initialization
        // Use variables from reducedBlock to ensure variable scope is managed correctly.
        var variables = reducedBlock.Variables;

        // List to hold all expressions that make up the state machine's body
        var bodyExpressions = new List<Expression>
        {
            // Initialize the AsyncTaskMethodBuilder field
            Expression.Assign(Expression.Field(Expression.Constant(this), _builderField),
                Expression.Call(typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null))
        };

        // Section: State Handling
        // Iterate through each block to define state transitions
        var blocks = reducedBlock.Expressions;
        for ( var i = 0; i < blocks.Count; i++ )
        {
            var blockExpr = blocks[i];

            // Define the types for the ConfiguredTaskAwaitable and its awaiter
            var configuredTaskAwaitableType = typeof( ConfiguredTaskAwaitable<> ).MakeGenericType( typeof( TResult ) );
            var configuredTaskAwaiterType = configuredTaskAwaitableType.GetNestedType( "ConfiguredTaskAwaiter" );

            // Define a field to hold the awaiter for this state
            var awaiterField = _typeBuilder.DefineField( $"_awaiter_{i}", configuredTaskAwaiterType!, FieldAttributes.Private );

            // Check if the current state matches
            var stateCheck = Expression.Equal( Expression.Field( Expression.Constant( this ), _stateField ), Expression.Constant( i ) );

            // Assign the awaiter
            var assignAwaiter = Expression.Assign(
                Expression.Field( Expression.Constant( this ), awaiterField ),
                Expression.Call(
                    Expression.Call( blockExpr, nameof( Task.ConfigureAwait ), null, Expression.Constant( false ) ),
                    nameof( ConfiguredTaskAwaitable<TResult>.GetAwaiter ), null )
            );

            // Create the StateMachineProxy instance and assign it
            var stateMachineProxy = Expression.New( typeof( StateMachineProxy ).GetConstructor( [typeof( IAsyncStateMachine )] )!, Expression.Constant( this ) );
            var assignProxy = Expression.Assign( Expression.Field( Expression.Constant( this ), _proxyField ), stateMachineProxy );

            // Setup continuation with the builder, using the awaiter and the state machine proxy
            var setupContinuation = Expression.Call(
                Expression.Field( Expression.Constant( this ), _builderField ),
                nameof( AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted ),
                [configuredTaskAwaiterType, typeof( IAsyncStateMachine )],
                Expression.Field( Expression.Constant( this ), awaiterField ),
                Expression.Field( Expression.Constant( this ), _proxyField )
            );

            // Move to the next state
            var moveToNextState = Expression.Assign( Expression.Field( Expression.Constant( this ), _stateField ), Expression.Constant( i + 1 ) );

            // Section: State Execution Logic
            // Check if the task is completed or needs to await
            var ifNotCompleted = Expression.IfThenElse(
                Expression.IsFalse( Expression.Property( Expression.Field( Expression.Constant( this ), awaiterField ), nameof( TaskAwaiter.IsCompleted ) ) ),
                Expression.Block( assignAwaiter, assignProxy, setupContinuation, Expression.Return( Expression.Label( typeof( void ) ) ) ),
                Expression.Block( assignAwaiter, moveToNextState )
            );

            // Add the state check and logic to the body expressions
            bodyExpressions.Add( Expression.IfThen( stateCheck, ifNotCompleted ) );
        }

        // Section: Final State
        // Set the final result of the async operation
        var setResult = Expression.Call(
            Expression.Field( Expression.Constant( this ), _builderField ),
            nameof( AsyncTaskMethodBuilder<TResult>.SetResult ),
            null,
            Expression.Field( Expression.Constant( this ), _finalResultField )
        );

        bodyExpressions.Add( setResult );

        // Section: Emit and Compile
        // Include the variables in the block to maintain their scope and compile the MoveNext method
        var stateMachineBody = Expression.Block( variables, bodyExpressions );
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
