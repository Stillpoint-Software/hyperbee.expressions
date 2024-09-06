using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

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

    public Expression CreateStateMachine( bool createRunner = true )
    {
        if ( _blockSource == null )
        {
            throw new InvalidOperationException( "Source must be set before creating state machine." );
        }

        // Create the state machine type
        CreateStateMachineType( _blockSource );

        // Compile MoveNext lambda and assign to state machine
        var moveNextLambda = CreateMoveNextExpression( _blockSource );

        var stateMachineVariable = Expression.Variable( _stateMachineType, "stateMachine" );
        var builderFieldInfo = _stateMachineType.GetField( "_builder" )!;
        var setLambdaMethod = _stateMachineType.GetMethod("SetMoveNext")!;

        var constructor = _stateMachineType.GetConstructor( Type.EmptyTypes )!;

        var stateMachineExpression = Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, Expression.New( constructor ) ),
            Expression.Assign(
                Expression.Field( stateMachineVariable, builderFieldInfo ),
                Expression.Call( typeof( AsyncTaskMethodBuilder<TResult> ), nameof( AsyncTaskMethodBuilder<TResult>.Create ), null )
            ),
            Expression.Call( stateMachineVariable, setLambdaMethod, moveNextLambda ),
            stateMachineVariable
        );

        return createRunner ? CreateStateMachineRunner( stateMachineExpression ) : stateMachineExpression;
    }

    public Expression CreateStateMachineRunner( Expression stateMachineExpression )
    {
        var stateMachineVariable = Expression.Variable( stateMachineExpression.Type, "stateMachineVariable" );
        var builderFieldInfo = stateMachineExpression.Type.GetField( "_builder" )!;
        var taskFieldInfo = builderFieldInfo.FieldType.GetProperty( "Task" )!;

        return Expression.Block(
            [stateMachineVariable],
            Expression.Assign( stateMachineVariable, stateMachineExpression ),
            Expression.Call( stateMachineVariable, "MoveNext", Type.EmptyTypes ),
            Expression.Property( Expression.Field( stateMachineVariable, builderFieldInfo ), taskFieldInfo )
        );
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

        _typeBuilder.DefineField( "_state", typeof( int ), FieldAttributes.Public );
        _builderField = _typeBuilder.DefineField( "_builder", typeof( AsyncTaskMethodBuilder<> ).MakeGenericType( typeof( TResult ) ), FieldAttributes.Public );
        _finalResultField = _typeBuilder.DefineField( "_finalResult", typeof( TResult ), FieldAttributes.Public );
        _moveNextLambdaField = _typeBuilder.DefineField( "_moveNextLambda", typeof( Action<> ).MakeGenericType( _typeBuilder ), FieldAttributes.Private );

        EmitBlockFields( block );
        EmitConstructor();
        EmitSetMoveNextMethod();
        EmitMoveNextMethod();
        EmitSetStateMachineMethod();

        _stateMachineType = _typeBuilder.CreateType();
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
            var field = _typeBuilder.DefineField( $"_{variable.Name}", variable.Type, FieldAttributes.Public );
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

            var awaiterField = _typeBuilder.DefineField( fieldName, awaiterType, FieldAttributes.Public );
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
            if ( !taskType.IsGenericType )
                return typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter);

            var genericArgument = taskType.GetGenericArguments()[0];

            if ( genericArgument.FullName == "System.Threading.Tasks.VoidTaskResult" )
                throw new InvalidOperationException( "Task<VoidTaskResult> is not supported, are you missing a cast to Task?" );

            return typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter).MakeGenericType( genericArgument );
        }
    }

    private LambdaExpression CreateMoveNextExpression( BlockExpression block )
    {
        // Example of a typical state-machine:
        //
        // public void MoveNext()
        // {
        //     try
        //     {
        //         if (_state == 0)
        //         {
        //             _awaiter1 = task1.ConfigureAwait(false).GetAwaiter();
        //
        //             if (!_awaiter1.IsCompleted == false)
        //             {
        //                 _builder.AwaitUnsafeOnCompleted(ref _awaiter1, this);
        //                 return;
        //             }
        //
        //             _awaiter1.GetResult();
        //             _state = 1;
        //         }
        //
        //         if (_state == 1)
        //         {
        //             _awaiter2 = task2.ConfigureAwait(false).GetAwaiter();
        //
        //             if (!_awaiter2.IsCompleted)
        //             {
        //                 _builder.AwaitUnsafeOnCompleted(ref _awaiter2, this);
        //                 return;
        //             }
        //
        //             _awaiter2.GetResult();
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _builder.SetException(ex);
        //     }
        // }

        var stateMachineInstance = Expression.Parameter( _stateMachineType, "stateMachine" );
        var parameterVisitor = new ParameterMappingVisitor( stateMachineInstance, _variableFields );

        var buildFieldInfo = GetFieldInfo( _stateMachineType, _builderField );
        var finalResultFieldInfo = GetFieldInfo( _stateMachineType, _finalResultField );

        var bodyExpressions = new List<Expression>();

        var blocks = block.Expressions;
        int lastBlockIndex = blocks.Count - 1;


        LabelTarget returnLabel = Expression.Label( "ExitMoveNext" );

        // Each block is a state in the state machine
        for ( var i = 0; i < blocks.Count; i++ )
        {
            // Fix BlockExpression parameters to use fields from state machine
            var blockExpr = parameterVisitor.Visit( blocks[i] );
            var blockReturnType = blockExpr.Type;

            if ( AsyncBaseExpression.IsTask( blockReturnType ) )
            {
                var awaiterField = GetFieldInfo( _stateMachineType, _awaiterFields[i] );

                var configureAwaitMethod = blockExpr.Type.GetMethod( "ConfigureAwait", [typeof(bool)] )!;
                var getAwaiterMethod = configureAwaitMethod.ReturnType.GetMethod( "GetAwaiter" );

                // Evaluate the block expression to produce the task
                var evaluateTask = blockExpr;

                // Assign the awaiter field (e.g., _awaiterX = task.ConfigureAwait(false).GetAwaiter())
                var assignAwaiter = Expression.Assign(
                    Expression.Field( stateMachineInstance, awaiterField ),
                    Expression.Call(
                        Expression.Call( evaluateTask, configureAwaitMethod, Expression.Constant( false ) ),
                        getAwaiterMethod! )
                );

                // Call AwaitUnsafeOnCompleted when awaiter is not completed
                var awaiterCompletedCheck = Expression.IfThen(
                    Expression.IsFalse( Expression.Property( Expression.Field( stateMachineInstance, awaiterField ), "IsCompleted" ) ),
                    Expression.Block(
                        Expression.Call(
                            Expression.Field( stateMachineInstance, buildFieldInfo ),
                            nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
                            [awaiterField.FieldType, typeof(IAsyncStateMachine)],
                            Expression.Field( stateMachineInstance, awaiterField ),
                            stateMachineInstance
                        ),
                        Expression.Return( returnLabel ) // Return from MoveNext
                    )
                );

                // Handle case when awaiter is completed (i.e., proceed to next state)

                var getResultMethod = awaiterField.FieldType.GetMethod( "GetResult" );
                var getResult = Expression.Call( Expression.Field( stateMachineInstance, awaiterField ), getResultMethod! );

                var handleCompletedAwaiter = i == lastBlockIndex
                    ? Expression.Block(
                        Expression.Assign( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i + 1 ) ), 
                        Expression.Assign( Expression.Field( stateMachineInstance, finalResultFieldInfo ), getResult ) 
                    )
                    : Expression.Block(
                        getResult, 
                        Expression.Assign( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i + 1 ) ) 
                    );

                // Full block for `if ( state == X )`
                var stateCheck = Expression.IfThen(  
                    Expression.Equal( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i ) ), 
                    Expression.Block( assignAwaiter, awaiterCompletedCheck, handleCompletedAwaiter ) // Execute task handling logic
                );

                bodyExpressions.Add( stateCheck );
            }
            else if ( i == lastBlockIndex ) // final block: non-task
            {
                var assignFinalResult = Expression.Assign(
                    Expression.Field( stateMachineInstance, finalResultFieldInfo ), blockExpr!
                );

                var finalStateCheck = Expression.IfThen( 
                    Expression.Equal( Expression.Field( stateMachineInstance, "_state" ), Expression.Constant( i ) ), 
                    assignFinalResult
                );

                bodyExpressions.Add( finalStateCheck );
            }
        }

        // Set the final result
        var setResult = Expression.Call(
            Expression.Field( stateMachineInstance, buildFieldInfo ),
            nameof(AsyncTaskMethodBuilder<TResult>.SetResult),
            null,
            Expression.Field( stateMachineInstance, finalResultFieldInfo )
        );

        bodyExpressions.Add( setResult );
        bodyExpressions.Add( Expression.Label( returnLabel ) );

        // Return the lambda expression for the method
        return Expression.Lambda( Expression.Block( bodyExpressions ), stateMachineInstance );

        // Helper method to retrieve FieldInfo from the created type
        static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
        {
            return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
        }
    }
}

public static class StateMachineBuilder
{
    private static readonly MethodInfo BuildStateMachineMethod =
        typeof(StateMachineBuilder)
            .GetMethods( BindingFlags.Public | BindingFlags.Static )
            .First( x => x.Name == nameof(Create) && x.IsGenericMethod );

    public static Expression Create( BlockExpression source, Type resultType, bool createRunner )
    {
        var buildStateMachine = BuildStateMachineMethod.MakeGenericMethod( resultType );
        return (Expression) buildStateMachine.Invoke( null, [source, createRunner] );
    }

    public static Expression Create<TResult>( BlockExpression source, bool createRunner = true )
    {
        var assemblyName = new AssemblyName( "DynamicStateMachineAssembly" );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var moduleBuilder = assemblyBuilder.DefineDynamicModule( "MainModule" );

        var stateMachineBuilder = new StateMachineBuilder<TResult>( moduleBuilder, "DynamicStateMachine" );
        stateMachineBuilder.SetSource( source );
        var stateMachineExpression = stateMachineBuilder.CreateStateMachine( createRunner );

        return stateMachineExpression;
    }
}
