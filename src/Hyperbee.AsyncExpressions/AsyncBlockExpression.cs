using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_body}" )]
[DebuggerTypeProxy( typeof( AsyncBlockExpressionProxy ) )]
public class AsyncBlockExpression : Expression
{
    private readonly Expression _body;
    private Expression _reducedBody;
    private bool _isReduced;
    private static int __stateMachineCounter;

    private static readonly Expression TaskVoidResult = Constant( Task.FromResult( new VoidResult() ) );

    private static MethodInfo MakeExecuteAsyncExpressionMethod => typeof( AsyncBaseExpression )
        .GetMethod( nameof( MakeExecuteAsyncExpression ), BindingFlags.Static | BindingFlags.NonPublic );

    internal AsyncBlockExpression( Expression body )
    {
        ArgumentNullException.ThrowIfNull( body, nameof( body ) );

        if ( !IsAsync( body.Type ) )
            throw new ArgumentException( $"The specified {nameof( body )} is not an async.", nameof( body ) );

        _body = body;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => _body.Type;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _reducedBody;

        _isReduced = true;

        var (type, result) = GetTypeResult( _body );
        var methodInfo = MakeExecuteAsyncExpressionMethod?.MakeGenericMethod( type );

        _reducedBody = (Expression) methodInfo!.Invoke( null, [result] );

        return _reducedBody!;
    }

    private static (Type Type, Expression Expression) GetTypeResult( Expression expression )
    {
        return expression.Type == typeof( Task )
            ? (typeof( VoidResult ), Block( expression, TaskVoidResult ))
            : (expression.Type.GetGenericArguments()[0], expression);
    }

    private static BlockExpression MakeExecuteAsyncExpression<T>( Expression task )
    {
        // Generating code block: 
        /*
        internal static Task<T> ExecuteAsync<T>(Task<T> task)
        {
           var stateMachine = new StateMachine<T>(task);
           stateMachine.MoveNext();
           return stateMachine.Task;
        }
        */

        // Create unique variable names to avoid conflicts
        var id = Interlocked.Increment( ref __stateMachineCounter );
        var stateMachineVar = Variable( typeof( MultiTaskStateMachine<T> ), $"stateMachine_{id}" );

        // Constructor for state machine
        var stateMachineCtor = typeof( MultiTaskStateMachine<T> )
            .GetConstructor( [typeof( Task<T> )] );

        var assignStateMachine = Assign(
            stateMachineVar,
            New( stateMachineCtor!, task )
        );

        // Call MoveNext
        var moveNextMethod = typeof( MultiTaskStateMachine<T> ).GetMethod( nameof( MultiTaskStateMachine<T>.MoveNext ) );
        var moveNextCall = Call( stateMachineVar, moveNextMethod! );

        // Return task property
        var taskProperty = typeof( MultiTaskStateMachine<T> ).GetProperty( nameof( MultiTaskStateMachine<T>.Task ) );
        var returnTask = Property( stateMachineVar, taskProperty! );

        // Explicitly use nested blocks to handle variable scoping
        var resultBlock = Block(
            [stateMachineVar],
            assignStateMachine,
            moveNextCall,
            returnTask
        );

        return resultBlock;
    }

    private struct MultiTaskStateMachine<T> : IAsyncStateMachine
    {
        private readonly Task[] _tasks;
        private readonly bool _isLastTaskGeneric;
        private AsyncTaskMethodBuilder<T> _builder;
        private int _state;

        public MultiTaskStateMachine( Task[] tasks )
        {
            _builder = AsyncTaskMethodBuilder<T>.Create();
            _state = -1;
            _tasks = tasks;

            // Determine if the last task is generic or not
            var lastTaskType = tasks[^1].GetType();
            _isLastTaskGeneric = lastTaskType.IsGenericType && lastTaskType.GetGenericTypeDefinition() == typeof( Task<> );

            SetStateMachine( this );
        }

        public Task<T> Task => _builder.Task;

        public void MoveNext()
        {
            try
            {
                if ( _state == -1 )
                {
                    // Initial state:
                    _state = 0;
                }

                if ( _state >= 0 && _state < _tasks.Length )
                {
                    var currentTask = _tasks[_state];

                    if ( _state == _tasks.Length - 1 && _isLastTaskGeneric )
                    {
                        // Last task is generic
                        var genericAwaiter = ((Task<T>) currentTask).ConfigureAwait( false ).GetAwaiter();
                        if ( !genericAwaiter.IsCompleted )
                        {
                            _builder.AwaitUnsafeOnCompleted( ref genericAwaiter, ref this );
                            return;
                        }

                        // Get the result directly if the task is already completed
                        var result = genericAwaiter.GetResult();
                        _state = -2;
                        _builder.SetResult( result );
                    }
                    else
                    {
                        // Intermediate non-generic task or last non-generic task
                        var awaiter = currentTask.ConfigureAwait( false ).GetAwaiter();
                        if ( !awaiter.IsCompleted )
                        {
                            _builder.AwaitUnsafeOnCompleted( ref awaiter, ref this );
                            return;
                        }

                        // Continue directly if the task is already completed
                        awaiter.GetResult();
                        _state++;
                        MoveNext();
                    }
                }
                else if ( _state == _tasks.Length && !_isLastTaskGeneric )
                {
                    // All tasks completed, last task was non-generic
                    _state = -2;
                    _builder.SetResult( default! );
                }
            }
            catch ( Exception ex )
            {
                // Final state: error
                _state = -2;
                _builder.SetException( ex );
            }
        }

        public void SetStateMachine( IAsyncStateMachine stateMachine )
        {
            _builder.SetStateMachine( stateMachine );
        }
    }

    private static bool IsAsync( Type returnType )
    {
        return returnType == typeof( Task ) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof( Task<> )) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof( ValueTask<> ));
    }

    public class AsyncBlockExpressionProxy( AsyncBlockExpression node )
    {
        public Expression Body => node._body;
    }

    public static AsyncBlockExpression BlockAsync( BlockExpression expression )
    {
        //expression.Expressions.Count..

        /*
        {
    
            var result1 = {
                [ex1Task]
                expression1,  //Task  Assign( ex1Task, expression1 )
                expression2,
                awaitExpression3 ( expression3 /// Expression ),
            },
        
            {
              [ex1Task, result1]
              await( ex1Task,void,T )
            }
    
            var result3 = {
                 [result2]
                 expression4,    
            }
          ...
        }
         */

        //var d = Task.Delay( 10 );
        // ...
        //await d;


        return new AsyncBlockExpression( expression );
    }

}
