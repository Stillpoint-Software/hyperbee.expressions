using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hyperbee.Collections;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

public interface IVoidResult; // Marker interface for void Task results
public delegate void MoveNextDelegate<in T>( T stateMachine ) where T : IAsyncStateMachine;

internal delegate AsyncLoweringInfo AsyncLoweringTransformer();

internal class AsyncStateMachineBuilder<TResult>
{
    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;
    private readonly ExpressionRuntimeOptions _options;

    protected static class FieldName
    {
        // special names to prevent collisions with user identifiers

        public const string Builder = "__builder<>";
        public const string Constants = "__constants<>";
        public const string FinalResult = "__final<>";
        public const string MoveNextDelegate = "__moveNextDelegate<>";
        public const string State = "__state<>";
    }

    public AsyncStateMachineBuilder( ModuleBuilder moduleBuilder, string typeName, ExpressionRuntimeOptions options )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
        _options = options;
    }

    public Expression CreateStateMachine( AsyncLoweringTransformer loweringTransformer, int id, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        ArgumentNullException.ThrowIfNull( loweringTransformer, nameof( loweringTransformer ) );

        var loweringInfo = loweringTransformer();
        var context = new StateMachineContext
        {
            LoweringInfo = loweringInfo,
            ExternVariables = externVariables,
            CanEmitIntoType = canEmitIntoType
        };

        return BuildStateMachineExpression( id, context );
    }

    private Expression BuildStateMachineExpression( int id, StateMachineContext context )
    {
        // A builder that can emit into a MethodBuilder makes MoveNext the machine's own
        // method. Otherwise the body becomes a delegate the machine holds in a field, which
        // is the only option for a compiler that cannot emit into a type under construction.

        return context.CanEmitIntoType && CoroutineBuilderContext.Current is ICoroutineMethodBuilder methodBuilder
            ? BuildWithEmittedMoveNext( id, context, methodBuilder )
            : BuildWithDelegateMoveNext( id, context );
    }

    private Expression BuildWithEmittedMoveNext( int id, StateMachineContext context, ICoroutineMethodBuilder methodBuilder )
    {
        // Conceptually:
        //
        // class StateMachine : IAsyncStateMachine
        // {
        //     public object[] __constants<>;
        //     void IAsyncStateMachine.MoveNext() { ... }
        // }
        //
        // The body is emitted before CreateType(), so it is written against the open type
        // and reaches its own fields through the builders.

        var typeBuilder = _moduleBuilder.DefineType(
            _typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            typeof( object ),
            [typeof( IAsyncStateMachine )] );

        typeBuilder.AddInterfaceImplementation( typeof( IAsyncStateMachine ) );

        var stateFieldBuilder = typeBuilder.DefineField( FieldName.State, typeof( int ), FieldAttributes.Public );

        var builderFieldBuilder = typeBuilder.DefineField(
            FieldName.Builder,
            typeof( AsyncTaskMethodBuilderBox<> ).MakeGenericType( typeof( TResult ) ),
            FieldAttributes.Public );

        var constantsFieldBuilder = typeBuilder.DefineField( FieldName.Constants, typeof( object[] ), FieldAttributes.Public );

        var hoisted = HoistedVariables.DefineFields(
            typeBuilder,
            context.LoweringInfo.ScopedVariables,
            FieldName.State,
            FieldName.Builder,
            FieldName.Constants
        );

        context.ExternVariables?.DefineFields( typeBuilder );
        context.VariableFields = HoistedVariables.AsFields( hoisted );

        var finalResultFieldBuilder = hoisted.Values.FirstOrDefault( field => field.Name == FieldName.FinalResult );

        var moveNextMethod = typeBuilder.DefineMethod(
            "IAsyncStateMachine.MoveNext",
            MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof( void ),
            Type.EmptyTypes );

        ImplementSetStateMachine( typeBuilder, builderFieldBuilder );

        // The body's first parameter is the instance, so it maps to IL arg 0.

        var stateMachine = Parameter( typeBuilder, $"sm<{id}>" );

        var body = CreateMoveNextBodyExpression(
            context,
            typeBuilder,
            stateMachine,
            Field( stateMachine, stateFieldBuilder ),
            Field( stateMachine, builderFieldBuilder ),
            Field( stateMachine, finalResultFieldBuilder! ) );

        var constants = methodBuilder.Emit( [stateMachine], body, typeof( void ), moveNextMethod, constantsFieldBuilder );

        typeBuilder.DefineMethodOverride( moveNextMethod, typeof( IAsyncStateMachine ).GetMethod( "MoveNext" )! );

        var stateMachineType = typeBuilder.CreateType();
        var builderField = stateMachineType.GetField( FieldName.Builder )!;

        var stateMachineVariable = Variable( stateMachineType, $"stateMachine<{id}>" );

        var bodyExpression = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign(
                Field( stateMachineVariable, builderField ),
                New( builderField.FieldType.GetConstructor( Type.EmptyTypes )! ) ),
            Assign(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.State )! ),
                Constant( -1 ) )
        };

        if ( constants.Length > 0 )
        {
            bodyExpression.Add( Assign(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.Constants )! ),
                Constant( constants ) ) );
        }

        if ( context.ExternVariables != null )
            bodyExpression.AddRange( context.ExternVariables.AssignFields( stateMachineVariable, stateMachineType ) );

        bodyExpression.Add( Call(
            Field( stateMachineVariable, builderField ),
            builderField.FieldType.GetMethod( "Start" )!.MakeGenericMethod( stateMachineType ),
            stateMachineVariable ) );

        bodyExpression.Add( Property(
            Field( stateMachineVariable, builderField ),
            builderField.FieldType.GetProperty( "Task" )! ) );

        return Block( [stateMachineVariable], bodyExpression );
    }

    private Expression BuildWithDelegateMoveNext( int id, StateMachineContext context )
    {
        // Conceptually:
        //
        // var stateMachine = new StateMachine();
        //
        // stateMachine.__builder<> = new AsyncTaskMethodBuilderBox<TResult>();
        // stateMachine.__state<> = -1;
        //
        // stateMachine.__moveNextDelegate<> = (StateMachine sm) => { ... }
        // stateMachine.__builder<>.Start<StateMachineType>( ref stateMachine );
        //
        // return stateMachine.__builder<>.Task;

        var stateMachineType = CreateStateMachineType( context, out var fields );
        var delegateType = typeof( MoveNextDelegate<> ).MakeGenericType( stateMachineType );
        var moveNextExpression = CreateMoveNextBody( id, context, stateMachineType, fields, delegateType );

        // Compiler choice flows through the ambient context (CoroutineBuilderContext.Current),
        // never through ExpressionRuntimeOptions. Null ambient = System compiler handles MoveNext
        // in the outer compilation context, preserving closure-based variable sharing.
        // Non-null ambient = pre-compile the lambda and embed as a Constant.
        //
        // A body that reads a variable from the enclosing expression cannot be pre-compiled:
        // compiled on its own it would lose the variable. Emit it inline instead, so the
        // enclosing compiler shares the variable through its own closure mechanism. The
        // ambient builder still compiles it, as a nested lambda of that compilation.
        var coroutineBuilder = CoroutineBuilderContext.Current;
        Expression moveNextDelegate = coroutineBuilder == null || FreeVariableScanner.HasFreeVariables( moveNextExpression )
            ? moveNextExpression
            : Constant( coroutineBuilder.Create( moveNextExpression ), delegateType );

        var stateMachineVariable = Variable( stateMachineType, $"stateMachine<{id}>" );

        var bodyExpression = new List<Expression>
        {
            Assign( stateMachineVariable, New( stateMachineType ) ),
            Assign(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.Builder )! ),
                New( typeof( AsyncTaskMethodBuilderBox<> )
                    .MakeGenericType( typeof( TResult ) )
                    .GetConstructor( Type.EmptyTypes )! )
            ),
            Assign(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.State )! ),
                Constant( -1 )
            ),
            Assign(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.MoveNextDelegate )! ),
                moveNextDelegate
            ),
            Call(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.Builder )! ),
                stateMachineType.GetField( FieldName.Builder )!.FieldType
                    .GetMethod( "Start" )!
                    .MakeGenericMethod( stateMachineType ),
                stateMachineVariable
            ),
            Property(
                Field( stateMachineVariable, stateMachineType.GetField( FieldName.Builder )! ),
                stateMachineType.GetField( FieldName.Builder )!.FieldType.GetProperty( "Task" )!
            )
        };

        if ( context.ExternVariables != null )
        {
            // copy the enclosing variables into their fields before the machine starts
            bodyExpression.InsertRange( 4, context.ExternVariables.AssignFields( stateMachineVariable, stateMachineType ) );
        }

        return Block( [stateMachineVariable], bodyExpression );
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
            typeof( AsyncTaskMethodBuilderBox<> ).MakeGenericType( typeof( TResult ) ),
            FieldAttributes.Public
        );

        // local variables in the current scope for this state-machine

        var fieldNames = HoistedVariables.DefineFields(
            typeBuilder,
            context.LoweringInfo.ScopedVariables,
            FieldName.MoveNextDelegate,
            FieldName.State,
            FieldName.Builder
        );

        // variables the body reads from the enclosing scope travel by field, so the body
        // itself stays closed and can be compiled once

        context.ExternVariables?.DefineFields( typeBuilder );

        // Define: methods

        ImplementMoveNext( typeBuilder, moveNextDelegateField, moveNextDelegateType );
        ImplementSetStateMachine( typeBuilder, builderField );

        // Close the type builder
        var stateMachineType = typeBuilder.CreateType();

        fields = [.. stateMachineType.GetFields( BindingFlags.Instance | BindingFlags.Public )];

        context.VariableFields = HoistedVariables.MapFields( fieldNames, fields );

        return stateMachineType;
    }

    // --- Implementation methods ---

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

        ilGenerator.Emit( OpCodes.Ldarg_0 );
        ilGenerator.Emit( OpCodes.Ldflda, builderFieldInfo );
        ilGenerator.Emit( OpCodes.Ldarg_1 );

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

        ilGenerator.Emit( OpCodes.Ldarg_0 );
        ilGenerator.Emit( OpCodes.Ldfld, moveNextDelegateField );
        ilGenerator.Emit( OpCodes.Ldarg_0 );

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
        FieldInfo[] fields,
        Type lambdaType = null
    )
    {
        // Set context state-machine-info

        var stateMachine = Parameter( stateMachineType, $"sm<{id}>" );

        var stateField = Field( stateMachine, Array.Find( fields, f => f.Name == FieldName.State )! );
        var builderField = Field( stateMachine, Array.Find( fields, f => f.Name == FieldName.Builder )! );
        var finalResultField = Field( stateMachine, Array.Find( fields, f => f.Name == FieldName.FinalResult )! );

        var body = CreateMoveNextBodyExpression( context, stateMachineType, stateMachine, stateField, builderField, finalResultField );

        return lambdaType != null
            ? Lambda( lambdaType, body, stateMachine )
            : Lambda( body, stateMachine );
    }

    private static Expression CreateMoveNextBodyExpression(
        StateMachineContext context,
        Type stateMachineType,
        ParameterExpression stateMachine,
        MemberExpression stateField,
        MemberExpression builderField,
        MemberExpression finalResultField )
    {
        var exitLabel = Label( "ST_EXIT" );

        context.StateMachineInfo = new AsyncStateMachineInfo(
            stateMachine,
            exitLabel,
            stateField,
            builderField,
            finalResultField
        );

        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var body = Block(
            TryCatch(
                Block(
                    typeof( void ),
                    CreateBody(
                        context,
                        Assign( stateField, Constant( -2 ) ),
                        Call(
                            builderField,
                            nameof( AsyncTaskMethodBuilderBox<TResult>.SetResult ),
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
                            nameof( AsyncTaskMethodBuilderBox<TResult>.SetException ),
                            null,
                            exceptionParam
                        )
                    )
                )
            ),
            Label( exitLabel )
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
}

public static class AsyncStateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod;
    private static int __id;

    const string StateMachineTypeName = "StateMachine";

    static AsyncStateMachineBuilder()
    {
        BuildStateMachineMethod = typeof( AsyncStateMachineBuilder )
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
            .First( method => method.Name == nameof( Create ) && method.IsGenericMethod );
    }

    internal static Expression Create( Type resultType, AsyncLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        if ( resultType == typeof( void ) )
            resultType = typeof( IVoidResult );

        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );

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

    internal static Expression Create<TResult>( AsyncLoweringTransformer loweringTransformer, ExpressionRuntimeOptions options = null, ExternVariables externVariables = null, bool canEmitIntoType = false )
    {
        options ??= new ExpressionRuntimeOptions();

        var typeId = Interlocked.Increment( ref __id );
        var typeName = $"{StateMachineTypeName}{typeId}";

        // Get ModuleBuilder from provider using ModuleKind.Async
        var moduleBuilder = options.ModuleBuilderProvider.GetModuleBuilder( ModuleKind.Async );

        var stateMachineBuilder = new AsyncStateMachineBuilder<TResult>( moduleBuilder, typeName, options );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( loweringTransformer, __id, externVariables, canEmitIntoType );

        if ( options.SourceHandler != null )
        {
            var debugView = GetDebugView( stateMachineExpression );
            options.SourceHandler( debugView );
        }

        return stateMachineExpression; // the-best expression breakpoint ever
    }

    [UnsafeAccessor( UnsafeAccessorKind.Method, Name = "get_DebugView" )]
    private static extern string GetDebugView( Expression expression );
}

