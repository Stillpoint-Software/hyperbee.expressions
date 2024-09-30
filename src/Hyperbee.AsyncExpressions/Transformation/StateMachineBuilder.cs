using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

public interface IVoidTaskResult; // Marker interface for void Task results

public class StateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;
    private FieldBuilder _builderField;
    private FieldBuilder _finalResultField;

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

    public Expression CreateStateMachine( GotoTransformerResult source, bool createRunner = true )
    {
        if ( source.Nodes == null )
            throw new InvalidOperationException( "States must be set before creating state machine." );

        // Create the state-machine
        //
        // Conceptually:
        //
        // var stateMachine = new StateMachine();
        // var moveNextLambda = (StateMachineBase stateMachine) => { ... };
        //
        // stateMachine.SetMoveNext( moveNextLambda );
        
        var stateMachineBaseType = CreateStateMachineBaseType( source, out var fields );
        var stateMachineType = CreateStateMachineDerivedType( stateMachineBaseType );
        var moveNextLambda = CreateMoveNextBody( source, stateMachineBaseType, fields );

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
        // return stateMachine.__builder<>.Task;

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

    private Type CreateStateMachineBaseType( GotoTransformerResult results, out IEnumerable<FieldInfo> fields )
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
        _builderField = typeBuilder.DefineField( FieldName.Builder,
            typeof(AsyncTaskMethodBuilder<>).MakeGenericType( typeof(TResult) ), FieldAttributes.Public );
        _finalResultField = typeBuilder.DefineField( FieldName.FinalResult, typeof(TResult), FieldAttributes.Public );

        var fieldNames = ImplementFields( typeBuilder, results );
        ImplementConstructor( typeBuilder, typeof(object), state );
        ImplementSetStateMachine( typeBuilder );

        typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes
        );

        var stateMachineBaseType = typeBuilder.CreateType();

        // Build the runtime field info for each variable
        fields = fieldNames.Select( name =>
            stateMachineBaseType.GetField( name, BindingFlags.Instance | BindingFlags.Public )!
        ).ToArray();

        return stateMachineBaseType;

    }

    private Type CreateStateMachineDerivedType( Type stateMachineBaseType )
    {
        // Define the state machine derived type
        //
        // public class StateMachineType: StateMachineBaseType
        // {
        //      private Action<StateMachineBaseType> __moveNextLambda<>;
        //
        //      public StateMachineType(): base() {}
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

    private static void ImplementConstructor( TypeBuilder typeBuilder, Type baseType, FieldInfo stateFieldInfo = null )
    {
        // Define the constructor 
        //
        // public StateMachineType(): base()
        // {
        //     __state<> = -1;
        // }

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

    private static string[] ImplementFields( TypeBuilder typeBuilder, GotoTransformerResult result )
    {
        // Define: variable fields
        return result.Variables
            .Select( x => typeBuilder.DefineField( $"{x.Name}", x.Type, FieldAttributes.Public ) )
            .Select( x => x.Name )
            .ToArray();
    }

    private static void ImplementSetMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextExpressionField )
    {
        // Define the SetMoveNext method
        //
        //  public void SetMoveNext<StateMachineBase>(Action<StateMachineBase> moveNext)
        //  {
        //     __moveNextLambda<> = moveNext;
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
        ilGenerator.Emit( OpCodes.Stfld, moveNextExpressionField ); // this._moveNextLambda<> = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret ); 
    }

    private void ImplementSetStateMachine( TypeBuilder typeBuilder )
    {
        // Define the IAsyncStateMachine.SetStateMachine method
        //
        // public void SetStateMachine( IAsyncStateMachine stateMachine )
        // {
        //    __builder<>.SetStateMachine( stateMachine );
        // }

        var setStateMachineMethod = typeBuilder.DefineMethod(
            "SetStateMachine",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IAsyncStateMachine)]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, _builderField ); // __builder<>
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // argument: stateMachine

        var setStateMachineOnBuilder = typeof(AsyncTaskMethodBuilder<>)
            .MakeGenericType( typeof(TResult) )
            .GetMethod( "SetStateMachine", [typeof(IAsyncStateMachine)] );

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( setStateMachineMethod, 
            typeof(IAsyncStateMachine).GetMethod( "SetStateMachine" )! );
    }

    private static void ImplementMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextExpressionField )
    {
        // Define the MoveNext method
        //
        // public override void MoveNext()
        // {
        //    __moveNextLambda<>((StateMachineBaseType) this);
        // }

        var moveNextMethod = typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, moveNextExpressionField ); // __moveNextExpression<>
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // argument: this

        var invokeMethod = typeof(Action<>)
            .MakeGenericType( typeBuilder.BaseType! ) 
            .GetMethod( "Invoke" );

        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! );
        ilGenerator.Emit( OpCodes.Ret );
    }

    private LambdaExpression CreateMoveNextBody( GotoTransformerResult result, Type stateMachineBaseType, IEnumerable<FieldInfo> fields )
    {
        // Example of a typical state-machine:
        //
        // try
        // {
        //     int returnValue;
        //
        //     switch ( stateMachine.__state<> )
        //     {
        //         case 0:
        //             stateMachine.__state<> = -1;
        //             goto ST_0002;
        //             break;
        //
        //         default:
        //             break;
        //     }
        //
        //     ST_0000:
        //     stateMachine.var1 = 1;
        //     stateMachine.__awaiter<0> = Task<int>.GetAwaiter();
        //
        //     if ( !stateMachine.__awaiter<0>.IsCompleted )
        //     {
        //         stateMachine.__state<> = 0;
        //         stateMachine.__builder<>.AwaitUnsafeOnCompleted( ref stateMachine.awaiter<0>, ref stateMachine );
        //         return;
        //     }
        //
        //     goto ST_0002;
        //
        //     ST_0001:
        //     stateMachine.var2 = stateMachine.<>s__2;
        //     goto ST_0004;
        //
        //     ST_0002:
        //     stateMachine.<>s__2 = stateMachine.__awaiter<0>.GetResult();
        //     goto ST_0001;
        //
        //     ST_0003:
        //     stateMachine.__finalResult<> = returnValue;
        //     stateMachine.__state<> = -2;
        //     stateMachine.__builder<>.SetResult( stateMachine.__finalResult<> );
        //     goto ST_FINAL;
        //
        //     ST_0004:
        //     returnValue = stateMachine.var2 + param1;
        //     goto ST_0003;
        //
        // }
        // catch ( Exception ex )
        // {
        //     stateMachine.__state<> = -2;
        //     stateMachine.__builder<>.SetException( ex );
        //     return;
        // }
        //
        // ST_FINAL:

        var returnLabel = Expression.Label( "ST_FINAL" );
        var stateMachineInstance = Expression.Parameter( stateMachineBaseType, "stateMachine" );

        var buildFieldInfo = GetFieldInfo( stateMachineBaseType, _builderField );
        var finalResultFieldInfo = GetFieldInfo( stateMachineBaseType, _finalResultField );

        var bodyExpressions = new List<Expression>();

        var stateIdFieldExpression = Expression.Field( stateMachineInstance, FieldName.State );
        var stateMachineBuilderFieldExpression = Expression.Field( stateMachineInstance, buildFieldInfo );
        
        var fieldMembers = fields.Select( x => Expression.Field( stateMachineInstance, x ) ).ToArray();

        var fieldResolverVisitor = new FieldResolverVisitor( 
            stateMachineInstance,
            fieldMembers,
            returnLabel,
            stateIdFieldExpression,
            stateMachineBuilderFieldExpression );

        // Create the jump table

        var jumpTableExpression = Expression.Switch(
            stateIdFieldExpression, 
            Expression.Empty(),
            result.JumpCases.Select( c =>
                Expression.SwitchCase(
                    Expression.Block(
                        Expression.Assign( stateIdFieldExpression, Expression.Constant( -1 ) ),
                        Expression.Goto( c.Key )
                    ),
                    Expression.Constant( c.Value ) 
                ) 
            )
            .ToArray() );

        bodyExpressions.Add( jumpTableExpression );

        // Iterate through the blocks (each block corresponds to a state)

        foreach ( var (blockExpressions, blockTransition) in result.Nodes )
        {
            var resolvedExpressions = fieldResolverVisitor.Visit( blockExpressions );

            var finalBlock = blockTransition == null;
            if ( finalBlock )
            {
                // TODO: fix final block (add lazy expression?)
                bodyExpressions.Add( resolvedExpressions[0] );

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
                        Expression.Block( resolvedExpressions[1..] )
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
            else
            {
                bodyExpressions.AddRange( resolvedExpressions );
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

        // Combine the try-catch block with the return label and
        // return the lambda expression for MoveNext

        var moveNextBody = Expression.Block( tryCatchBlock, Expression.Label( returnLabel ) );
        return Expression.Lambda( moveNextBody, stateMachineInstance );

        // Helper methods
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

    public static Expression Create( Type resultType, GotoTransformerResult source, bool createRunner = true )
    {
        // If the result type is void, use the internal VoidTaskResult type
        if ( resultType == typeof(void) )
            resultType = typeof(IVoidTaskResult);

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );
        return (Expression) buildStateMachine.Invoke( null, [source, createRunner] );
    }

    public static Expression Create<TResult>( GotoTransformerResult source, bool createRunner = true )
    {
        // Create the state machine
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( source, createRunner );

        return stateMachineExpression;
    }
}
