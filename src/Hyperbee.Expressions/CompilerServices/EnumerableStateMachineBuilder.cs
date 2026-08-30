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
        public const string Constants = "__constants<>";

        // declared by EnumerableStateMachineBase<TResult>
        public const string State = nameof( EnumerableStateMachineBase<TResult>.__state );
        public const string Current = nameof( EnumerableStateMachineBase<TResult>.__current );
    }

    private static Type BaseType => typeof( EnumerableStateMachineBase<> ).MakeGenericType( typeof( TResult ) );

    public EnumerableStateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public Expression CreateStateMachine(
        YieldLoweringTransformer loweringTransformer,
        int id,
        ExternVariables externVariables = null,
        bool canEmitIntoType = false )
    {
        var loweringInfo = loweringTransformer();

        // Create the state-machine builder context
        //
        var context = new StateMachineContext
        {
            LoweringInfo = loweringInfo,
            ExternVariables = externVariables,
            CanEmitIntoType = canEmitIntoType
        };

        // A builder that can emit into a MethodBuilder makes MoveNext the machine's own
        // method. Otherwise the body becomes a delegate the machine holds in a field, which
        // is the only option for a compiler that cannot emit into a type under construction.

        var stateMachineType = context.CanEmitIntoType && CoroutineBuilderContext.Current is ICoroutineMethodBuilder methodBuilder
            ? BuildWithEmittedMoveNext( id, context, methodBuilder, out var assignments )
            : BuildWithDelegateMoveNext( id, context, out assignments );

        // Conceptually:
        //
        // var stateMachine = new YieldStateMachine();
        //
        // stateMachine.__state = -1;
        // stateMachine.<extern_fields> = <extern_fields>;
        //
        // return (IEnumerable<TResult>) stateMachine;

        var stateMachineVariable = Variable( stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign( Field( stateMachineVariable, FieldName.State ), Constant( -1 ) )
        };

        foreach ( var assignment in assignments )
            bodyExpressions.Add( assignment( stateMachineVariable, stateMachineType ) );

        if ( externVariables != null )
        {
            // copy the enclosing cells into their fields before the machine is handed out
            bodyExpressions.AddRange( externVariables.AssignFields( stateMachineVariable, stateMachineType ) );
        }

        bodyExpressions.Add( stateMachineVariable );

        return Block( [.. loweringInfo.Variables, stateMachineVariable], bodyExpressions );
    }

    // A field assignment a build needs before the machine is handed out. Deferred because it
    // names a field of a type the build has not closed yet.

    private delegate Expression FieldAssignment( ParameterExpression stateMachine, Type stateMachineType );

    private Type BuildWithEmittedMoveNext(
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

        var typeBuilder = _moduleBuilder.DefineType( _typeName, TypeAttributes.Public, baseType );

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

        assignments = [];

        if ( constants.Length > 0 )
        {
            assignments.Add( ( stateMachineVariable, stateMachineType ) =>
                Assign( Field( stateMachineVariable, stateMachineType.GetField( FieldName.Constants )! ), Constant( constants ) ) );
        }

        return typeBuilder.CreateType();
    }

    private Type BuildWithDelegateMoveNext( int id, StateMachineContext context, out List<FieldAssignment> assignments )
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

        var disposeMethod = baseType.GetMethod( nameof( EnumerableStateMachineBase<TResult>.Dispose ) )!;

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

        // Close the body: an enclosing variable becomes a read of the field that carries it.

        return context.ExternVariables?.Close( body, stateMachine, stateMachineType ) ?? body;
    }

    private static IEnumerable<Expression> CreateBody( StateMachineContext context, params Expression[] antecedents )
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

    private Type CreateStateMachineType( StateMachineContext context )
    {
        // The interfaces and their plumbing live on the base type, so only the fields and
        // MoveNext are emitted here.

        var baseType = BaseType;

        var typeBuilder = _moduleBuilder.DefineType( _typeName, TypeAttributes.Public, baseType );

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

        // Close the type builder
        var stateMachineType = typeBuilder.CreateType();

        context.VariableFields = HoistedVariables.MapFields(
            fieldNames,
            stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public ) );

        return stateMachineType;
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

    internal static Expression Create( Type resultType, YieldLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        if ( resultType == typeof( void ) )
            throw new ArgumentException( "IEnumerable must have a valid result type", nameof( resultType ) );

        var buildStateMachine = BuildYieldStateMachineMethod.MakeGenericMethod( resultType );

        try
        {
            return (Expression) buildStateMachine.Invoke( null, [loweringTransformer, options, externVariables, canEmitIntoType] );
        }
        catch ( TargetInvocationException ex ) when ( ex.InnerException != null )
        {
            // surface lowering failures to the caller, not the reflection wrapper
            ExceptionDispatchInfo.Capture( ex.InnerException ).Throw();
            throw;
        }
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

        return stateMachineExpression; // the-best expression breakpoint ever
    }
}
