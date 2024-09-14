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
    private FieldBuilder _builderField;
    private FieldBuilder _finalResultField;
    private List<FieldBuilder> _variableFields;
    private List<FieldBuilder> _awaiterFields;

    private static class FieldName
    {
        // use special names to prevent collisions with user fields
        public const string Builder = "__builder<>";
        public const string FinalResult = "__finalResult<>";
        public const string MoveNextLambda = "__moveNextLambda<>";
        public const string State = "__state<>";
        
        private const string AwaiterTemplate = "__awaiter<{0}>";

        public static string Awaiter( int i ) => string.Format( AwaiterTemplate, i );
    }

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public void SetExpressionSource( BlockExpression blockSource )
    {
        _blockSource = blockSource;
    }

    public Expression CreateStateMachine( bool createRunner = true )
    {
        if ( _blockSource == null )
            throw new InvalidOperationException( "Source must be set before creating state machine." );

        // Create the state-machine

        var stateMachineBaseType = CreateStateMachineBaseType( _blockSource );
        var stateMachineType = CreateStateMachineDerivedType( stateMachineBaseType );
        var moveNextLambda = CreateMoveNextExpression( _blockSource, stateMachineBaseType );

        var stateMachineVariable = Expression.Variable( stateMachineType, "stateMachine" );
        var setMoveNextMethod = stateMachineType.GetMethod( "SetMoveNextExpression" )!;

        var stateMachineExpression = Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, Expression.New( stateMachineType ) ),
            Expression.Call( stateMachineVariable, setMoveNextMethod, moveNextLambda ),
            stateMachineVariable
        );

        if ( !createRunner )
            return stateMachineExpression;

        // Wrap the state-machine in a method that executes it
        //
        // Conceptually:
        //
        // public Task<TResult> Run( StateMachineType stateMachine )
        // {
        //     stateMachine._builder.Start<StateMachineType>( ref stateMachine );
        //     return stateMachine._builder.Task;
        // }

        var builderFieldInfo = stateMachineType.GetField( FieldName.Builder )!;
        var taskFieldInfo = builderFieldInfo.FieldType.GetProperty( "Task" )!;

        var builderField = Expression.Field( stateMachineVariable, builderFieldInfo );

        var startMethod = typeof(AsyncTaskMethodBuilder<>)
            .MakeGenericType( typeof(TResult) )
            .GetMethod( "Start" )!
            .MakeGenericMethod( stateMachineType );

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

    private Type CreateStateMachineBaseType( BlockExpression block )
    {
        // Define the state machine base type
        //
        // public class StateMachineBaseType : IAsyncStateMachine
        // {
        //      public int __state<>;
        //      public AsyncTaskMethodBuilder<TResult> __builder<>;
        //      public TResult __finalResult<>;
        //      
        //      // Hoisted (example)
        //      public int _variable1;
        //      public int _variable2;
        //
        //      // Awaiters (example)
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter<1>;
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter __awaiter<2>;
        //
        //      public StateMachineBaseType() {}
        //
        //      public abstract void MoveNext();
        //      public void SetStateMachine(IAsyncStateMachine stateMachine) => __builder<>.SetStateMachine( stateMachine );
        // }

        var typeBuilder = _moduleBuilder.DefineType(
            $"{_typeName}Base",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class,
            typeof(object),
            [typeof(IAsyncStateMachine)]
        );

        typeBuilder.DefineField( FieldName.State, typeof(int), FieldAttributes.Public );
        _builderField = typeBuilder.DefineField( FieldName.Builder, typeof(AsyncTaskMethodBuilder<>).MakeGenericType( typeof(TResult) ), FieldAttributes.Public );
        _finalResultField = typeBuilder.DefineField( FieldName.FinalResult, typeof(TResult), FieldAttributes.Public );

        ImplementFields( typeBuilder, block );
        ImplementConstructor( typeBuilder, typeof(object) );
        ImplementSetStateMachine( typeBuilder );

        typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes
        );

        return typeBuilder.CreateType();
    }

    private Type CreateStateMachineDerivedType( Type stateMachineBaseType )
    {
        // Define the state machine derived type
        //
        // public class StateMachineType : StateMachineBaseType
        // {
        //      private Action<StateMachineBaseType> __moveNextLambda<>;
        //
        //      public StateMachineType() {}
        //      public void SetMoveNext(Action<StateMachineBaseType> moveNext) => __moveNextLambda<> = moveNext;
        //      public override void MoveNext() => __moveNextLambda<>((StateMachineBaseType) this);
        // }

        var typeBuilder = _moduleBuilder.DefineType(
            _typeName,
            TypeAttributes.Public,
            stateMachineBaseType
        );

        var moveNextExpressionField = typeBuilder.DefineField(
            FieldName.MoveNextLambda,
            typeof(Action<>).MakeGenericType( stateMachineBaseType ),
            FieldAttributes.Private
        );

        ImplementConstructor( typeBuilder, stateMachineBaseType );

        ImplementSetMoveNext( typeBuilder, moveNextExpressionField );
        ImplementMoveNext( typeBuilder, moveNextExpressionField );

        return typeBuilder.CreateType();
    }

    private void ImplementConstructor( TypeBuilder typeBuilder, Type baseType )
    {
        // Define a parameterless constructor
        var constructor = typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes );
        var ilGenerator = constructor.GetILGenerator();

        // Call the base constructor 
        ilGenerator.Emit( OpCodes.Ldarg_0 ); 
        ilGenerator.Emit( OpCodes.Call, baseType.GetConstructor( Type.EmptyTypes )! ); // base()
        ilGenerator.Emit( OpCodes.Ret );
    }

    private void ImplementFields( TypeBuilder typeBuilder, BlockExpression block )
    {
        // Define: variable fields
        _variableFields = [];
        foreach ( var variable in block.Variables )
        {
            var field = typeBuilder.DefineField( $"_{variable.Name}", variable.Type, FieldAttributes.Public );
            _variableFields.Add( field );
        }

        // Define: awaiter fields
        _awaiterFields = [];
        for ( var i = 0; i < block.Expressions.Count; i++ )
        {
            var expr = block.Expressions[i];

            if ( !TryMakeAwaiterType( expr, out Type awaiterType ) )
                continue; // Not an awaitable expression

            // `i` should match the index of the expression to align with state machine logic
            var awaiterField = typeBuilder.DefineField( FieldName.Awaiter( i ), awaiterType, FieldAttributes.Public );

            _awaiterFields.Add( awaiterField );
        }
    }

    private void ImplementSetMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextExpressionField )
    {
        // Define the SetMoveNext method
        //
        //  public void SetMoveNext<StateMachineBase>(Action<StateMachineBase> moveNext)
        //  {
        //     _moveNextLambda = moveNext;
        //  }

        var setMoveNextMethod = typeBuilder.DefineMethod(
            "SetMoveNextExpression",
            MethodAttributes.Public,
            typeof(void),
            [typeof(Action<>).MakeGenericType( typeBuilder.BaseType! )] //[typeof(Action<StateMachineBase<TResult>>)]
        );

        var ilGenerator = setMoveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, moveNextExpressionField ); // this._moveNextLambda = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret ); // return
    }

    private void ImplementSetStateMachine( TypeBuilder typeBuilder )
    {
        // Define the IAsyncStateMachine.SetStateMachine method
        //
        // public void SetStateMachine( IAsyncStateMachine stateMachine )
        // {
        //    _builder.SetStateMachine( stateMachine );
        // }

        var setStateMachineMethod = typeBuilder.DefineMethod(
            "SetStateMachine",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IAsyncStateMachine)]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this` (stateMachine)
        ilGenerator.Emit( OpCodes.Ldfld, _builderField ); // load `_builder`
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // Load the `stateMachine` (IAsyncStateMachine) from the argument

        var setStateMachineOnBuilder = typeof(AsyncTaskMethodBuilder<>)
            .MakeGenericType( typeof(TResult) )
            .GetMethod( "SetStateMachine", [typeof(IAsyncStateMachine)] );

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( setStateMachineMethod, 
            typeof(IAsyncStateMachine).GetMethod( "SetStateMachine" )! );
    }

    private void ImplementMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextExpressionField )
    {
        var moveNextMethod = typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this`
        ilGenerator.Emit( OpCodes.Ldfld, moveNextExpressionField ); // load `_moveNextExpression`
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load 'this' as the argument for `_moveNextExpression.Invoke`

        var invokeMethod = typeof(Action<>)
            .MakeGenericType( typeBuilder.BaseType! ) 
            .GetMethod( "Invoke" );

        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! );
        ilGenerator.Emit( OpCodes.Ret );
    }

    private LambdaExpression CreateMoveNextExpression( BlockExpression block, Type stateMachineBaseType )
    {
        // Example of a typical state-machine:
        //
        // public void MoveNext()
        // {
        //     try
        //     {
        //         if (__state<> == 0)
        //         {
        //             __awaiter<1> = task1.ConfigureAwait(false).GetAwaiter();
        //             __state<> = 1;
        //
        //             if (!__awaiter<1>.IsCompleted == false)
        //             {
        //                 __builder<>.AwaitUnsafeOnCompleted(ref __awaiter<1>, this);
        //                 return;
        //             }
        //         }
        //
        //         if (__state<> == 1)
        //         {
        //             __awaiter<1>.GetResult();
        //             __awaiter<2> = task2.ConfigureAwait(false).GetAwaiter();
        //             __state<> = 2;
        //
        //             if (!__awaiter<2>.IsCompleted)
        //             {
        //                 __builder<>.AwaitUnsafeOnCompleted(ref __awaiter<2>, this);
        //                 return;
        //             }
        //         }
        //
        //         if (__state<> == 2)
        //         {
        //             __builder<>.Task.SetResult( __awaiter<2>.GetResult() );
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         __builder<>.SetException(ex);
        //     }
        // }

        var stateMachineInstance = Expression.Parameter( stateMachineBaseType, "stateMachine" );

        var parameterVisitor = new ParameterMappingVisitor( stateMachineInstance, _variableFields );

        var buildFieldInfo = GetFieldInfo( stateMachineBaseType, _builderField );
        var finalResultFieldInfo = GetFieldInfo( stateMachineBaseType, _finalResultField );

        var bodyExpressions = new List<Expression>();

        var blocks = block.Expressions;
        int lastBlockIndex = blocks.Count - 1;

        var returnLabel = Expression.Label( "ExitMoveNext" );

        FieldInfo lastAwaitField = null; // To track the last awaiter field
        var handledFinalBlock = false;

        // Iterate through the blocks (each block corresponds to a state)
        for ( var i = 0; i <= lastBlockIndex; i++ )
        {
            // Initialize awaiters
            if ( blocks[i] is BlockExpression blockExpression && blockExpression.Expressions.First() is AwaitResultExpression resultExpression )
            {
                resultExpression.InitializeAwaiter( stateMachineInstance, lastAwaitField );
            }

            // Visit and map parameters to fields for the current block
            var blockExpr = parameterVisitor.Visit( blocks[i] );
            var blockReturnType = blockExpr.Type;

            if ( AsyncBaseExpression.IsTask( blockReturnType ) )
            {
                // Handle task-based state
                lastAwaitField = GetFieldInfo( stateMachineBaseType, _awaiterFields[i] );
                var configureAwaitMethod = blockReturnType.GetMethod( "ConfigureAwait", [typeof(bool)] );
                var getAwaiterMethod = configureAwaitMethod!.ReturnType.GetMethod( "GetAwaiter" );

                // Assign the awaiter to the field
                var assignAwaiter = Expression.Assign(
                    Expression.Field( stateMachineInstance, lastAwaitField ),
                    Expression.Call(
                        Expression.Call( blockExpr, configureAwaitMethod, Expression.Constant( false ) ),
                        getAwaiterMethod!
                    )
                );

                // Increment state
                var setStateBeforeAwait = Expression.Assign( Expression.Field( stateMachineInstance, FieldName.State ), Expression.Constant( i + 1 ) );

                // Check if awaiter is completed
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

                // Check the current state and execute the block
                var stateCheck = Expression.IfThen(
                    Expression.Equal( Expression.Field( stateMachineInstance, FieldName.State ), Expression.Constant( i ) ),
                    Expression.Block( assignAwaiter, setStateBeforeAwait, awaiterCompletedCheck )
                );
                bodyExpressions.Add( stateCheck );
            }
            else if ( i == lastBlockIndex ) // If the last block is not a task
            {
                // Handle final result for Task<T>
                if ( typeof(TResult) != typeof(IVoidTaskResult) )
                {
                    var assignFinalResult = Expression.Assign( Expression.Field( stateMachineInstance, finalResultFieldInfo ), blockExpr );
                    var incrementState = Expression.Assign( Expression.Field( stateMachineInstance, FieldName.State ), Expression.Constant( i + 1 ) );
                    bodyExpressions.Add( Expression.Block( assignFinalResult, incrementState ) );
                    handledFinalBlock = true;
                }
                else
                {
                    // For void result task
                    var incrementState = Expression.Assign( Expression.Field( stateMachineInstance, FieldName.State ), Expression.Constant( i + 1 ) );
                    bodyExpressions.Add( incrementState );
                }
            }
            else
            {
                throw new InvalidOperationException( $"Non-final block {i} must be a Task." );
            }
        }

        // Generate the final state logic
        var finalState = Expression.IfThen(
            Expression.Equal( Expression.Field( stateMachineInstance, FieldName.State ), Expression.Constant( lastBlockIndex + 1 ) ),
            Expression.Block(
                // Handle the final result for Task<T>
                !handledFinalBlock && typeof(TResult) != typeof(IVoidTaskResult)
                    ? Expression.Assign(
                        Expression.Field( stateMachineInstance, finalResultFieldInfo ),
                        Expression.Call(
                            Expression.Field( stateMachineInstance, lastAwaitField! ),
                            "GetResult",
                            Type.EmptyTypes
                        )
                    )
                    : Expression.Empty(), // No result for IVoidTaskResult

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

        // Mark the state machine as completed after executing the final state
        var markCompletedState = Expression.Assign(
            Expression.Field( stateMachineInstance, FieldName.State ),
            Expression.Constant( -2 ) // Mark the state machine as completed
        );

        // Add the final state and the completion marker to the body
        bodyExpressions.Add( finalState );
        bodyExpressions.Add( markCompletedState );

        // Create a try-catch block to handle exceptions
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
                    Expression.Return( returnLabel ) // Return after setting the exception
                )
            )
        );

        // Combine the try-catch block with the return label
        var moveNextBody = Expression.Block( tryCatchBlock, Expression.Label( returnLabel ) );

        // Return the lambda expression for MoveNext
        return Expression.Lambda( moveNextBody, stateMachineInstance );

        static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
        {
            return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
        }
    }

    private static bool TryMakeAwaiterType( Expression expr, out Type awaiterType )
    {
        while ( true )
        {
            awaiterType = null;

            switch ( expr )
            {
                case MethodCallExpression methodCall when typeof(Task).IsAssignableFrom( methodCall.Type ):
                    awaiterType = MakeAwaiterType( methodCall.Type );
                    return true;

                case InvocationExpression invocation when typeof(Task).IsAssignableFrom( invocation.Type ):
                    awaiterType = MakeAwaiterType( invocation.Type );
                    return true;

                case BlockExpression block:
                    expr = block.Expressions.Last();
                    continue; // loop

                case AwaitExpression await:
                    awaiterType = MakeAwaiterType( await.Target.Type );
                    return true;

                case not null when typeof(Task).IsAssignableFrom( expr.Type ):
                    awaiterType = MakeAwaiterType( expr.Type );
                    return true;
            }

            return false;
        }

        static Type MakeAwaiterType( Type taskType )
        {
            if ( !taskType.IsGenericType ) 
                return typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter);

            var genericArgument = taskType.GetGenericArguments()[0];

            if ( genericArgument.FullName == "System.Threading.Tasks.VoidTaskResult" )
                throw new InvalidOperationException( "Task<VoidTaskResult> is not supported, are you missing a cast to Task?" );

            return typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter).MakeGenericType( genericArgument );
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
        stateMachineBuilder.SetExpressionSource( source );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( createRunner );

        return stateMachineExpression;
    }
}
