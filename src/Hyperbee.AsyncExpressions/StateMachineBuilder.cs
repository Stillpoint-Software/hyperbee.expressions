using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

public class StateMachineBuilder<TResult>
{
    private readonly List<Expression> _blocks = [];
    private readonly List<ParameterExpression> _stateVariables = [];
    private readonly ParameterExpression _state = Expression.Variable( typeof(int), "state" );
    private readonly ParameterExpression _builder = Expression.Variable( typeof(AsyncTaskMethodBuilder<TResult>), "builder" );

    public StateMachineBuilder()
    {
        _stateVariables.Add( _state );
        _stateVariables.Add( _builder );
    }

    public StateMachineBuilder<TResult> AddBlock( Expression blockExpression )
    {
        _blocks.Add( blockExpression );
        return this;
    }

    public StateMachineBuilder<TResult> AddTaskBlock( Expression taskExpression )
    {
        // Create the necessary awaiter and assign it to a variable
        var awaiterType = typeof(TaskAwaiter);
        var awaiter = Expression.Variable( awaiterType, "awaiter" );
        _stateVariables.Add( awaiter );

        // Generate the block that awaits the task
        var assignAwaiter = Expression.Assign( awaiter, Expression.Call( Expression.Convert( taskExpression, typeof(Task) ), nameof(Task.GetAwaiter), null ) );
        var isCompleted = Expression.Property( awaiter, nameof(TaskAwaiter.IsCompleted) );

        var setState = Expression.Assign( _state, Expression.Constant( _blocks.Count ) );

        var onCompleted = Expression.Call(
            _builder,
            nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
            [awaiter.Type, typeof(StateMachineBuilder<TResult>)],
            awaiter,
            Expression.Constant( this )
        );

        var block = Expression.Block(
            assignAwaiter,
            Expression.IfThenElse(
                isCompleted,
                Expression.Empty(),
                Expression.Block( setState, onCompleted, Expression.Return( Expression.Label( typeof(void) ) ) )
            )
        );

        _blocks.Add( block );
        return this;
    }

    public StateMachineBuilder<TResult> AddTaskResultBlock( Expression taskExpression )
    {
        // Determine the result type of the task
        var resultType = taskExpression.Type.GetGenericArguments()[0];
        var awaiterType = typeof(TaskAwaiter<>).MakeGenericType( resultType );
        var awaiter = Expression.Variable( awaiterType, "awaiter" );
        _stateVariables.Add( awaiter );

        // Generate the block that awaits the task result
        var assignAwaiter = Expression.Assign( awaiter, Expression.Call( Expression.Convert( taskExpression, typeof(Task<>).MakeGenericType( resultType ) ), nameof(Task<TResult>.GetAwaiter), null ) );
        var isCompleted = Expression.Property( awaiter, nameof(TaskAwaiter<TResult>.IsCompleted) );

        var setState = Expression.Assign( _state, Expression.Constant( _blocks.Count ) );
        var onCompleted = Expression.Call(
            _builder,
            nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
            [awaiter.Type, typeof(StateMachineBuilder<TResult>)],
            awaiter,
            Expression.Constant( this )
        );

        var block = Expression.Block(
            assignAwaiter,
            Expression.IfThenElse(
                isCompleted,
                Expression.Empty(),
                Expression.Block( setState, onCompleted, Expression.Return( Expression.Label( typeof(void) ) ) )
            )
        );

        _blocks.Add( block );
        return this;
    }

    public Expression<Func<Task<TResult>>> Build()
    {
        // Final result variable to hold the outcome of the last block
        var finalResult = Expression.Variable( typeof(TResult), "finalResult" );
        _stateVariables.Add( finalResult );

        // Generate the state machine body
        var body = new List<Expression>
        {
            // Add the initial state of the builder
            Expression.Assign( _builder, Expression.Call( typeof(AsyncTaskMethodBuilder<TResult>), nameof(AsyncTaskMethodBuilder<TResult>.Create), null ) )
        };

        // Add each state machine block
        for ( var i = 0; i < _blocks.Count; i++ )
        {
            var block = _blocks[i];

            // Check the current state
            var condition = Expression.Equal( _state, Expression.Constant( i ) );
            var ifStateMatches = Expression.IfThen( condition, block );

            body.Add( ifStateMatches );
        }

        // Set the final result and return
        body.Add( Expression.Assign( finalResult, Expression.Default( typeof(TResult) ) ) );
        body.Add( Expression.Call( _builder, nameof(AsyncTaskMethodBuilder<TResult>.SetResult), null, finalResult ) );

        var stateMachineBody = Expression.Block( _stateVariables, body );

        // Return the lambda expression representing the state machine
        return Expression.Lambda<Func<Task<TResult>>>( stateMachineBody, [] );
    }
}
