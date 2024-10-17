using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

public interface IVoidResult; // Marker interface for void Task results

public class StateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;

    private static class FieldName
    {
        // use special names to prevent collisions with user fields
        public const string Builder = "__builder<>";
        public const string FinalResult = "__finalResult<>";
        public const string MoveNextLambda = "__moveNextLambda<>";
        public const string State = "__state<>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool IsSystemField( string name ) => name.EndsWith( "<>" ); // System fields end in <>
    }

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public Expression CreateStateMachine( LoweringResult source, int id, bool createRunner = true )
    {
        if ( source.Scopes[0].Nodes == null )
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
        var moveNextLambda = CreateMoveNextBody( source, stateMachineBaseType, id, fields );

        var stateMachineVariable = Expression.Variable( stateMachineType, $"stateMachine<{id}>" );

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

        var startMethod = typeof( AsyncTaskMethodBuilder<> )
            .MakeGenericType( typeof( TResult ) )
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

    private Type CreateStateMachineBaseType( LoweringResult source, out IEnumerable<FieldInfo> fields )
    {
        // Define the state machine base type
        //
        // public class StateMachineBaseType : IAsyncStateMachine
        // {
        //      // System fields
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
            typeof( object ),
            [typeof( IAsyncStateMachine )]
        );

        ImplementSystemFields( typeBuilder, out var stateField, out var builderField );
        ImplementVariableFields( typeBuilder, source );
        ImplementConstructor( typeBuilder, typeof( object ), stateField );
        ImplementSetStateMachine( typeBuilder, builderField );

        typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            typeof( void ),
            Type.EmptyTypes
        );

        var stateMachineBaseType = typeBuilder.CreateType();

        // Build the runtime field info for each variable

        fields = stateMachineBaseType.GetFields( BindingFlags.Instance | BindingFlags.Public )
            .Where( field => !FieldName.IsSystemField( field.Name ) )
            .ToArray();

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
            typeof( Action<> ).MakeGenericType( stateMachineBaseType ),
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

    private static void ImplementSystemFields( TypeBuilder typeBuilder, out FieldBuilder stateField, out FieldBuilder builderField )
    {
        // Define: system fields
        stateField = typeBuilder.DefineField(
            FieldName.State,
            typeof( int ),
            FieldAttributes.Public
        );

        builderField = typeBuilder.DefineField(
            FieldName.Builder,
            typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ),
            FieldAttributes.Public
        );

        typeBuilder.DefineField(
            FieldName.FinalResult,
            typeof( TResult ),
            FieldAttributes.Public
        );
    }

    private static void ImplementVariableFields( TypeBuilder typeBuilder, LoweringResult result )
    {
        // Define: variable fields
        foreach ( var parameterExpression in result.Variables )
        {
            typeBuilder.DefineField(
                parameterExpression.Name ?? parameterExpression.ToString(),
                parameterExpression.Type,
                FieldAttributes.Public
            );
        }
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
            typeof( void ),
            [typeof( Action<> ).MakeGenericType( typeBuilder.BaseType! )] //[typeof(Action<StateMachineBase<TResult>>)]
        );

        var ilGenerator = setMoveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, moveNextExpressionField ); // this._moveNextLambda<> = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret );
    }

    private static void ImplementSetStateMachine( TypeBuilder typeBuilder, FieldBuilder builderField )
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
            typeof( void ),
            [typeof( IAsyncStateMachine )]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, builderField );
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // argument: stateMachine

        var setStateMachineOnBuilder = typeof( AsyncTaskMethodBuilder<> )
            .MakeGenericType( typeof( TResult ) )
            .GetMethod( "SetStateMachine", [typeof( IAsyncStateMachine )] );

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( setStateMachineMethod,
            typeof( IAsyncStateMachine ).GetMethod( "SetStateMachine" )! );
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
            typeof( void ),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        var invokeMethod = typeof( Action<> )
            .MakeGenericType( typeBuilder.BaseType! )
            .GetMethod( "Invoke" );

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldfld, moveNextExpressionField );
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // argument: this
        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! );
        ilGenerator.Emit( OpCodes.Ret );
    }

    private static LambdaExpression CreateMoveNextBody( LoweringResult source, Type stateMachineBaseType, int id, IEnumerable<FieldInfo> fields )
    {
        // Example of a typical state-machine:
        //
        // try
        // {
        //     int return<>;
        //
        //     switch ( stateMachine.__state<> )
        //     {
        //         case 0:
        //             stateMachine.__state<> = -1;
        //             goto ST_0002;
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
        //     ST_0004:
        //     return<> = stateMachine.var2 + param1;
        //
        //     ST_0003:
        //     stateMachine.__finalResult<> = return<>;
        //     stateMachine.__state<> = -2;
        //     stateMachine.__builder<>.SetResult( stateMachine.__finalResult<> );
        // }
        // catch ( Exception ex )
        // {
        //     stateMachine.__state<> = -2;
        //     stateMachine.__builder<>.SetException( ex );
        //     return;
        // }

        var stateMachineInstance = Expression.Parameter( stateMachineBaseType, $"sm<{id}>" );
        var exitLabel = Expression.Label( "ST_EXIT" );

        var bodyExpressions = new List<Expression>( 16 ); // preallocate slots for expressions

        var stateFieldExpression = Expression.Field( stateMachineInstance, FieldName.State );
        var builderFieldExpression = Expression.Field( stateMachineInstance, FieldName.Builder );
        var finalResultFieldExpression = Expression.Field( stateMachineInstance, FieldName.FinalResult );

        var fieldMembers = fields.Select( x => Expression.Field( stateMachineInstance, x ) ).ToArray();

        // Create the jump table

        var jumpTableExpression = source.Scopes[0]
            .CreateJumpTable( source.Scopes, stateFieldExpression );

        bodyExpressions.Add( jumpTableExpression );

        // Optimize node ordering to reduce goto calls

        var nodes = OptimizeNodeOrder( source.Scopes );

        // Emit the body of the MoveNext method

        var hoistingVisitor = new HoistingVisitor(
            typeof( TResult ),
            stateMachineInstance,
            fieldMembers,
            stateFieldExpression,
            builderFieldExpression,
            finalResultFieldExpression,
            exitLabel,
            source.ReturnValue );

        bodyExpressions.AddRange( nodes.Select( hoistingVisitor.Visit ) );

        ParameterExpression[] variables = (source.ReturnValue != null)
            ? [source.ReturnValue]
            : [];

        // Create a try-catch block to handle exceptions

        var exceptionParameter = Expression.Parameter( typeof( Exception ), "ex" );
        var tryCatchBlock = Expression.TryCatch(
            Expression.Block( typeof( void ), variables, bodyExpressions ),
            Expression.Catch(
                exceptionParameter,
                Expression.Block(
                    Expression.Assign( stateFieldExpression, Expression.Constant( -2 ) ),
                    Expression.Call(
                        builderFieldExpression,
                        nameof( AsyncTaskMethodBuilder<TResult>.SetException ),
                        null,
                        exceptionParameter
                    )
                )
            )
        );

        // return the lambda expression for MoveNext

        var moveNextBody = Expression.Block( tryCatchBlock, Expression.Label( exitLabel ) );
        return Expression.Lambda( moveNextBody, stateMachineInstance );
    }

    private static List<NodeExpression> OptimizeNodeOrder( List<StateScope> scopes )
    {
        for ( var i = 1; i < scopes.Count - 1; i++ )
        {
            scopes[i].Nodes = OrderNodes( scopes[i].ScopeId, scopes[i].Nodes );
        }

        return OrderNodes( scopes[0].ScopeId, scopes[0].Nodes );

        static List<NodeExpression> OrderNodes( int currentScopeId, List<NodeExpression> nodes )
        {
            // Optimize node order for better performance by performing a greedy depth-first
            // search to find the best order of execution for each node.
            //
            // Doing this will allow us to reduce the number of goto calls in the final machine.
            //
            // The first node is always the start node, and the last node is always the final node.

            var ordered = new List<NodeExpression>( nodes.Count );
            var visited = new HashSet<NodeExpression>( nodes.Count );

            // Perform greedy DFS for every unvisited node

            for ( var index = 0; index < nodes.Count; index++ )
            {
                var node = nodes[index];

                if ( !visited.Contains( node ) )
                    Visit( node );
            }

            // Make sure the final state is last

            var finalNode = nodes.FirstOrDefault( x => x.Transition == null );

            if ( finalNode != null && ordered.Last() != finalNode )
            {
                ordered.Remove( finalNode );
                ordered.Add( finalNode );
            }

            // Update the order property of each node

            for ( var index = 0; index < ordered.Count; index++ )
            {
                ordered[index].MachineOrder = index;
            }

            return ordered;

            void Visit( NodeExpression node )
            {
                while ( node != null && visited.Add( node ) )
                {
                    ordered.Add( node );
                    node = node.Transition?.FallThroughNode;

                    if ( node?.ScopeId != currentScopeId )
                        return;
                }
            }
        }
    }
}

public static class StateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod;
    private static readonly ModuleBuilder ModuleBuilder;
    private static int __id;

    static StateMachineBuilder()
    {
        BuildStateMachineMethod = typeof( StateMachineBuilder )
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
            .First( x => x.Name == nameof( Create ) && x.IsGenericMethod );

        // Create the state machine module
        var assemblyName = new AssemblyName( "RuntimeStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        ModuleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );
    }

    public static Expression Create( Type resultType, LoweringResult source, bool createRunner = true )
    {
        // If the result type is void, use the internal VoidTaskResult type
        if ( resultType == typeof( void ) )
            resultType = typeof( IVoidResult );

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );
        return (Expression) buildStateMachine.Invoke( null, [source, createRunner] );
    }

    internal static Expression Create<TResult>( LoweringResult source, bool createRunner = true )
    {
        var typeName = $"StateMachine{Interlocked.Increment( ref __id )}";

        var stateMachineBuilder = new StateMachineBuilder<TResult>( ModuleBuilder, typeName );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( source, __id, createRunner );

        return stateMachineExpression; // the-best expression breakpoint ever
    }
}
