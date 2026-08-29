using System.Collections;
//using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using Hyperbee.Collections;

using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

public delegate bool YieldMoveNextDelegate<in T>( T stateMachine );

internal delegate EnumerableLoweringInfo YieldLoweringTransformer();

internal class EnumerableStateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;


    protected static class FieldName
    {
        // special names to prevent collisions with user identifiers
        public const string MoveNextDelegate = "__moveNextDelegate<>";
        public const string State = "__state<>";
        public const string Current = "__current<>";
    }

    public EnumerableStateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public Expression CreateStateMachine( YieldLoweringTransformer loweringTransformer, int id, ExternVariables externVariables = null )
    {
        var loweringInfo = loweringTransformer();

        // Create the state-machine builder context
        //
        var context = new StateMachineContext
        {
            LoweringInfo = loweringInfo,
            ExternVariables = externVariables
        };

        // Create the state-machine
        //
        // Conceptually:
        //
        // var stateMachine = new YieldStateMachine();
        // 
        // stateMachine.__state<> = -1;
        // stateMachine.<extern_fields> = <extern_fields>;
        //
        // stateMachine.__moveNextDelegate<> = (ref YieldStateMachine stateMachine) => { ... }
        //
        // return (IEnumerable<TResult>) stateMachine;

        var stateMachineType = CreateStateMachineType( context, out var fields );
        var moveNextLambda = CreateMoveNextBody( id, context, stateMachineType, fields );

        var stateMachineVariable = Variable( stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign( Field( stateMachineVariable, FieldName.State ), Constant( -1 ) ),
            Assign( Field( stateMachineVariable, FieldName.MoveNextDelegate ), moveNextLambda ),
            stateMachineVariable
        };

        if ( externVariables != null )
        {
            // copy the enclosing cells into their fields before the machine is handed out
            bodyExpressions.InsertRange( 2, externVariables.AssignFields( stateMachineVariable, stateMachineType ) );
        }

        return Block( [.. loweringInfo.Variables, stateMachineVariable], bodyExpressions );
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
        var success = Parameter( typeof( bool ), "success" );

        var stateField = Field( stateMachine, FieldName.State );
        var currentField = Field( stateMachine, FieldName.Current );

        var exitLabel = Label( typeof( bool ), "ST_EXIT" );

        context.StateMachineInfo = new EnumerableStateMachineInfo(
            stateMachine,
            exitLabel,
            stateField,
            currentField,
            success
        );

        var body = Block(
                [success],
                // This should be a try fault, but fails with preferInterpretation (see: https://github.com/dotnet/runtime/issues/114081)
                TryFinally(
                    Block(
                        CreateBody(
                            fields,
                            context,
                            Assign( stateField, Constant( -2 ) ),
                            Assign( success, Constant( true ) ),
                            Return( exitLabel, Constant( false ), typeof( bool ) )
                        )
                    ),
                    Block(
                        IfThen( Not( success ),
                            Call( stateMachine, "Dispose", Type.EmptyTypes )
                        )
                    )
                ),
                Label( exitLabel, defaultValue: Constant( false ) )
            );

        // Close the body: an enclosing variable becomes a read of the field that carries it.

        var closedBody = context.ExternVariables?.Close( body, stateMachine, stateMachineType ) ?? body;

        return Lambda(
            typeof( YieldMoveNextDelegate<> ).MakeGenericType( stateMachineType ),
            closedBody,
            stateMachine
        );
    }

    private static IEnumerable<Expression> CreateBody( FieldInfo[] fields, StateMachineContext context, params Expression[] antecedents )
    {
        var stateMachineInfo = context.StateMachineInfo;
        var loweringInfo = context.LoweringInfo;

        var scopes = loweringInfo.Scopes;

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
            context.VariableFields,
            stateMachineInfo.StateMachine
        );

        // return the body expressions

        return bodyExpressions.Concat( antecedents );
    }

    private static IEnumerable<Expression> HoistVariables(
        Expression jumpTable,
        IReadOnlyList<Expression> expressions,
        IReadOnlyDictionary<ParameterExpression, FieldInfo> variableFields,
        ParameterExpression stateMachine )
    {
        var hoistingVisitor = new HoistingVisitor( stateMachine, variableFields );

        return HoistingSource().Select( hoistingVisitor.Visit );

        IEnumerable<Expression> HoistingSource()
        {
            yield return jumpTable;

            foreach ( var expression in expressions )
                yield return expression;
        }
    }

    private Type CreateStateMachineType( StateMachineContext context, out FieldInfo[] fields )
    {
        var typeBuilder = _moduleBuilder.DefineType(
            _typeName, TypeAttributes.Public, typeof( object ),
        [
            typeof( IEnumerable<> ).MakeGenericType( typeof(TResult) ),
            typeof( IEnumerator<> ).MakeGenericType( typeof(TResult) ),
            typeof( IDisposable )
        ] );

        typeBuilder.AddInterfaceImplementation( typeof( IEnumerable<> ).MakeGenericType( typeof( TResult ) ) );
        typeBuilder.AddInterfaceImplementation( typeof( IEnumerator<> ).MakeGenericType( typeof( TResult ) ) );
        typeBuilder.AddInterfaceImplementation( typeof( IDisposable ) );

        // Define: fields

        var moveNextDelegateType = typeof( YieldMoveNextDelegate<> ).MakeGenericType( typeBuilder );

        var moveNextDelegateField = typeBuilder.DefineField(
            FieldName.MoveNextDelegate,
            moveNextDelegateType,
            FieldAttributes.Public );

        var stateField = typeBuilder.DefineField(
            FieldName.State,
            typeof( int ),
            FieldAttributes.Public
        );

        var currentField = typeBuilder.DefineField(
            FieldName.Current,
            typeof( TResult ),
            FieldAttributes.Public
        );

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

        ImplementIEnumerable( typeBuilder );
        ImplementIEnumerableType( typeBuilder, stateField );
        ImplementIEnumerator( typeBuilder, moveNextDelegateField, moveNextDelegateType, currentField );
        ImplementIEnumeratorType( typeBuilder, currentField );
        ImplementIDisposable( typeBuilder, stateField );

        // Close the type builder
        var stateMachineType = typeBuilder.CreateType();

        fields = [.. stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )];

        context.VariableFields = HoistedVariables.MapFields( fieldNames, fields );

        return stateMachineType;
    }

    private static void ImplementIEnumerable( TypeBuilder typeBuilder )
    {
        var getEnumeratorMethod = typeBuilder.DefineMethod( "GetEnumerator",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( IEnumerator ), Type.EmptyTypes );
        var ilGen = getEnumeratorMethod.GetILGenerator();

        // Get the GetEnumerator method from IEnumerable<TResult>
        var genericGetEnumeratorMethod = typeof( IEnumerable<> )
            .MakeGenericType( typeof( TResult ) )
            .GetMethod( "GetEnumerator" )!;

        // IEnumerator IEnumerable.GetEnumerator()
        // {
        //   return (IEnumerator) ((IEnumerable<TResult>)this).GetEnumerator();
        // }
        ilGen.Emit( OpCodes.Ldarg_0 );
        ilGen.Emit( OpCodes.Castclass, typeof( IEnumerable<> ).MakeGenericType( typeof( TResult ) ) );
        ilGen.Emit( OpCodes.Callvirt, genericGetEnumeratorMethod );
        ilGen.Emit( OpCodes.Castclass, typeof( IEnumerator ) );
        ilGen.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( getEnumeratorMethod, typeof( IEnumerable ).GetMethod( "GetEnumerator" )! );
    }
    private static void ImplementIEnumerableType( TypeBuilder typeBuilder, FieldBuilder stateField )
    {
        var getEnumeratorMethod = typeBuilder
            .DefineMethod( "GetEnumerator",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof( IEnumerator<> ).MakeGenericType( typeof( TResult ) ),
                Type.EmptyTypes );

        var ilGen = getEnumeratorMethod.GetILGenerator();

        // TODO: this needs more logic for handling threads and multiple enumerators

        //this.__state<> = 0;
        ilGen.Emit( OpCodes.Ldarg_0 );
        ilGen.Emit( OpCodes.Ldc_I4_0 );
        ilGen.Emit( OpCodes.Stfld, stateField );

        // var enumerator = this;
        // return (IEnumerator<TResult>) enumerator;
        var enumeratorLocal = ilGen.DeclareLocal( typeBuilder );
        ilGen.Emit( OpCodes.Ldarg_0 );
        ilGen.Emit( OpCodes.Stloc, enumeratorLocal );
        ilGen.Emit( OpCodes.Ldloc, enumeratorLocal );
        ilGen.Emit( OpCodes.Castclass, typeof( IEnumerator<> ).MakeGenericType( typeof( TResult ) ) );
        ilGen.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride(
            getEnumeratorMethod,
            typeof( IEnumerable<> ).MakeGenericType( typeof( TResult ) ).GetMethod( "GetEnumerator" )! );
    }
    private static void ImplementIEnumerator( TypeBuilder typeBuilder, FieldBuilder moveNextDelegateField, Type moveNextDelegateType, FieldBuilder currentField )
    {
        var moveNextMethod = typeBuilder.DefineMethod( "MoveNext", MethodAttributes.Public | MethodAttributes.Virtual, typeof( bool ), Type.EmptyTypes );
        var currentMethod = typeBuilder.DefineMethod( "get_Current", MethodAttributes.Public | MethodAttributes.Virtual, typeof( object ), Type.EmptyTypes );
        var resetMethod = typeBuilder.DefineMethod( "Reset", MethodAttributes.Public | MethodAttributes.Virtual, typeof( void ), Type.EmptyTypes );

        var ilGenMoveNext = moveNextMethod.GetILGenerator();
        var ilGenCurrent = currentMethod.GetILGenerator();
        var ilGenReset = resetMethod.GetILGenerator();

        // MoveNext
        //  bool IEnumerator.MoveNext()
        //  {
        //    return __moveNextDelegate<>( this );
        //  }
        ilGenMoveNext.Emit( OpCodes.Ldarg_0 );
        ilGenMoveNext.Emit( OpCodes.Ldfld, moveNextDelegateField );
        ilGenMoveNext.Emit( OpCodes.Ldarg_0 );

        var moveNextInvoke = typeof( YieldMoveNextDelegate<> ).GetMethod( "Invoke" )!;
        var invokeMethod = TypeBuilder.GetMethod( moveNextDelegateType, moveNextInvoke );

        ilGenMoveNext.Emit( OpCodes.Callvirt, invokeMethod );
        ilGenMoveNext.Emit( OpCodes.Ret );

        // Current
        // object IEnumerator.Current
        // {
        //   get {
        //     return (object) __current<>;
        //   }
        // }
        ilGenCurrent.Emit( OpCodes.Ldarg_0 );
        ilGenCurrent.Emit( OpCodes.Ldfld, currentField );
        ilGenCurrent.Emit( OpCodes.Box, typeof( TResult ) );
        ilGenCurrent.Emit( OpCodes.Ret );

        // Reset
        ilGenReset.Emit( OpCodes.Newobj, typeof( NotSupportedException ).GetConstructor( Type.EmptyTypes )! );
        ilGenReset.Emit( OpCodes.Throw );

        typeBuilder.DefineMethodOverride( moveNextMethod, typeof( IEnumerator ).GetMethod( "MoveNext" )! );
        typeBuilder.DefineMethodOverride( currentMethod, typeof( IEnumerator ).GetProperty( "Current" )!.GetMethod! );
        typeBuilder.DefineMethodOverride( resetMethod, typeof( IEnumerator ).GetMethod( "Reset" )! );
    }
    private static void ImplementIEnumeratorType( TypeBuilder typeBuilder, FieldBuilder currentField )
    {
        var currentMethod = typeBuilder.DefineMethod( "get_Current", MethodAttributes.Public | MethodAttributes.Virtual, typeof( TResult ), Type.EmptyTypes );

        var ilGenCurrent = currentMethod.GetILGenerator();

        // Current
        // TResult IEnumerator<TResult>.Current
        // {
        //   get {
        //     return __current<>;
        //   }
        // }
        ilGenCurrent.Emit( OpCodes.Ldarg_0 );
        ilGenCurrent.Emit( OpCodes.Ldfld, currentField );
        ilGenCurrent.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( currentMethod, typeof( IEnumerator<> ).MakeGenericType( typeof( TResult ) ).GetProperty( "Current" )!.GetMethod! );
    }
    private static void ImplementIDisposable( TypeBuilder typeBuilder, FieldBuilder stateField )
    {
        var disposeMethod = typeBuilder.DefineMethod( "Dispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof( void ), Type.EmptyTypes );
        var ilGen = disposeMethod.GetILGenerator();

        // TODO: Dispose all disposable fields
        // TODO: NOTE: this could include nested IEnumerable<> state machines

        //this.__state<> = -2;
        ilGen.Emit( OpCodes.Ldarg_0 );
        ilGen.Emit( OpCodes.Ldc_I4, -2 );
        ilGen.Emit( OpCodes.Stfld, stateField );
        ilGen.Emit( OpCodes.Ret );

        typeBuilder.DefineMethodOverride( disposeMethod, typeof( IDisposable ).GetMethod( "Dispose" )! );
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

    internal static Expression Create( Type resultType, YieldLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null )
    {
        if ( resultType == typeof( void ) )
            throw new ArgumentException( "IEnumerable must have a valid result type", nameof( resultType ) );

        var buildStateMachine = BuildYieldStateMachineMethod.MakeGenericMethod( resultType );

        try
        {
            return (Expression) buildStateMachine.Invoke( null, [loweringTransformer, options, externVariables] );
        }
        catch ( TargetInvocationException ex ) when ( ex.InnerException != null )
        {
            // surface lowering failures to the caller, not the reflection wrapper
            ExceptionDispatchInfo.Capture( ex.InnerException ).Throw();
            throw;
        }
    }

    internal static Expression Create<TResult>( YieldLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null )
    {
        options ??= new ExpressionRuntimeOptions();

        var typeId = Interlocked.Increment( ref __id );
        var typeName = $"{StateMachineTypeName}{typeId}";

        // Get ModuleBuilder from provider using ModuleKind.Enumerable
        var moduleBuilder = options.ModuleBuilderProvider.GetModuleBuilder( ModuleKind.Enumerable );

        var stateMachineBuilder = new EnumerableStateMachineBuilder<TResult>( moduleBuilder, typeName );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( loweringTransformer, __id, externVariables );

        return stateMachineExpression; // the-best expression breakpoint ever
    }
}
