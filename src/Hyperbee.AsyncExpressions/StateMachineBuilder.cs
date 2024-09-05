using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

// ReSharper disable once InconsistentNaming
internal interface VoidResult;

public class StateMachineBuilder<TResult>
{
    private BlockExpression _blockSource;

    private readonly ModuleBuilder _moduleBuilder;
    private readonly string _typeName;
    private Type _stateMachineType;
    private TypeBuilder _typeBuilder;
    private FieldBuilder _builderField;
    private FieldBuilder _finalResultField;
    private FieldBuilder _moveNextLambdaField;
    private List<FieldBuilder> _variableFields;
    private List<FieldBuilder> _awaiterFields;

    public StateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        _moduleBuilder = moduleBuilder;
        _typeName = typeName;
    }

    public void SetSource( BlockExpression blockSource )
    {
        _blockSource = blockSource;
    }

    public Expression CreateStateMachine()
    {
        if ( _blockSource == null )
        {
            throw new InvalidOperationException( "Source must be set before creating state machine." );
        }

        // Create the state machine type
        CreateStateMachineType( _blockSource );

        // Compile MoveNext lambda and assign to state machine
        var moveNextLambda = CreateMoveNextExpression( _blockSource );
        var lambdaType = typeof( Action<> ).MakeGenericType( _stateMachineType );

        var constructor = _stateMachineType.GetConstructor( [lambdaType] );
        return Expression.New( constructor!, moveNextLambda );
    }

    private void CreateStateMachineType( BlockExpression block )
    {
        // Define the state machine type
        //
        // public class StateMachineType : IAsyncStateMachine
        // {
        //      public int _state;
        //      public AsyncTaskMethodBuilder<TResult> _builder;
        //      public TResult _finalResult;
        //      public Action _moveNextLambda;
        //      
        //      // Variables (example)
        //      public int _variable1;
        //      public int _variable2;
        //
        //      // Awaiters (example)
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter1;
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter2;
        //
        //      public StateMachineType()
        //      {
        //      }
        //
        //      public void SetLambda<T>(Action<T> moveNextLambda)
        //      {
        //         Action<object> moveNext = obj => moveNextLambda( (StateMachineType) obj );
        //         moveNext(this);
        //      }
        //
        //      public void MoveNext() => _moveNextLambda(this);
        //      public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine( stateMachine );
        // }

        _typeBuilder = _moduleBuilder.DefineType( _typeName, TypeAttributes.Public, typeof( object ), [typeof( IAsyncStateMachine )]);

        _typeBuilder.DefineField( "_state", typeof( int ), FieldAttributes.Private );
        _builderField = _typeBuilder.DefineField( "_builder", typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ), FieldAttributes.Private );
        _finalResultField = _typeBuilder.DefineField( "_finalResult", typeof( TResult ), FieldAttributes.Private );
        _moveNextLambdaField = _typeBuilder.DefineField( "_moveNextLambda", typeof( Action<> ).MakeGenericType( _typeBuilder ), FieldAttributes.Private );

        EmitBlockFields( block );
        EmitConstructor();
        EmitSetMoveNextMethod();
        EmitMoveNextMethod();
        EmitSetStateMachineMethod();

        _stateMachineType = _typeBuilder.CreateTypeInfo()!.AsType();
    }

    private void EmitConstructor()
    {
        // Define a parameterless constructor: public StateMachineType()
        var constructor = _typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes );
        var ilGenerator = constructor.GetILGenerator();

        // Call the base constructor (object)
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Call, typeof(object).GetConstructor( Type.EmptyTypes )! ); // base()
        ilGenerator.Emit( OpCodes.Ret ); // return
    }

    private void EmitBlockFields( BlockExpression block )
    {
        // Define: variable fields
        _variableFields = [];
        foreach ( var variable in block.Variables )
        {
            var field = _typeBuilder.DefineField( $"_{variable.Name}", variable.Type, FieldAttributes.Private );
            _variableFields.Add( field );
        }

        // Define: awaiter fields
        _awaiterFields = [];
        for ( var i = 0; i < block.Expressions.Count; i++ )
        {
            var expr = block.Expressions[i]; 

            if ( !TryGetAwaiterType( expr, out Type awaiterType ) )
                continue; // Not an awaitable expression

            var fieldName = $"_awaiter_{i}"; // `i` should match the index of the expression to align with state machine logic

            var awaiterField = _typeBuilder.DefineField( fieldName, awaiterType, FieldAttributes.Private );
            _awaiterFields.Add( awaiterField );
        }
    }

    private void EmitSetMoveNextMethod()
    {
        // Define: public void SetMoveNext(Action<StateMachineTypeN> moveNext)
        //
        //  public void SetMoveNext<T>(Action<T> moveNext)
        //  {
        //     _moveNextLambda = moveNext;
        //  }

        var setMoveNextMethod = _typeBuilder.DefineMethod( 
            "SetMoveNext", 
            MethodAttributes.Public | MethodAttributes.Virtual, 
            typeof(void),
            [typeof(Action<>).MakeGenericType( _typeBuilder )]
        );

        var ilGenerator = setMoveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, _moveNextLambdaField ); // this._moveNextLambda = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret ); // return
    }

    private void EmitMoveNextMethod()
    {
        // Define: public void MoveNext()
        //
        //  public void MoveNext()
        //  {
        //      Action<object> moveNext = obj => _moveNextLambda( (StateMachineTypeN) obj );
        //      moveNext( this );
        //  }

        var moveNextMethod = _typeBuilder.DefineMethod(
            "MoveNext",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            Type.EmptyTypes
        );

        var ilGenerator = moveNextMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this`
        ilGenerator.Emit( OpCodes.Ldfld, _moveNextLambdaField ); // load `_moveNextLambda`
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this` as lambda argument

        var actionObjectType = typeof(Action<object>);
        var invokeMethod = actionObjectType.GetMethod( "Invoke" );
        ilGenerator.Emit( OpCodes.Callvirt, invokeMethod! ); // Call Action<object>.Invoke(this)

        ilGenerator.Emit( OpCodes.Ret );
    }

    private void EmitSetStateMachineMethod()
    {
        // Define the SetStateMachine method (from IAsyncStateMachine)

        // public void SetStateMachine( IAsyncStateMachine stateMachine )
        // {
        //    _builder.SetStateMachine( stateMachine );
        // }
        
        var setStateMachineMethod = _typeBuilder.DefineMethod(
            "SetStateMachine",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IAsyncStateMachine)]
        );

        var ilGenerator = setStateMachineMethod.GetILGenerator();

        ilGenerator.Emit( OpCodes.Ldarg_0 ); // load `this`
        ilGenerator.Emit( OpCodes.Ldfld, _builderField ); // load `_builder`
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // Load the `stateMachine` parameter

        var setStateMachineOnBuilder = typeof(AsyncTaskMethodBuilder<>)
            .MakeGenericType( typeof(TResult) )
            .GetMethod( "SetStateMachine", [typeof(IAsyncStateMachine)] );

        ilGenerator.Emit( OpCodes.Callvirt, setStateMachineOnBuilder! );
        ilGenerator.Emit( OpCodes.Ret );

        _typeBuilder.DefineMethodOverride( setStateMachineMethod, 
            typeof(IAsyncStateMachine).GetMethod( "SetStateMachine" )! 
        );
    }


    private static bool TryGetAwaiterType( Expression expr, out Type awaiterType )
    {
        awaiterType = null;

        switch ( expr )
        {
            case MethodCallExpression methodCall when typeof(Task).IsAssignableFrom( methodCall.Type ):
                awaiterType = GetAwaiterType( methodCall.Type );
                return true;

            case InvocationExpression invocation when typeof(Task).IsAssignableFrom( invocation.Type ):
                awaiterType = GetAwaiterType( invocation.Type );
                return true;

            case not null when typeof(Task).IsAssignableFrom( expr.Type ):
                awaiterType = GetAwaiterType( expr.Type );
                return true;
        }

        return false;

        static Type GetAwaiterType( Type taskType )
        {
            var genericArgument = taskType.IsGenericType ? taskType.GetGenericArguments()[0] : null;

            if ( genericArgument == null || genericArgument.FullName == "System.Threading.Tasks.VoidTaskResult" )
            {
                return typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter);
            }

            return typeof(ConfiguredTaskAwaitable<>).MakeGenericType(genericArgument).GetNestedType("ConfiguredTaskAwaiter")!;
        }
    }

    private LambdaExpression CreateMoveNextExpression( BlockExpression block )
    {
        var stateMachineInstance = Expression.Parameter( _stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
            {
                Expression.Assign(
                    Expression.Field(stateMachineInstance, _builderField),
                    Expression.Call(typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null)
                )
            };

        var blocks = block.Expressions;
        for ( var i = 0; i < blocks.Count; i++ )
        {
            var blockExpr = blocks[i];
            var blockReturnType = blockExpr.Type;

            if ( AsyncBaseExpression.IsTask( blockReturnType ) )
            {
                var awaiterType = blockReturnType.IsGenericType
                    ? typeof( ConfiguredTaskAwaitable<> ).MakeGenericType( blockReturnType.GetGenericArguments()[0] )
                    : typeof(ConfiguredTaskAwaitable<>).MakeGenericType( blockReturnType );

                var awaiterField = _awaiterFields[i]; // BF - This is the field we defined in EmitBlockFields
                //var awaiterField = _awaiterFields.First( x => x.Name == $"_awaiter_{i}" );
                // var awaiterField = _typeBuilder.DefineField( $"_awaiter_{i}", awaiterType.GetNestedType( "ConfiguredTaskAwaiter" )!, FieldAttributes.Private );
                // _awaiterFields.Add( awaiterField );

                var assignAwaiter = Expression.Assign(
                    Expression.Field( stateMachineInstance, awaiterField ),
                    Expression.Call(
                        Expression.Call( blockExpr, nameof( Task.ConfigureAwait ), null, Expression.Constant( false ) ),
                        awaiterType.GetMethod( "GetAwaiter" )! ) );

                var setupContinuation = Expression.Call(
                    Expression.Field( stateMachineInstance, _builderField ),
                    nameof( AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted ),
                    [awaiterType.GetNestedType( "ConfiguredTaskAwaiter" ), typeof( IAsyncStateMachine )],
                    Expression.Field( stateMachineInstance, awaiterField ),
                    stateMachineInstance );

                bodyExpressions.Add( Expression.Block( assignAwaiter, setupContinuation ) );
            }
            else if (i == blocks.Count - 1)
            {
                // Handle non-awaitable final block
                var assignFinalResult = Expression.Assign(Expression.Field(stateMachineInstance, _finalResultField), blockExpr!);
                bodyExpressions.Add(assignFinalResult);
            }
            else
            {
                // Handle non-awaitable block
                bodyExpressions.Add(blockExpr!);
            }
        }

        var setResult = Expression.Call(
            Expression.Field( stateMachineInstance, _builderField ),
            nameof( AsyncTaskMethodBuilder<TResult>.SetResult ),
            null,
            Expression.Field( stateMachineInstance, _finalResultField ) );

        bodyExpressions.Add( setResult );

        return Expression.Lambda( Expression.Block( bodyExpressions ), stateMachineInstance );
    }
}
