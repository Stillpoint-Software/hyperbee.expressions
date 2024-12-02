using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

public interface IVoidResult; // Marker interface for void Task results
public delegate void MoveNextDelegate<in T>( T stateMachine ) where T : IAsyncStateMachine;

internal class StateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;

    protected static class FieldName
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

    public Expression CreateStateMachine( LoweringResult source, int id )
    {
        if ( source.Scopes[0].Nodes == null )
            throw new InvalidOperationException( "States must be set before creating state machine." );

        // Create the state-machine
        //
        // Conceptually:
        //
        // var stateMachine = new StateMachine();
        // stateMachine.__state<> = -1;
        // stateMachine.__moveNextDelegate<> = (ref StateMachine stateMachine) => { ... }

        var stateMachineType = CreateStateMachineType( source, out var fields );
        var moveNextLambda = CreateMoveNextBody( id, source, stateMachineType, fields );

        // Initialize the state machine

        var bodyExpression = new List<Expression>();

        var stateMachineVariable = Variable(
            stateMachineType,
            $"stateMachine<{id}>"
        );

        var assignNew = Assign(
            stateMachineVariable,
            New( stateMachineType )
        );
        bodyExpression.Add( assignNew );

        var assignStateField = Assign(
            Field(
                stateMachineVariable,
                stateMachineType.GetField( FieldName.State )!
            ),
            Constant( -1 )
        );
        bodyExpression.Add( assignStateField );

        // create local copy of shared variables on state-machine
        foreach ( var scopedVariable in source.ScopedVariables )
        {
            var field = fields.First( field => field.Name == scopedVariable.Name );
            var fieldExpression = Field( stateMachineVariable, field );
            var assignField = Assign(
                fieldExpression,
                scopedVariable
            );
            bodyExpression.Add( assignField );
        }

        var assignMoveNextDelegate = Assign(
            Field(
                stateMachineVariable,
                stateMachineType.GetField( FieldName.MoveNextDelegate )!
            ),
            moveNextLambda
        );
        bodyExpression.Add( assignMoveNextDelegate );

        // Run the state-machine
        //
        // Conceptually:
        //
        // stateMachine._builder.Start<StateMachineType>( ref stateMachine );
        // return stateMachine.__builder<>.Task;

        var builderFieldInfo = stateMachineType.GetField( FieldName.Builder )!;
        var builderField = Field( stateMachineVariable, builderFieldInfo );

        var startMethod = builderFieldInfo.FieldType
            .GetMethod( "Start" )!
            .MakeGenericMethod( stateMachineType );

        var callBuilderStart = Call(
            builderField,
            startMethod,
            stateMachineVariable
        );
        bodyExpression.Add( callBuilderStart );

        var taskProperty = builderFieldInfo.FieldType.GetProperty( "Task" );
        var taskExpression = Property( builderField, taskProperty! );
        bodyExpression.Add( taskExpression );

        return Block(
            [stateMachineVariable],
            bodyExpression
        );
    }

    private Type CreateStateMachineType( LoweringResult source, out FieldInfo[] fields )
    {
        var typeBuilder = _moduleBuilder.DefineType(
            _typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            typeof( object ),
            [typeof( IAsyncStateMachine )] );

        typeBuilder.AddInterfaceImplementation( typeof( IAsyncStateMachine ) );

        // Define: fields

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

        foreach ( var parameterExpression in source.Variables.OfType<ParameterExpression>() )
        {
            typeBuilder.DefineField(
                parameterExpression.Name ?? parameterExpression.ToString(),
                parameterExpression.Type,
                FieldAttributes.Public
            );
        }

        foreach ( var parameterExpression in source.ScopedVariables )
        {
            typeBuilder.DefineField(
                parameterExpression.Name ?? parameterExpression.ToString(),
                parameterExpression.Type,
                FieldAttributes.Public
            );
        }

        // Define: methods

        ImplementMoveNext( typeBuilder, moveNextDelegateField, moveNextDelegateType );
        ImplementSetStateMachine( typeBuilder, builderField );

        // Close the type builder
        var stateMachineType = typeBuilder.CreateType();

        fields = stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )
            .Where( field => !FieldName.IsSystemField( field.Name ) )
            .ToArray();

        return stateMachineType;
    }

    private static void ImplementSetStateMachine( TypeBuilder typeBuilder, FieldBuilder builderFieldInfo )
    {
        // Define the IAsyncStateMachine.SetStateMachine method
        //
        // private void IAsyncStateMachine.SetStateMachine( IAsyncStateMachine stateMachine )
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

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( setStateMachineMethod,
            typeof( IAsyncStateMachine ).GetMethod( "SetStateMachine" )! );
    }

    private static void ImplementMoveNext( TypeBuilder typeBuilder, FieldBuilder moveNextDelegateField, Type moveNextDelegateType )
    {
        // Define the IAsyncStateMachine.MoveNext method
        //
        // private void IAsyncStateMachine.MoveNext()
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
        ilGenerator.Emit( OpCodes.Ldfld, moveNextDelegateField ); // Load '__moveNextDelegate<>'
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // Load 'this'

        var openInvokeMethod = typeof( MoveNextDelegate<> ).GetMethod( "Invoke" )!;
        var invokeMethod = TypeBuilder.GetMethod( moveNextDelegateType, openInvokeMethod );

        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod );
        ilGenerator.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( moveNextMethod, typeof( IAsyncStateMachine ).GetMethod( "MoveNext" )! );
    }

    private static LambdaExpression CreateMoveNextBody(
        int id,
        LoweringResult source,
        Type stateMachineType,
        FieldInfo[] fields
    )
    {
        /* Example state-machine:

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

        var stateMachine = Parameter( stateMachineType, $"sm<{id}>" );

        var stateField = Field( stateMachine, FieldName.State );
        var builderField = Field( stateMachine, FieldName.Builder );
        var finalResultField = Field( stateMachine, FieldName.FinalResult );

        var exitLabel = Label( "ST_EXIT" );

        // Optimize source nodes

        StateMachineOptimizer.Optimize( source );

        // Variable Hoisting 

        //HoistVariables( source, fields, stateMachine );

        // Assign state-machine source to nodes

        var stateMachineSource = new StateMachineSource(
            stateMachine,
            exitLabel,
            stateField,
            builderField,
            finalResultField,
            source.ReturnValue
        );

        foreach ( var node in source.Nodes )
        {
            node.StateMachineSource = stateMachineSource; // required for node reducers
        }

        // Add the state-nodes

        var bodyExpressions = CreateBody( stateField, source );

        //
        var fieldMembers = fields
            .Select( field => Field( stateMachine, field ) )
            .ToDictionary( x => x.Member.Name );

        var hoistingVisitor = new HoistingVisitor( fieldMembers );

        var rbe = new List<Expression>();

        foreach ( var node in bodyExpressions )
        {
            rbe.Add( hoistingVisitor.Visit( node ) );
        }

        bodyExpressions = rbe;

        // Add the final builder result assignment

        bodyExpressions.AddRange(
        [
            Assign( stateField, Constant( -2 ) ),
            Call(
                builderField,
                nameof( AsyncTaskMethodBuilder<TResult>.SetResult ),
                null,
                finalResultField.Type != typeof(IVoidResult)
                    ? finalResultField
                    : Constant( null, finalResultField.Type ) // No result for IVoidResult
            )
        ] );

        // Create a try-catch block to handle exceptions

        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var tryCatchBlock = TryCatch(
            Block(
                typeof( void ),
                source.ReturnValue != null
                    ? [source.ReturnValue]
                    : [],
                bodyExpressions
            ),
            Catch(
                exceptionParam,
                Block(
                    Assign( stateField, Constant( -2 ) ),
                    Call(
                        builderField,
                        nameof( AsyncTaskMethodBuilder<TResult>.SetException ),
                        null,
                        exceptionParam
                    )
                )
            )
        );

        // Create the final lambda expression

        return Lambda(
            typeof( MoveNextDelegate<> ).MakeGenericType( stateMachineType ),
            Block(
                tryCatchBlock,
                Label( exitLabel )
            ),
            stateMachine
        );
    }

    private static List<Expression> CreateBody( MemberExpression stateField, LoweringResult source )
    {
        var firstScope = source.Scopes.First();

        var jumpTable = JumpTableBuilder.Build(
            firstScope,
            source.Scopes,
            stateField
        );

        return [jumpTable, Block( NodeExpression.Merge( firstScope.Nodes ) )]; //BF ME
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static void HoistVariables( LoweringResult source, FieldInfo[] fields, ParameterExpression stateMachine )
    {
        var fieldMembers = fields
            .Select( field => Field( stateMachine, field ) )
            .ToDictionary( x => x.Member.Name );

        var hoistingVisitor = new HoistingVisitor( fieldMembers );

        foreach ( var node in source.Nodes )
        {
            hoistingVisitor.Visit( node );
        }
    }

    private sealed class HoistingVisitor( IDictionary<string, MemberExpression> memberExpressions ) : ExpressionVisitor
    {
        protected override Expression VisitParameter( ParameterExpression node )
        {
            var name = node.Name ?? node.ToString();

            if ( memberExpressions.TryGetValue( name, out var fieldAccess ) )
                return fieldAccess;

            return node;
        }
    }
}

public static class StateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod;
    private static readonly ModuleBuilder ModuleBuilder;
    private static int __id;

    const string RuntimeAssemblyName = "RuntimeStateMachineAssembly";
    const string RuntimeModuleName = "RuntimeStateMachineModule";
    const string StateMachineTypeName = "StateMachine";

    static StateMachineBuilder()
    {
        BuildStateMachineMethod = typeof( StateMachineBuilder )
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
            .First( method => method.Name == nameof( Create ) && method.IsGenericMethod );

        // Create the state machine module
        var assemblyName = new AssemblyName( RuntimeAssemblyName );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        ModuleBuilder = assemblyBuilder.DefineDynamicModule( RuntimeModuleName );
    }

    internal static Expression Create( Type resultType, LoweringResult source )
    {
        if ( resultType == typeof( void ) )
            resultType = typeof( IVoidResult );

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );

        return (Expression) buildStateMachine.Invoke( null, [source] );
    }

    internal static Expression Create<TResult>( LoweringResult source )
    {
        var typeId = Interlocked.Increment( ref __id );
        var typeName = $"{StateMachineTypeName}{typeId}";

        var stateMachineBuilder = new StateMachineBuilder<TResult>( ModuleBuilder, typeName );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( source, __id );

        return stateMachineExpression; // the-best expression breakpoint ever
    }
}
