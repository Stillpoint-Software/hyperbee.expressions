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
        // Define a parameter expression to represent 'this' within the state machine
        var stateMachineInstance = Expression.Parameter( _typeBuilder.AsType(), "stateMachine" );

        // Use variables from reducedBlock
        var variables = reducedBlock.Variables;

        var bodyExpressions = new List<Expression>
        {
            // Initialize the builder field
            Expression.Assign( Expression.Field( stateMachineInstance, _builderField ),
                Expression.Call( typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null ) )
        };

        var blocks = reducedBlock.Expressions;

        for ( var i = 0; i < blocks.Count; i++ )
        {
            var blockExpr = blocks[i] as BlockExpression;
            var lastExpression = blockExpr?.Expressions.Last();

            // Check if the last expression is an AwaitExpression
            if ( lastExpression is AwaitExpression awaitExpr )
            {
                var awaiterType = awaitExpr.Type.IsGenericType
                    ? typeof(ConfiguredTaskAwaitable<>).MakeGenericType( awaitExpr.Type.GetGenericArguments()[0] )
                    : typeof(ConfiguredTaskAwaitable);

                var awaiterField = _typeBuilder.DefineField( $"_awaiter_{i}", awaiterType.GetNestedType( "ConfiguredTaskAwaiter" )!, FieldAttributes.Private );

                // Assigning awaiter logic
                var assignAwaiter = Expression.Assign(
                    Expression.Field( stateMachineInstance, awaiterField ),
                    Expression.Call(
                        Expression.Call( awaitExpr, nameof(Task.ConfigureAwait), null, Expression.Constant( false ) ),
                        awaiterType.GetMethod( "GetAwaiter" )! )
                );

                // Generate proxy and continuation setup
                var stateMachineProxy = Expression.New( typeof(StateMachineProxy).GetConstructor( [typeof(IAsyncStateMachine)] )!, stateMachineInstance );
                var assignProxy = Expression.Assign( Expression.Field( stateMachineInstance, _proxyField ), stateMachineProxy );

                var setupContinuation = Expression.Call(
                    Expression.Field( stateMachineInstance, _builderField ),
                    nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
                    [awaiterType.GetNestedType( "ConfiguredTaskAwaiter" ), typeof(IAsyncStateMachine)],
                    Expression.Field( stateMachineInstance, awaiterField ),
                    Expression.Field( stateMachineInstance, _proxyField )
                );

                var moveToNextState = Expression.Assign( Expression.Field( stateMachineInstance, _stateField ), Expression.Constant( i + 1 ) );

                // Check if task is not completed
                var ifNotCompleted = Expression.IfThenElse(
                    Expression.IsFalse( Expression.Property( Expression.Field( stateMachineInstance, awaiterField ), nameof(TaskAwaiter.IsCompleted) ) ),
                    Expression.Block( assignAwaiter, assignProxy, setupContinuation, Expression.Return( Expression.Label( typeof(void) ) ) ),
                    Expression.Block( assignAwaiter, moveToNextState )
                );

                bodyExpressions.Add( Expression.IfThen( Expression.Equal( Expression.Field( stateMachineInstance, _stateField ), Expression.Constant( i ) ), ifNotCompleted ) );
            }
            else
            {
                // Handle non-awaitable final block
                var assignFinalResult = Expression.Assign( Expression.Field( stateMachineInstance, _finalResultField ), blockExpr! );
                bodyExpressions.Add( assignFinalResult );
            }
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

        // Compile to a method using Emit, replacing CompileToMethod
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
