using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

#if STATEMACHINE_LOGGER
internal static class Debug
{
    public static void Log( string message )
    {
        Console.WriteLine( message );
    }

    public static MethodCallExpression LogCall( string message )
    {
        return Expression.Call( typeof( Debug ).GetMethod( "Log" )!, Expression.Constant( message ) );
    }
}
#endif

public interface IVoidTaskResult; // Marker interface for void Task results

public class StateMachineBuilder<TResult>
{
    private BlockExpression _blockSource;

    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;
    private Type _stateMachineType;
    private TypeBuilder _typeBuilder;
    private FieldBuilder _builderField;
    private FieldBuilder _finalResultField;
    private FieldBuilder _moveNextLambdaField;
    private List<FieldBuilder> _variableFields;
    private List<FieldBuilder> _awaiterFields;

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public void SetSource( BlockExpression blockSource )
    {
        _blockSource = blockSource;
    }

    public Expression CreateStateMachine( bool createRunner = true )
    {
        if ( _blockSource == null )
        {
            throw new InvalidOperationException( "Source must be set before creating state machine." );
        }

        // Create the state machine type
        CreateStateMachineType( _blockSource );

        // Compile MoveNext lambda and assign to state machine
        var moveNextLambda = CreateMoveNextExpression( _blockSource );

        var stateMachineVariable = Expression.Variable( _stateMachineType, "stateMachine" );
        var builderFieldInfo = _stateMachineType.GetField( "_builder" )!;
        var setLambdaMethod = _stateMachineType.GetMethod( "SetMoveNext" )!;

        var constructor = _stateMachineType.GetConstructor( Type.EmptyTypes )!;

        var stateMachineExpression = Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, Expression.New( constructor ) ),
            Expression.Assign(
                Expression.Field( stateMachineVariable, builderFieldInfo ),
                Expression.Call( typeof( AsyncTaskMethodBuilder<TResult> ), nameof( AsyncTaskMethodBuilder<TResult>.Create ), null )
            ),
            Expression.Call( stateMachineVariable, setLambdaMethod, moveNextLambda ),
            stateMachineVariable
        );

        return createRunner ? CreateRunStateMachine( stateMachineExpression ) : stateMachineExpression;
    }

    public Expression CreateRunStateMachine( Expression stateMachineExpression )
    {
        // Define the RunStateMachine method
        //
        // public Task<TResult> RunStateMachine( StateMachineType stateMachine )
        // {
        //     stateMachine._builder.Start<StateMachineType>( ref stateMachine );
        //     return stateMachine._builder.Task;
        // }

        var stateMachineVariable = Expression.Variable( _stateMachineType, "stateMachine" );

        var builderFieldInfo = _stateMachineType.GetField( "_builder" )!;
        var taskFieldInfo = builderFieldInfo.FieldType.GetProperty( "Task" )!;

        var builderField = Expression.Field( stateMachineVariable, builderFieldInfo );

        var startMethod = typeof( AsyncTaskMethodBuilder<> )
            .MakeGenericType( typeof( TResult ) )
            .GetMethod( "Start" )!
            .MakeGenericMethod( _stateMachineType );

        var callBuilderStart = Expression.Call(
            builderField,
            startMethod,
            stateMachineVariable // ref stateMachine
        );

        return Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, stateMachineExpression ),
            callBuilderStart,
            Expression.Property( builderField, taskFieldInfo )
        );
    }

    private void CreateStateMachineType( BlockExpression block )
    {
        // Define the state machine type
        //
        // public class StateMachineType : IAsyncStateMachine
        // {
        //      public int _state;
        //      public AsyncTaskMethodBuilder<TResult> _builder;
        //      public TResult _finalResult;
        //      public Action _moveNextLambda;
        //      
        //      // Variables (example)
        //      public int _variable1;
        //      public int _variable2;
        //
        //      // Awaiters (example)
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter1;
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter2;
        //
        //      public StateMachineType()
        //      {
        //      }
        //
        //      public void SetLambda<T>(Action<T> moveNextLambda)
        //      {
        //         Action<object> moveNext = obj => moveNextLambda( (StateMachineType) obj );
        //         moveNext(this);
        //      }
        //
        //      public void MoveNext() => _moveNextLambda(this);
        //      public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine( stateMachine );
        // }

        _typeBuilder = _moduleBuilder.DefineType( _typeName, TypeAttributes.Public, typeof( object ), [typeof( IAsyncStateMachine )] );

        _typeBuilder.DefineField( "_state", typeof( int ), FieldAttributes.Public );
        _builderField = _typeBuilder.DefineField( "_builder", typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ), FieldAttributes.Public );
        _finalResultField = _typeBuilder.DefineField( "_finalResult", typeof( TResult ), FieldAttributes.Public );
        _moveNextLambdaField = _typeBuilder.DefineField( "_moveNextLambda", typeof( Action<> ).MakeGenericType( _typeBuilder ), FieldAttributes.Private );

        EmitBlockFields( block );
        EmitConstructor();
        EmitSetMoveNextMethod();
        EmitMoveNextMethod();
        EmitSetStateMachineMethod();

        _stateMachineType = _typeBuilder.CreateType();
    }

    private void EmitConstructor()
    {
        // Define a parameterless constructor: public StateMachineType()
        var constructor = _typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes );
        var ilGenerator = constructor.GetILGenerator();

        // Call the base constructor (object)
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Call, typeof( object ).GetConstructor( Type.EmptyTypes )! ); // base()
        ilGenerator.Emit( OpCodes.Ret ); // return
    }

    private void EmitBlockFields( BlockExpression block )
    {
        // Define: variable fields
        _variableFields = [];
        foreach ( var variable in block.Variables )
        {
            var field = _typeBuilder.DefineField( $"_{variable.Name}", variable.Type, FieldAttributes.Public );
            _variableFields.Add( field );
        }

        // Define: awaiter fields
        _awaiterFields = [];
        for ( var i = 0; i < block.Expressions.Count; i++ )
        {
            var expr = block.Expressions[i];

            if ( !TryMakeAwaiterType( expr, out Type awaiterType ) )
                continue; // Not an awaitable expression

            var fieldName = $"_awaiter_{i}"; // `i` should match the index of the expression to align with state machine logic

            var awaiterField = _typeBuilder.DefineField( fieldName, awaiterType, FieldAttributes.Public );
            _awaiterFields.Add( awaiterField );
        }
    }

    private void EmitSetMoveNextMethod()
    {
        // Define the SetMoveNext method
        //
        //  public void SetMoveNext<T>(Action<T> moveNext)
        //  {
        //     _moveNextLambda = moveNext;
        //  }

        var setMoveNextMethod = _typeBuilder.DefineMethod(
            "SetMoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof( void ),
            [typeof( Action<> ).MakeGenericType( _typeBuilder )]
        );

        var ilGenerator = setMoveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, _moveNextLambdaField ); // this._moveNextLambda = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret ); // return
    }

    private void EmitMoveNextMethod()
    {
        // Define the MoveNext method
        //
        //  public void MoveNext()
        //  {
        //      Action<object> moveNext = obj => _moveNextLambda( (StateMachineTypeN) obj );
        //      moveNext( this );
        //  }

        var moveNextMethod = _typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof( void ),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this`
        ilGenerator.Emit( OpCodes.Ldfld, _moveNextLambdaField ); // load `_moveNextLambda`
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this` as lambda argument

        var actionObjectType = typeof( Action<object> );
        var invokeMethod = actionObjectType.GetMethod( "Invoke" );
        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! ); // Call Action<object>.Invoke(this)

        ilGenerator.Emit( OpCodes.Ret );
    }

    private void EmitSetStateMachineMethod()
    {
        // Define the IAsyncStateMachine.SetStateMachine method
        //
        // public void SetStateMachine( IAsyncStateMachine stateMachine )
        // {
        //    _builder.SetStateMachine( stateMachine );
        // }

        var setStateMachineMethod = _typeBuilder.DefineMethod(
            "SetStateMachine",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof( void ),
            [typeof( IAsyncStateMachine )]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this`
        ilGenerator.Emit( OpCodes.Ldfld, _builderField ); // load `_builder`
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // Load the `stateMachine` parameter

        var setStateMachineOnBuilder = typeof( AsyncTaskMethodBuilder<> )
            .MakeGenericType( typeof( TResult ) )
            .GetMethod( "SetStateMachine", [typeof( IAsyncStateMachine )] );

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        _typeBuilder.DefineMethodOverride( setStateMachineMethod,
            typeof( IAsyncStateMachine ).GetMethod( "SetStateMachine" )!
        );
    }


    private LambdaExpression CreateMoveNextExpression( BlockExpression block )
    {
        // Example of a typical state-machine:
        //
        // public void MoveNext()
        // {
        //     try
        //     {
        //         if (_state == 0)
        //         {
        //             _awaiter1 = task1.ConfigureAwait(false).GetAwaiter();
        //             _state = 1;
        //
        //             if (!_awaiter1.IsCompleted == false)
        //             {
        //                 _builder.AwaitUnsafeOnCompleted(ref _awaiter1, this);
        //                 return;
        //             }
        //         }
        //
        //         if (_state == 1)
        //         {
        //             _awaiter1.GetResult();
        //             _awaiter2 = task2.ConfigureAwait(false).GetAwaiter();
        //             _state = 2;
        //
        //             if (!_awaiter2.IsCompleted)
        //             {
        //                 _builder.AwaitUnsafeOnCompleted(ref _awaiter2, this);
        //                 return;
        //             }
        //         }
        //
        //         if (_state == 2)
        //         {
        //             _builder.Task.SetResult( _awaiter2.GetResult() );
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _builder.SetException(ex);
        //     }
        // }
        var stateMachineInstance = Expression.Parameter( _stateMachineType, "stateMachine" );
        var parameterVisitor = new ParameterMappingVisitor( stateMachineInstance, _variableFields );

        var buildFieldInfo = GetFieldInfo( _stateMachineType, _builderField );
        var finalResultFieldInfo = GetFieldInfo( _stateMachineType, _finalResultField );

        var bodyExpressions = new List<Expression>();

        var blocks = block.Expressions;
        int lastBlockIndex = blocks.Count - 1;

        LabelTarget returnLabel = Expression.Label( "ExitMoveNext" );

        FieldInfo lastAwaitField = null; // TODO: Review with BF
        bool handledFinalBlock = false;  // TODO: Review with BF

        // Iterate through the blocks (each block is a state)
        for ( var i = 0; i <= lastBlockIndex; i++ )
        {
            // ensure awaited results have state machine instance set
            if ( blocks[i] is BlockExpression blockExpression && blockExpression.Expressions.First() is AwaitResultExpression resultExpression )
            {
                resultExpression.InitializeAwaiter( stateMachineInstance, lastAwaitField ); 
            }

            var blockExpr = parameterVisitor.Visit( blocks[i] );
            var blockReturnType = blockExpr.Type;

            if ( AsyncBaseExpression.IsTask( blockReturnType ) )
            {
                // Task-based state
                lastAwaitField = GetFieldInfo( _stateMachineType, _awaiterFields[i] );
                var configureAwaitMethod = blockReturnType.GetMethod( "ConfigureAwait", [typeof(bool)] )!;
                var getAwaiterMethod = configureAwaitMethod.ReturnType.GetMethod( "GetAwaiter" );

                var assignAwaiter = Expression.Assign(
                    Expression.Field( stateMachineInstance, lastAwaitField ),
                    Expression.Call(
                        Expression.Call( blockExpr, configureAwaitMethod, Expression.Constant( false ) ),
                        getAwaiterMethod!
                    )
                );

                // Increment state
                var setStateBeforeAwait = Expression.Assign( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i + 1 ) );

                // Check completed
                var awaiterCompletedCheck = Expression.IfThen(
                    Expression.IsFalse( Expression.Property( Expression.Field( stateMachineInstance, lastAwaitField ), "IsCompleted" ) ),
                    Expression.Block(
                        Expression.Call(
                            Expression.Field( stateMachineInstance, buildFieldInfo ),
                            nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
                            [lastAwaitField.FieldType, typeof(IAsyncStateMachine)],
                            Expression.Field( stateMachineInstance, lastAwaitField ),
                            stateMachineInstance
                        ),
                        Expression.Return( returnLabel )
                    )
                );

                var stateCheck = Expression.IfThen(
                    Expression.Equal( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i ) ),
                    Expression.Block( assignAwaiter, setStateBeforeAwait, awaiterCompletedCheck )
                );
                bodyExpressions.Add( stateCheck );
            }
            else if ( i == lastBlockIndex ) // If last block is not a Task
            {
                if ( typeof(TResult) != typeof(IVoidTaskResult) )
                {
                    var assignFinalResult = Expression.Assign( Expression.Field( stateMachineInstance, finalResultFieldInfo ), blockExpr );
                    var incrementState = Expression.Assign( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i + 1 ) );
                    bodyExpressions.Add( Expression.Block( assignFinalResult, incrementState ) );
                    handledFinalBlock = true;
                }
                else
                {
                    // IVoidTaskResult (no result)
                    var incrementState = Expression.Assign( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i + 1 ) );
                    bodyExpressions.Add( incrementState );
                }
            }
            else
            {
                throw new InvalidOperationException( $"Non-final block {i} must be a Task." );
            }
        }

        // Generate the final state
        var finalState = Expression.IfThen(
            Expression.Equal( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( lastBlockIndex + 1 ) ),
            Expression.Block(
                // Handle the final result for Task and Task<T> 
                !handledFinalBlock && typeof( TResult) != typeof(IVoidTaskResult)
                    ? Expression.Assign(
                        Expression.Field( stateMachineInstance, finalResultFieldInfo ),
                        Expression.Call(
                            Expression.Field( stateMachineInstance, lastAwaitField! ),
                            "GetResult", Type.EmptyTypes
                        )
                    )
                    : Expression.Empty(), // No-op for IVoidTaskResult

                // Set the final result on the builder
                Expression.Call(
                    Expression.Field( stateMachineInstance, buildFieldInfo ),
                    nameof(AsyncTaskMethodBuilder<TResult>.SetResult),
                    null,
                    typeof(TResult) != typeof(IVoidTaskResult)
                        ? Expression.Field( stateMachineInstance, finalResultFieldInfo )
                        : Expression.Constant( null, typeof(TResult) ) // No result for IVoidTaskResult
                )
            )
        );

        // Mark as completed after the final state logic is executed
        var markCompletedState = Expression.Assign(
            Expression.Field( stateMachineInstance, "_state" ),
            Expression.Constant( -2 ) // Mark the state machine as completed
        );

        bodyExpressions.Add( finalState );
        bodyExpressions.Add( markCompletedState );

        // Create try-catch block
        var exceptionParameter = Expression.Parameter( typeof(Exception), "ex" );
        var tryCatchBlock = Expression.TryCatch(
            Expression.Block( typeof(void), bodyExpressions ), // Try block returns void
            Expression.Catch(
                exceptionParameter,
                Expression.Block(
                    Expression.Call(
                        Expression.Field( stateMachineInstance, buildFieldInfo ),
                        nameof(AsyncTaskMethodBuilder<TResult>.SetException),
                        null,
                        exceptionParameter
                    ),
                    Expression.Return( returnLabel ) 
                )
            )
        );

        var moveNextBody = Expression.Block( tryCatchBlock, Expression.Label( returnLabel ) ); 
        return Expression.Lambda( moveNextBody, stateMachineInstance );
    }

    // Helper method to retrieve FieldInfo from the created type
    private static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
    {
        return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
    }

    private static bool TryMakeAwaiterType( Expression expr, out Type awaiterType )
    {
        awaiterType = null;

        switch ( expr )
        {
            case MethodCallExpression methodCall when typeof( Task ).IsAssignableFrom( methodCall.Type ):
                awaiterType = MakeAwaiterType( methodCall.Type );
                return true;

            case InvocationExpression invocation when typeof( Task ).IsAssignableFrom( invocation.Type ):
                awaiterType = MakeAwaiterType( invocation.Type );
                return true;

            case BlockExpression block:
                return TryMakeAwaiterType( block.Expressions.Last(), out awaiterType );

            case AwaitExpression await:
                awaiterType = MakeAwaiterType( await.Target.Type );
                return true;

            case not null when typeof( Task ).IsAssignableFrom( expr.Type ):
                awaiterType = MakeAwaiterType( expr.Type );
                return true;
        }

        return false;

        static Type MakeAwaiterType( Type taskType )
        {
            if ( !taskType.IsGenericType )
                return typeof( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter );

            var genericArgument = taskType.GetGenericArguments()[0];

            if ( genericArgument.FullName == "System.Threading.Tasks.VoidTaskResult" )
                throw new InvalidOperationException( "Task<VoidTaskResult> is not supported, are you missing a cast to Task?" );

            return typeof( ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter ).MakeGenericType( genericArgument );
        }
    }
}

public static class StateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod =
        typeof( StateMachineBuilder )
            .GetMethods( BindingFlags.Public | BindingFlags.Static )
            .First( x => x.Name == nameof( Create ) && x.IsGenericMethod );

    public static Expression Create( BlockExpression source, Type resultType, bool createRunner = true )
    {
        // If the result type is void, use the internal VoidTaskResult type
        if ( resultType == typeof(void) )
            resultType = typeof(IVoidTaskResult);

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );
        return (Expression) buildStateMachine.Invoke( null, [source, createRunner] );
    }

    public static Expression Create<TResult>( BlockExpression source, bool createRunner = true )
    {
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );
        stateMachineBuilder.SetSource( source );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( createRunner );

        return stateMachineExpression;
    }
}
