using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

public interface IVoidResult; // Marker interface for void Task results

public delegate void MoveNextDelegate<T>( ref T stateMachine ) where T : IAsyncStateMachine;

public class StateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;

    private static class FieldName
    {
        // use special names to prevent collisions with user fields
        public const string Builder = "__builder<>";
        public const string FinalResult = "__finalResult<>";
        public const string MoveNextDelegate = "__moveNextDelegate<>";
        public const string State = "__state<>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool IsSystemField( string name ) => name.EndsWith( "<>" );
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
        // var moveNextLambda = (StateMachine stateMachine) => { ... };
        // var stateMachine = new StateMachine( moveNextLambda );

        var stateMachineType = CreateStateMachineType( source, out var fields );
        var moveNextLambda = CreateMoveNextBody( id, source, stateMachineType, fields );

        // Initialize the state machine

        var stateMachineVariable = Expression.Variable(
            stateMachineType,
            $"stateMachine<{id}>"
        );

        var assignNew = Expression.Assign(
            stateMachineVariable,
            Expression.New( stateMachineType )
        );

        var assignStateField = Expression.Assign(
            Expression.Field(
                stateMachineVariable,
                stateMachineType.GetField( FieldName.State )!
            ),
            Expression.Constant( -1 )
        );

        var assignMoveNextDelegate = Expression.Assign(
            Expression.Field(
                stateMachineVariable,
                stateMachineType.GetField( FieldName.MoveNextDelegate )!
            ),
            moveNextLambda
        );

        if ( !createRunner )
        {
            var stateMachineInitialization = Expression.Block(
                [stateMachineVariable],
                assignNew,
                assignStateField,
                assignMoveNextDelegate,
                stateMachineVariable
            );

            return stateMachineInitialization;
        }

        // Run the state-machine
        //
        // Conceptually:
        //
        // stateMachine._builder.Start<StateMachineType>( ref stateMachine );
        // return stateMachine.__builder<>.Task;

        var builderFieldInfo = stateMachineType.GetField( FieldName.Builder )!;
        var builderField = Expression.Field( stateMachineVariable, builderFieldInfo );

        var startMethod = builderFieldInfo.FieldType
            .GetMethod( "Start" )!
            .MakeGenericMethod( stateMachineType );

        var callBuilderStart = Expression.Call(
            builderField,
            startMethod,
            stateMachineVariable // ref stateMachine
        );

        var taskProperty = builderFieldInfo.FieldType.GetProperty( "Task" );
        var taskExpression = Expression.Property( builderField, taskProperty! );

        return Expression.Block(
            [stateMachineVariable],
            assignNew,
            assignStateField,
            assignMoveNextDelegate,
            callBuilderStart,
            taskExpression
        );
    }

    private Type CreateStateMachineType( LoweringResult source, out IEnumerable<FieldInfo> fields )
    {
        var typeBuilder = _moduleBuilder.DefineType(
            _typeName,
            TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed,
            typeof( ValueType ) // struct
        );

        typeBuilder.AddInterfaceImplementation( typeof( IAsyncStateMachine ) );

        // Define: system fields
        var moveNextDelegateType = typeof( MoveNextDelegate<> ).MakeGenericType( typeBuilder );

        var moveNextDelegateField = typeBuilder.DefineField(
            FieldName.MoveNextDelegate,
            moveNextDelegateType,
            FieldAttributes.Public );

        typeBuilder.DefineField(
            FieldName.State,
            typeof( int ),
            FieldAttributes.Public
        );

        var builderField = typeBuilder.DefineField(
            FieldName.Builder,
            typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ),
            FieldAttributes.Public
        );

        typeBuilder.DefineField(
            FieldName.FinalResult,
            typeof( TResult ),
            FieldAttributes.Public
        );

        // Define: state-machine fields
        ImplementFields( typeBuilder, source );

        // Define: methods

        ImplementMoveNext( typeBuilder, moveNextDelegateField, moveNextDelegateType );
        ImplementSetStateMachine( typeBuilder, builderField );

        var stateMachineType = typeBuilder.CreateType();

        fields = stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )
            .Where( field => !FieldName.IsSystemField( field.Name ) )
            .ToArray();

        return stateMachineType;
    }

    private static void ImplementFields( TypeBuilder typeBuilder, LoweringResult result )
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

    private static void ImplementSetStateMachine( TypeBuilder typeBuilder, FieldInfo builderFieldInfo )
    {
        // Define the IAsyncStateMachine.SetStateMachine method
        //
        // public void SetStateMachine( IAsyncStateMachine stateMachine )
        // {
        //    __builder<>.SetStateMachine( stateMachine );
        // }
        var setStateMachineMethod = typeBuilder.DefineMethod(
            "IAsyncStateMachine.SetStateMachine",
            MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof( void ),
            [typeof( IAsyncStateMachine )]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // Load 'this'
        ilGenerator.Emit( OpCodes.Ldflda, builderFieldInfo ); // Load address of '__builder<>'
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // Load 'stateMachine' argument

        var setStateMachineOnBuilder = builderFieldInfo
            .FieldType
            .GetMethod( "SetStateMachine", [typeof( IAsyncStateMachine )]
        );

        ilGenerator.Emit( OpCodes.Call, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( setStateMachineMethod,
            typeof( IAsyncStateMachine ).GetMethod( "SetStateMachine" )! );
    }

    private static void ImplementMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextDelegateField, Type moveNextDelegateType )
    {
        // Define the MoveNext method
        //
        // public void MoveNext()
        // {
        //    __moveNextDelegate<>( ref this );
        // }

        var moveNextMethod = typeBuilder.DefineMethod(
            "IAsyncStateMachine.MoveNext",
            MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof( void ),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // Load 'this'
        ilGenerator.Emit( OpCodes.Ldfld, moveNextDelegateField ); // Load '__moveNextDelegate'

        ilGenerator.Emit( OpCodes.Ldarga_S, 0 ); // Load the address of 'this'

        var openInvokeMethod = typeof( MoveNextDelegate<> ).GetMethod( "Invoke" )!;
        var invokeMethod = TypeBuilder.GetMethod( moveNextDelegateType, openInvokeMethod );

        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod ); // Call the delegate
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( moveNextMethod, typeof( IAsyncStateMachine ).GetMethod( "MoveNext" )! );
    }

    private static LambdaExpression CreateMoveNextBody(
        int id,
        LoweringResult source,
        Type stateMachineType,
        IEnumerable<FieldInfo> fields
    )
    {
        // Example state-machine:
        //

        /*
            (ref StateMachine1 sm<1>) =>
            {
                var var<1> = sm<1>.__stateMachineData<>;
                try
                {
                    switch (var<1>.__state<>)
                    {
                        case 0:
                            var<1>.__state<> = -1;
                            goto ST_0002;
            
                        case 1:
                            var<1>.__state<> = -1;
                            goto ST_0004;
                    }
            
                    var awaitable = Task<int>;
                    var<1>.__awaiter<0> = AwaitBinder.GetAwaiter(ref awaitable, false);
            
                    if (!var<1>.__awaiter<0>.IsCompleted)
                    {
                        var<1>.__state<> = 0;
                        var<1>.__builder<>.AwaitUnsafeOnCompleted(ref var<1>.__awaiter<0>, ref sm<1>);
                        return;
                    }

                ST_0002:
                    var<1>.__result<0> = AwaitBinder.GetResult(ref var<1>.__awaiter<0>);
                    var<1>.__result<1> = var<1>.__result<0>;
                    Task<int> awaitable;
                    awaitable = Task<int>;
                    var<1>.__awaiter<1> = AwaitBinder.GetAwaiter(ref awaitable, false);
            
                    if (!var<1>.__awaiter<1>.IsCompleted)
                    {
                        var<1>.__state<> = 1;
                        var<1>.__builder<>.AwaitUnsafeOnCompleted(ref var<1>.__awaiter<1>, ref sm<1>);
                        return;
                    }

                ST_0004:
                    var<1>.__result<1> = AwaitBinder.GetResult(ref var<1>.__awaiter<1>);
                    var<1>.__finalResult<> = var<1>.__result<1>;
                    var<1>.__state<> = -2;
                    var<1>.__builder<>.SetResult(var<1>.__finalResult<>);
                }
                catch (Exception ex)
                {
                    var<1>.__state<> = -2;
                    var<1>.__builder<>.SetException(ex);
                }
            }
           
        */

        var stateMachine = Expression.Parameter( stateMachineType.MakeByRefType(), $"sm<{id}>" );

        var stateFieldExpression = Expression.Field( stateMachine, FieldName.State );
        var builderFieldExpression = Expression.Field( stateMachine, FieldName.Builder );
        var finalResultFieldExpression = Expression.Field( stateMachine, FieldName.FinalResult );

        var fieldMembers = fields.Select( x => Expression.Field( stateMachine, x ) ).ToArray();
        var exitLabel = Expression.Label( "ST_EXIT" );

        // Create the jump table

        var jumpTableExpression = source.Scopes[0]
            .CreateJumpTable( source.Scopes, stateFieldExpression );

        // Optimize node ordering to reduce goto calls

        var nodes = OptimizeNodeOrder( source.Scopes );

        // Emit the body of the MoveNext method

        var hoistingVisitor = new HoistingVisitor(
            stateMachine,
            fieldMembers,
            stateFieldExpression,
            builderFieldExpression,
            finalResultFieldExpression,
            exitLabel,
            source.ReturnValue );

        var bodyExpressions = new List<Expression>( 16 ) // preallocate slots for expressions
        {
            jumpTableExpression
        };

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

        var moveNextBody = Expression.Block(
            tryCatchBlock,
            Expression.Label( exitLabel )
        );

        var moveNextDelegateType = typeof( MoveNextDelegate<> ).MakeGenericType( stateMachineType );

        return Expression.Lambda( moveNextDelegateType, moveNextBody, stateMachine ); // takes a ref to the state machine
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
