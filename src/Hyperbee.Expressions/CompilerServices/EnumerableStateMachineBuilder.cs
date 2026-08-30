using System.Collections;
using System.Collections.Concurrent;
//using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hyperbee.Collections;

using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

public delegate bool YieldMoveNextDelegate<in T>( T stateMachine );

internal delegate EnumerableLoweringInfo YieldLoweringTransformer();

internal class EnumerableStateMachineBuilder<TResult> : CoroutineStateMachineBuilder<TResult>
{

    protected static class FieldName
    {
        // special names to prevent collisions with user identifiers
        public const string MoveNextDelegate = "__moveNextDelegate<>";
        public const string Constants = "__constants<>";

        // declared by EnumerableStateMachineBase<TResult>
        public const string State = nameof( EnumerableStateMachineBase<TResult>.__state );
        public const string Current = nameof( EnumerableStateMachineBase<TResult>.__current );
        public const string Disposing = nameof( EnumerableStateMachineBase<TResult>.__disposing );
    }

    private static readonly Type BaseType = typeof( EnumerableStateMachineBase<> ).MakeGenericType( typeof( TResult ) );

    public EnumerableStateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
        : base( moduleBuilder, typeName )
    {
    }

    public Expression CreateStateMachine(
        YieldLoweringTransformer loweringTransformer,
        int id,
        ExternVariables externVariables = null,
        bool canEmitIntoType = false )
    {
        return BuildStateMachine( () => loweringTransformer(), id, externVariables, canEmitIntoType );
    }

    protected override Expression BuildStartExpression(
        int id,
        StateMachineContext context,
        Type stateMachineType,
        List<FieldAssignment> assignments )
    {
        // Conceptually:
        //
        // var stateMachine = new YieldStateMachine();
        //
        // stateMachine.__state = -1;
        // stateMachine.<extern_fields> = <extern_fields>;
        //
        // return (IEnumerable<TResult>) stateMachine;

        var loweringInfo = (EnumerableLoweringInfo) context.LoweringInfo;

        var stateMachineVariable = Variable( stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign( Field( stateMachineVariable, FieldName.State ), Constant( -1 ) )
        };

        for ( var index = 0; index < assignments.Count; index++ )
            bodyExpressions.Add( assignments[index]( stateMachineVariable, stateMachineType ) );

        if ( context.ExternVariables != null )
        {
            // copy the enclosing cells into their fields before the machine is handed out
            bodyExpressions.AddRange( context.ExternVariables.AssignFields( stateMachineVariable, stateMachineType ) );
        }

        bodyExpressions.Add( stateMachineVariable );

        return Block( [.. loweringInfo.Variables, stateMachineVariable], bodyExpressions );
    }

    protected override Type BuildWithEmittedMoveNext(
        int id,
        StateMachineContext context,
        ICoroutineMethodBuilder methodBuilder,
        out List<FieldAssignment> assignments )
    {
        // Conceptually:
        //
        // class YieldStateMachine : EnumerableStateMachineBase<TResult>
        // {
        //     public object[] __constants<>;
        //     public override bool MoveNext() { ... }
        // }
        //
        // The body is emitted before CreateType(), so it is written against the open type
        // and reaches its own fields through the builders.

        var baseType = BaseType;

        var typeBuilder = ModuleBuilder.DefineType( TypeName, TypeAttributes.Public, baseType );

        var constantsFieldBuilder = typeBuilder.DefineField( FieldName.Constants, typeof( object[] ), FieldAttributes.Public );

        var hoisted = HoistedVariables.DefineFields(
            typeBuilder,
            context.LoweringInfo.ScopedVariables,
            FieldName.Constants,
            FieldName.State,
            FieldName.Current
        );

        context.ExternVariables?.DefineFields( typeBuilder );
        context.VariableFields = HoistedVariables.AsFields( hoisted );

        var moveNextMethod = typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof( bool ),
            Type.EmptyTypes );

        // The body first parameter is the instance, so it maps to IL arg 0.

        var stateMachine = Parameter( typeBuilder, $"sm<{id}>" );

        var body = CreateMoveNextBodyExpression( context, stateMachine, typeBuilder );

        var constants = methodBuilder.Emit( [stateMachine], body, typeof( bool ), moveNextMethod, constantsFieldBuilder );

        typeBuilder.DefineMethodOverride( moveNextMethod, baseType.GetMethod( nameof( EnumerableStateMachineBase<TResult>.MoveNext ) )! );

        ImplementClone( typeBuilder, baseType, context, constantsFieldBuilder );

        assignments = [];

        if ( constants.Length > 0 )
        {
            assignments.Add( ( stateMachineVariable, stateMachineType ) =>
                Assign( Field( stateMachineVariable, stateMachineType.GetField( FieldName.Constants )! ), Constant( constants ) ) );
        }

        return typeBuilder.CreateType();
    }

    protected override Type BuildWithDelegateMoveNext( int id, StateMachineContext context, out List<FieldAssignment> assignments )
    {
        // Conceptually:
        //
        // stateMachine.__moveNextDelegate<> = (YieldStateMachine sm) => { ... }

        var stateMachineType = CreateStateMachineType( context );
        var moveNextLambda = CreateMoveNextBody( id, context, stateMachineType );

        // Compiler choice flows through the ambient context (CoroutineBuilderContext.Current),
        // never through ExpressionRuntimeOptions. Null ambient = the System compiler handles
        // MoveNext in the outer compilation context. Non-null ambient = compile the lambda
        // once here and embed the delegate as a constant, so the outer compilation sees a
        // constant rather than a nested lambda it has to compile itself.
        //
        // A body that reads a variable from the enclosing expression cannot be pre-compiled:
        // compiled on its own it would lose the variable. Emit it inline instead, so the
        // enclosing compiler shares the variable through its own closure mechanism.

        var coroutineBuilder = CoroutineBuilderContext.Current;

        Expression moveNextDelegate = coroutineBuilder == null || FreeVariableScanner.HasFreeVariables( moveNextLambda )
            ? moveNextLambda
            : Constant( coroutineBuilder.Create( moveNextLambda ), moveNextLambda.Type );

        assignments =
        [
            ( stateMachineVariable, _ ) =>
                Assign( Field( stateMachineVariable, FieldName.MoveNextDelegate ), moveNextDelegate )
        ];

        return stateMachineType;
    }

    private static LambdaExpression CreateMoveNextBody( int id, StateMachineContext context, Type stateMachineType )
    {
        var stateMachine = Parameter( stateMachineType, $"sm<{id}>" );

        return Lambda(
            typeof( YieldMoveNextDelegate<> ).MakeGenericType( stateMachineType ),
            CreateMoveNextBodyExpression( context, stateMachine, stateMachineType ),
            stateMachine
        );
    }

    private static Expression CreateMoveNextBodyExpression(
        StateMachineContext context,
        ParameterExpression stateMachine,
        Type stateMachineType )
    {
        // State, current and Dispose are declared by the base type, so they are real runtime
        // members even while the derived type is still open.

        var baseType = BaseType;

        var success = Parameter( typeof( bool ), "success" );

        var stateField = Field( stateMachine, baseType.GetField( FieldName.State )! );
        var currentField = Field( stateMachine, baseType.GetField( FieldName.Current )! );
        var disposingField = Field( stateMachine, baseType.GetField( FieldName.Disposing )! );

        var disposeMethod = baseType.GetMethod( nameof( EnumerableStateMachineBase<TResult>.Dispose ) )!;

        var exitLabel = Label( typeof( bool ), "ST_EXIT" );

        context.StateMachineInfo = new EnumerableStateMachineInfo(
            stateMachine,
            exitLabel,
            stateField,
            currentField,
            success,
            disposingField
        );

        var body = Block(
                [success],
                // This should be a try fault, but fails with preferInterpretation (see: https://github.com/dotnet/runtime/issues/114081)
                TryFinally(
                    Block(
                        CreateBody(
                            context,
                            Assign( stateField, Constant( -2 ) ),
                            Assign( success, Constant( true ) ),
                            Return( exitLabel, Constant( false ), typeof( bool ) )
                        )
                    ),
                    Block(
                        IfThen( Not( success ),
                            Call( stateMachine, disposeMethod )
                        )
                    )
                ),
                Label( exitLabel, defaultValue: Constant( false ) )
            );

        // The body is already closed: the hoisting pass turned each enclosing variable into
        // a read of the field that carries it.

        return body;
    }

    private Type CreateStateMachineType( StateMachineContext context )
    {
        // The interfaces and their plumbing live on the base type, so only the fields and
        // MoveNext are emitted here.

        var baseType = BaseType;

        var typeBuilder = ModuleBuilder.DefineType( TypeName, TypeAttributes.Public, baseType );

        // Define: fields

        var moveNextDelegateType = typeof( YieldMoveNextDelegate<> ).MakeGenericType( typeBuilder );

        var moveNextDelegateField = typeBuilder.DefineField(
            FieldName.MoveNextDelegate,
            moveNextDelegateType,
            FieldAttributes.Public );

        // local variables in the current scope for this state-machine

        var fieldNames = HoistedVariables.DefineFields(
            typeBuilder,
            context.LoweringInfo.ScopedVariables,
            FieldName.MoveNextDelegate,
            FieldName.State,
            FieldName.Current
        );

        // variables the body reads from the enclosing scope travel by field, so the body
        // itself stays closed and needs no closure

        context.ExternVariables?.DefineFields( typeBuilder );

        // Define: methods

        ImplementMoveNext( typeBuilder, baseType, moveNextDelegateField, moveNextDelegateType );
        ImplementClone( typeBuilder, baseType, context, moveNextDelegateField );

        // Close the type builder
        var stateMachineType = typeBuilder.CreateType();

        context.VariableFields = HoistedVariables.MapFields(
            fieldNames,
            stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public ) );

        return stateMachineType;
    }

    // A machine is its own enumerator, which it can be exactly once. Enumerating again needs
    // a copy that carries the same enclosing values but starts with its own state and its own
    // locals -- so the carried field is copied and everything else is left at its default.

    private static void ImplementClone(
        TypeBuilder typeBuilder,
        Type baseType,
        StateMachineContext context,
        FieldBuilder carried )
    {
        var constructor = typeBuilder.DefineDefaultConstructor( MethodAttributes.Public );

        var fields = new List<FieldBuilder> { carried };

        context.ExternVariables?.AddFields( fields );

        var cloneMethod = typeBuilder.DefineMethod(
            "Clone",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            baseType,
            Type.EmptyTypes );

        var ilGen = cloneMethod.GetILGenerator();

        ilGen.DeclareLocal( typeBuilder );

        ilGen.Emit( OpCodes.Newobj, constructor );
        ilGen.Emit( OpCodes.Stloc_0 );

        for ( var index = 0; index < fields.Count; index++ )
        {
            ilGen.Emit( OpCodes.Ldloc_0 );
            ilGen.Emit( OpCodes.Ldarg_0 );
            ilGen.Emit( OpCodes.Ldfld, fields[index] );
            ilGen.Emit( OpCodes.Stfld, fields[index] );
        }

        ilGen.Emit( OpCodes.Ldloc_0 );
        ilGen.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride(
            cloneMethod,
            baseType.GetMethod( "Clone", BindingFlags.Instance | BindingFlags.NonPublic )! );
    }

    private static void ImplementMoveNext(
        TypeBuilder typeBuilder,
        Type baseType,
        FieldBuilder moveNextDelegateField,
        Type moveNextDelegateType )
    {
        var moveNextMethod = typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof( bool ),
            Type.EmptyTypes );

        var ilGen = moveNextMethod.GetILGenerator();

        //  public override bool MoveNext()
        //  {
        //    return __moveNextDelegate<>( this );
        //  }
        ilGen.Emit( OpCodes.Ldarg_0 );
        ilGen.Emit( OpCodes.Ldfld, moveNextDelegateField );
        ilGen.Emit( OpCodes.Ldarg_0 );

        var moveNextInvoke = typeof( YieldMoveNextDelegate<> ).GetMethod( "Invoke" )!;

        ilGen.Emit( OpCodes.Callvirt, TypeBuilder.GetMethod( moveNextDelegateType, moveNextInvoke ) );
        ilGen.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( moveNextMethod, baseType.GetMethod( nameof( EnumerableStateMachineBase<TResult>.MoveNext ) )! );
    }

}

public static class YieldStateMachineBuilder
{
    private static readonly MethodInfo BuildYieldStateMachineMethod;
    private static int __id;

    const string StateMachineTypeName = "YieldStateMachine";

    static YieldStateMachineBuilder()
    {
        BuildYieldStateMachineMethod = typeof( YieldStateMachineBuilder )
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
            .First( method => method.Name == nameof( Create ) && method.IsGenericMethod );
    }

    // Bound once per result type. See the matching comment in AsyncStateMachineBuilder.

    private delegate Expression CreateStateMachineDelegate(
        YieldLoweringTransformer loweringTransformer,
        ExpressionRuntimeOptions options,
        ExternVariables externVariables,
        bool canEmitIntoType );

    private static readonly ConcurrentDictionary<Type, CreateStateMachineDelegate> CreateByResultType = new();

    internal static Expression Create( Type resultType, YieldLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        if ( resultType == typeof( void ) )
            throw new ArgumentException( "IEnumerable must have a valid result type", nameof( resultType ) );

        var create = CreateByResultType.GetOrAdd( resultType, static type =>
            BuildYieldStateMachineMethod
                .MakeGenericMethod( type )
                .CreateDelegate<CreateStateMachineDelegate>() );

        return create( loweringTransformer, options, externVariables, canEmitIntoType );
    }

    internal static Expression Create<TResult>( YieldLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        options ??= new ExpressionRuntimeOptions();

        var typeId = Interlocked.Increment( ref __id );
        var typeName = $"{StateMachineTypeName}{typeId}";

        // Get ModuleBuilder from provider using ModuleKind.Enumerable
        var moduleBuilder = options.ModuleBuilderProvider.GetModuleBuilder( ModuleKind.Enumerable );

        var stateMachineBuilder = new EnumerableStateMachineBuilder<TResult>( moduleBuilder, typeName );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( loweringTransformer, __id, externVariables, canEmitIntoType );

        // The async builder has always reported its state machine here. This one did not,
        // which is why SourceHandler saw nothing for a BlockEnumerable.

        if ( options.SourceHandler != null )
            options.SourceHandler( GetDebugView( stateMachineExpression ) );

        return stateMachineExpression; // the-best expression breakpoint ever
    }

    [UnsafeAccessor( UnsafeAccessorKind.Method, Name = "get_DebugView" )]
    private static extern string GetDebugView( Expression expression );
}
