using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

public interface IVoidResult; // Marker interface for void Task results
public delegate void MoveNextDelegate<in T>( T stateMachine ) where T : IAsyncStateMachine;

internal delegate LoweringInfo LoweringTransformer();

internal class StateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;

    protected static class FieldName
    {
        // special names to prevent collisions with user identifiers

        public const string Builder = "__builder<>";
        public const string FinalResult = "__final<>";
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

    public Expression CreateStateMachine( LoweringTransformer loweringTransformer, int id )
    {
        // Lower the async expression
        //
        var loweringInfo = loweringTransformer();

        if ( loweringInfo.AwaitCount == 0 )
            throw new InvalidOperationException( "The target of a lowering operation must contain at least one await." );

        if ( loweringInfo.Scopes[0].States == null )
            throw new InvalidOperationException( "States must be set before creating state machine." );

        // Create the state-machine builder context
        //
        var context = new StateMachineContext { LoweringInfo = loweringInfo };

        // Create the state-machine
        //
        // Conceptually:
        //
        // var stateMachine = new StateMachine();
        // stateMachine.__state<> = -1;
        // stateMachine.__moveNextDelegate<> = (ref StateMachine stateMachine) => { ... }

        var stateMachineType = CreateStateMachineType( context, out var fields );
        var moveNextLambda = CreateMoveNextBody( id, context, stateMachineType, fields );

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

        // create local copy of extern variables on state-machine

        foreach ( var externVariable in loweringInfo.ExternVariables )
        {
            var field = fields.First( field => field.Name == externVariable.Name );
            var assignField = Assign(
                Field( stateMachineVariable, field ),
                externVariable
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

    private Type CreateStateMachineType( StateMachineContext context, out FieldInfo[] fields )
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

        // variables from this state-machine

        foreach ( var parameterExpression in context.LoweringInfo.Variables.OfType<ParameterExpression>() )
        {
            typeBuilder.DefineField(
                parameterExpression.Name ?? parameterExpression.ToString(),
                parameterExpression.Type,
                FieldAttributes.Public
            );
        }

        // variables from other state-machines

        foreach ( var parameterExpression in context.LoweringInfo.ExternVariables )
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

        fields = [.. stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )];

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
        StateMachineContext context,
        Type stateMachineType,
        FieldInfo[] fields
    )
    {
        // Set context state-machine-info

        var stateMachine = Parameter( stateMachineType, $"sm<{id}>" );

        var stateField = Field( stateMachine, FieldName.State );
        var builderField = Field( stateMachine, FieldName.Builder );
        var finalResultField = Field( stateMachine, FieldName.FinalResult );

        var exitLabel = Label( "ST_EXIT" );

        context.StateMachineInfo = new StateMachineInfo(
            stateMachine,
            exitLabel,
            stateField,
            builderField,
            finalResultField
        );

        // Create final lambda with try-catch block

        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        return Lambda(
            typeof( MoveNextDelegate<> ).MakeGenericType( stateMachineType ),
            Block(
                TryCatch(
                    Block(
                        typeof( void ),
                        CreateBody(
                            fields,
                            context,
                            Assign( stateField, Constant( -2 ) ),
                            Call(
                                builderField,
                                nameof( AsyncTaskMethodBuilder<TResult>.SetResult ),
                                null,
                                finalResultField
                            )
                        )
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
                ),
                Label( exitLabel )
            ),
            stateMachine
        );
    }

    private static IEnumerable<Expression> CreateBody( FieldInfo[] fields, StateMachineContext context, params Expression[] antecedents )
    {
        var stateMachineInfo = context.StateMachineInfo;
        var loweringInfo = context.LoweringInfo;

        var scopes = loweringInfo.Scopes;

        // Optimize source nodes

        StateMachineOptimizer.Optimize( loweringInfo );

        // Create the body expressions

        var firstScope = scopes[0];

        var jumpTable = JumpTableBuilder.Build(
            firstScope,
            scopes,
            stateMachineInfo.StateField
        );

        // hoist variables

        var bodyExpressions = HoistVariables(
            jumpTable,
            firstScope.GetExpressions( context ),
            fields,
            stateMachineInfo.StateMachine
        );

        // return the body expressions

        return bodyExpressions.Concat( antecedents );
    }

    private static IEnumerable<Expression> HoistVariables( Expression jumpTable, List<Expression> expressions, FieldInfo[] fields, ParameterExpression stateMachine )
    {
        var fieldMembers = fields
            .Select( field => Field( stateMachine, field ) )
            .ToDictionary( x => x.Member.Name );

        var hoistingVisitor = new HoistingVisitor( fieldMembers );

        return HoistingSource().Select( hoistingVisitor.Visit );

        IEnumerable<Expression> HoistingSource()
        {
            yield return jumpTable;

            foreach ( var expression in expressions )
                yield return expression;
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

    internal static Expression Create( Type resultType, LoweringTransformer loweringTransformer )
    {
        if ( resultType == typeof( void ) )
            resultType = typeof( IVoidResult );

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );

        return (Expression) buildStateMachine.Invoke( null, [loweringTransformer] );
    }

    internal static Expression Create<TResult>( LoweringTransformer loweringTransformer )
    {
        var typeId = Interlocked.Increment( ref __id );
        var typeName = $"{StateMachineTypeName}{typeId}";

        var stateMachineBuilder = new StateMachineBuilder<TResult>( ModuleBuilder, typeName );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( loweringTransformer, __id );

        return stateMachineExpression; // the-best expression breakpoint ever
    }
}
