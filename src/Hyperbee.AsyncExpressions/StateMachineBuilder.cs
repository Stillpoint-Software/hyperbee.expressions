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
    private FieldBuilder _stateField;
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

        var constructor = _stateMachineType.GetConstructor( [typeof( Action )] );
        return Expression.New( constructor!, moveNextLambda );
    }

    private void CreateStateMachineType( BlockExpression block )
    {
        // Define the state machine type
        //
        // public class StateMachineTypeN : IAsyncStateMachine
        // {
        //      public int _state;
        //      public AsyncTaskMethodBuilder<TResult> _builder;
        //      public TResult _finalResult;
        //      public Action _moveNextLambda;
        //      
        //      // Variables
        //      public int _variable1;
        //      public int _variable2;
        //
        //      // Awaiters
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter1;
        //      public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _awaiter2;
        //
        //      public StateMachineTypeN(Action moveNextLambda)
        //      {
        //          _moveNextLambda = moveNextLambda;
        //      }
        //
        //      public void MoveNext() => _moveNextLambda(this);
        // }

        _typeBuilder = _moduleBuilder.DefineType( _typeName, TypeAttributes.Public, typeof( object ), [typeof( IAsyncStateMachine )] );

        // Define fields
        _stateField = _typeBuilder.DefineField( "_state", typeof( int ), FieldAttributes.Private );
        _builderField = _typeBuilder.DefineField( "_builder", typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ), FieldAttributes.Private );
        _finalResultField = _typeBuilder.DefineField( "_finalResult", typeof( TResult ), FieldAttributes.Private );
        _moveNextLambdaField = _typeBuilder.DefineField( "_moveNextLambda", typeof( Action<> ).MakeGenericType( _typeBuilder ), FieldAttributes.Private ); // Action<TStateMachine>

        // Define variable fields
        _variableFields = [];
        foreach ( var variable in block.Variables )
        {
            var field = _typeBuilder.DefineField( $"_{variable.Name}", variable.Type, FieldAttributes.Private );
            _variableFields.Add( field );
        }

        // Define awaiter fields
        _awaiterFields = [];
        foreach ( var expr in block.Expressions )
        {
            if ( !TryGetAwaiterType( expr, out Type awaiterType ) )
            {
                continue;
            }

            var awaiterField = _typeBuilder.DefineField( $"_awaiter{_awaiterFields.Count}", awaiterType, FieldAttributes.Private );
            _awaiterFields.Add( awaiterField );
        }

        // Define constructor: public StateMachineType(Action<StateMachineType> moveNextLambda)
        var constructor = _typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, [_moveNextLambdaField.FieldType] );
        var ilGenerator = constructor.GetILGenerator();
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Call, typeof( object ).GetConstructor( Type.EmptyTypes )! ); // base()
        ilGenerator.Emit( OpCodes.Ldarg_0 ); // this
        ilGenerator.Emit( OpCodes.Ldarg_1 ); // moveNextLambda
        ilGenerator.Emit( OpCodes.Stfld, _moveNextLambdaField ); // this._moveNextLambda = moveNextLambda
        ilGenerator.Emit( OpCodes.Ret );

        // Define method: public void MoveNext() => _moveNextLambda(this);
        var moveNextMethod = _typeBuilder.DefineMethod( "MoveNext", MethodAttributes.Public | MethodAttributes.Virtual, typeof( void ), Type.EmptyTypes );
        var moveNextIlGenerator = moveNextMethod.GetILGenerator();
        moveNextIlGenerator.Emit( OpCodes.Ldarg_0 ); // this
        moveNextIlGenerator.Emit( OpCodes.Ldfld, _moveNextLambdaField ); // load _moveNextLambda
        moveNextIlGenerator.Emit( OpCodes.Ldarg_0 ); // load this as argument for _moveNextLambda
        moveNextIlGenerator.Emit( OpCodes.Callvirt, _moveNextLambdaField.FieldType.GetMethod( "Invoke" )! ); // _moveNextLambda(this)
        moveNextIlGenerator.Emit( OpCodes.Ret );

        _stateMachineType = _typeBuilder.CreateTypeInfo()!.AsType();
    }

    private static bool TryGetAwaiterType( Expression expr, out Type awaiterType )
    {
        awaiterType = null;

        if ( expr is not MethodCallExpression methodCall || !typeof(Task).IsAssignableFrom( methodCall.Type ) )
        {
            return false;
        }

        var genericArgument = methodCall.Type.IsGenericType ? methodCall.Type.GetGenericArguments()[0] : typeof( void );
        awaiterType = typeof( ConfiguredTaskAwaitable<> ).MakeGenericType( genericArgument ).GetNestedType( "ConfiguredTaskAwaiter" )!;

        return true;

    }

    // Builds the MoveNext expression and compiles it
    private LambdaExpression CreateMoveNextExpression( BlockExpression block )
    {
        var stateMachineInstance = Expression.Parameter( _stateMachineType, "stateMachine" );

        var bodyExpressions = new List<Expression>
            {
                Expression.Assign(Expression.Field(stateMachineInstance, _builderField),
                    Expression.Call(typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null))
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
                    : typeof( ConfiguredTaskAwaitable );

                var awaiterField = _typeBuilder.DefineField( $"_awaiter_{i}", awaiterType.GetNestedType( "ConfiguredTaskAwaiter" )!, FieldAttributes.Private );
                _awaiterFields.Add( awaiterField );

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
            else
            {
                var assignFinalResult = Expression.Assign( Expression.Field( stateMachineInstance, _finalResultField ), blockExpr );
                bodyExpressions.Add( assignFinalResult );
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
