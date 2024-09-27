#define STATEMACHINE_LOGGER
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
    private GotoTransformResult _result;

    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;
    private FieldBuilder _builderField;
    private FieldBuilder _finalResultField;
    private List<FieldBuilder> _variableFields;

    private static class FieldName
    {
        // use special names to prevent collisions with user fields
        public const string Builder = "__builder<>";
        public const string FinalResult = "__finalResult<>";
        public const string MoveNextLambda = "__moveNextLambda<>";
        public const string State = "__state<>";
    }

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public void SetExpressionSource( GotoTransformResult result )
    {
        _result = result;
    }

    public Expression CreateStateMachine( bool createRunner = true )
    {
        if ( _result.Nodes == null )
            throw new InvalidOperationException( "States must be set before creating state machine." );

        // Create the state-machine
        //
        // Conceptually:
        //
        // var stateMachine = new StateMachine();
        // var moveNextLambda = (StateMachineBase stateMachine) => { ... };
        //
        // stateMachine.SetMoveNext( moveNextLambda );
        
        var stateMachineBaseType = CreateStateMachineBaseType( _result );
        var stateMachineType = CreateStateMachineDerivedType( stateMachineBaseType );
        var moveNextLambda = CreateMoveNextBody( _result, stateMachineBaseType );

        var stateMachineVariable = Expression.Variable( stateMachineType, "stateMachine" );
        var setMoveNextMethod = stateMachineType.GetMethod( "SetMoveNext" )!;

        var stateMachineExpression = Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, Expression.New( stateMachineType ) ),
            Expression.Call( stateMachineVariable, setMoveNextMethod, moveNextLambda ),
            stateMachineVariable
        );

        if ( !createRunner )
            return stateMachineExpression;

        // Run the state-machine
        //
        // Conceptually:
        //
        // stateMachine._builder.Start<StateMachineType>( ref stateMachine );
        // return stateMachine._builder.Task;

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

    private Type CreateStateMachineBaseType( GotoTransformResult results )
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
        //      public StateMachineBaseType()
        //      {
        //          __state<> = -1; 
        //      }
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

        var state = typeBuilder.DefineField( FieldName.State, typeof(int), FieldAttributes.Public );
        _builderField = typeBuilder.DefineField( FieldName.Builder, typeof(AsyncTaskMethodBuilder<>).MakeGenericType( typeof(TResult) ), FieldAttributes.Public );
        _finalResultField = typeBuilder.DefineField( FieldName.FinalResult, typeof(TResult), FieldAttributes.Public );

        ImplementFields( typeBuilder, results );
        ImplementConstructor( typeBuilder, typeof(object), state );
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

    private void ImplementConstructor( TypeBuilder typeBuilder, Type baseType, FieldInfo stateFieldInfo = null )
    {
        // Define a parameterless constructor
        var constructor = typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes );
        var ilGenerator = constructor.GetILGenerator();

        // Initialize state field to -1
        if ( stateFieldInfo != null )
        {
            ilGenerator.Emit( OpCodes.Ldarg_0 );
            ilGenerator.Emit( OpCodes.Ldc_I4_M1 ); // load -1
            ilGenerator.Emit( OpCodes.Stfld, stateFieldInfo ); 
        }

        // Call the base constructor 
        ilGenerator.Emit( OpCodes.Ldarg_0 );
        ilGenerator.Emit( OpCodes.Call, baseType.GetConstructor( Type.EmptyTypes )! ); 
        ilGenerator.Emit( OpCodes.Ret );
    }

    private void ImplementFields( TypeBuilder typeBuilder, GotoTransformResult result )
    {
        // Define: variable fields
        _variableFields = result.Nodes
            .SelectMany( x => x.Variables )
            .Distinct()
            .Select( x => typeBuilder.DefineField( $"{x.Name}", x.Type, FieldAttributes.Public ) )
            .ToList();
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
            "SetMoveNext",
            MethodAttributes.Public,
            typeof(void),
            [typeof(Action<>).MakeGenericType( typeBuilder.BaseType! )] //[typeof(Action<StateMachineBase<TResult>>)]
        );

        var ilGenerator = setMoveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, moveNextExpressionField ); // this._moveNextLambda = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret ); 
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

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, _builderField ); // _builder<>
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // argument: stateMachine

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

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, moveNextExpressionField ); // _moveNextExpression
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // argument: this

        var invokeMethod = typeof(Action<>)
            .MakeGenericType( typeBuilder.BaseType! ) 
            .GetMethod( "Invoke" );

        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! );
        ilGenerator.Emit( OpCodes.Ret );
    }

    private LambdaExpression CreateMoveNextBody( GotoTransformResult result, Type stateMachineBaseType )
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

        var returnLabel = Expression.Label( "ExitMoveNext" );
        var stateMachineInstance = Expression.Parameter( stateMachineBaseType, "stateMachine" );

        var buildFieldInfo = GetFieldInfo( stateMachineBaseType, _builderField );
        var finalResultFieldInfo = GetFieldInfo( stateMachineBaseType, _finalResultField );

        var bodyExpressions = new List<Expression>();

        var stateIdFieldExpression = Expression.Field( stateMachineInstance, FieldName.State );
        var stateMachineBuilderFieldExpression = Expression.Field( stateMachineInstance, buildFieldInfo );
        
        var parameterVisitor = new ParameterMappingVisitor( 
            stateMachineInstance, 
            _variableFields, 
            returnLabel,
            stateIdFieldExpression,
            stateMachineBuilderFieldExpression );

        // Create the jump table
        result.JumpTable.State = Expression.Field( stateMachineInstance, FieldName.State );
        bodyExpressions.Add( result.JumpTable.Reduce() );

        // Iterate through the blocks (each block corresponds to a state)
        foreach ( var (blockVariables, blockExpressions, blockTransition) in result.Nodes )
        {
            // TODO: Creating block just for visiting?
            var block = Expression.Block( blockVariables, blockExpressions );

            // Visit and map parameters to fields for the current block
            var expr = parameterVisitor.Visit( block );

            var finalBlock = blockTransition == null;
            if ( finalBlock && expr is BlockExpression finalBlockExpression )
            {
                // TODO: fix final block (add lazy expression?)
                bodyExpressions.Add( finalBlockExpression.Expressions.First() );

                if ( result.ReturnValue != null )
                {
                    bodyExpressions.Add( Expression.Assign(
                        Expression.Field( stateMachineInstance, finalResultFieldInfo ),
                        result.ReturnValue
                    ) );
                }
                else
                {
                    bodyExpressions.Add( Expression.Assign(
                        Expression.Field( stateMachineInstance, finalResultFieldInfo ),
                        Expression.Block( finalBlockExpression.Expressions.Skip( 1 ).ToArray() )
                    ) );
                }

                bodyExpressions.Add( Expression.Assign( stateIdFieldExpression, Expression.Constant( -2 ) ) );

                // Set the final result on the builder
                bodyExpressions.Add( Expression.Call(
                    Expression.Field( stateMachineInstance, buildFieldInfo ),
                    nameof(AsyncTaskMethodBuilder<TResult>.SetResult),
                    null,
                    typeof(TResult) != typeof(IVoidTaskResult)
                        ? Expression.Field( stateMachineInstance, finalResultFieldInfo )
                        : Expression.Constant( null, typeof(TResult) ) // No result for IVoidTaskResult
                ) );

                bodyExpressions.Add( Expression.Goto( returnLabel ) );
            }
            else if ( expr is BlockExpression blockExpression )
            {
                bodyExpressions.AddRange( blockExpression.Expressions );
            }
            else
            {
                throw new InvalidOperationException( "Unexpected expression type." );
            }
        }

        ParameterExpression[] variables = (result.ReturnValue != null)
            ? [result.ReturnValue]
            : [];

        // Create a try-catch block to handle exceptions
        var exceptionParameter = Expression.Parameter( typeof(Exception), "ex" );
        var tryCatchBlock = Expression.TryCatch(
            Expression.Block( typeof( void ), variables, bodyExpressions ), // Try block returns void
            Expression.Catch(
                exceptionParameter,
                Expression.Block(
                    Expression.Assign( stateIdFieldExpression, Expression.Constant( -2 ) ),
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
}

public static class StateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod =
        typeof( StateMachineBuilder )
            .GetMethods( BindingFlags.Public | BindingFlags.Static )
            .First( x => x.Name == nameof( Create ) && x.IsGenericMethod );

    public static Expression Create( GotoTransformResult result, Type resultType, bool createRunner = true )
    {
        // If the result type is void, use the internal VoidTaskResult type
        if ( resultType == typeof(void) )
            resultType = typeof(IVoidTaskResult);

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );
        return (Expression) buildStateMachine.Invoke( null, [result, createRunner] );
    }

    public static Expression Create<TResult>( GotoTransformResult result, bool createRunner = true )
    {
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );
        stateMachineBuilder.SetExpressionSource( result );

        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( createRunner );

        return stateMachineExpression;
    }
}
