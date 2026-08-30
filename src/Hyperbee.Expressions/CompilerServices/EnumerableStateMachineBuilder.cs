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

        var stateMachineVariable = Variable( stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign( Field( stateMachineVariable, FieldName.State ), Constant( -1 ) ),
            Assign( Field( stateMachineVariable, FieldName.MoveNextDelegate ), moveNextDelegate ),
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

        fields = [.. stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )];

        context.VariableFields = HoistedVariables.MapFields( fieldNames, fields );

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
