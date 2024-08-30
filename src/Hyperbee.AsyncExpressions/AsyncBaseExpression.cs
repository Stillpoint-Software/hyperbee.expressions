using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_body}" )]
[DebuggerTypeProxy( typeof( AsyncBaseExpressionProxy ) )]
public abstract class AsyncBaseExpression : Expression
{
    private readonly Expression _body;
    private Expression _reducedBody;
    private bool _isReduced;

    private static int __stateMachineCounter;
    private static readonly Expression TaskVoidResult = Constant( Task.FromResult( new VoidResult() ) );

    private static MethodInfo GenerateExecuteAsyncMethod => typeof( AsyncBaseExpression )
        .GetMethod( nameof( GenerateExecuteAsyncExpression ), BindingFlags.Static | BindingFlags.NonPublic );

    internal AsyncBaseExpression( Expression body )
    {
        ArgumentNullException.ThrowIfNull( body, nameof( body ) );

        if ( !IsTask( body.Type ) )
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

        _reducedBody = _body.Type.IsGenericType switch
        {
            true => GetReducedBody( _body.Type.GetGenericArguments()[0], _body ),
            false => GetReducedBody( typeof( VoidResult ), Block( _body, TaskVoidResult ) )
        };

        return _reducedBody;

        static Expression GetReducedBody( Type type, Expression body )
        {
            var methodInfo = GenerateExecuteAsyncMethod.MakeGenericMethod( type );
            return (Expression) methodInfo!.Invoke( null, [body] );
        }
    }

    private static BlockExpression GenerateExecuteAsyncExpression<T>( Expression task )
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
        var stateMachineVar = Variable( typeof( StateMachine<T> ), $"stateMachine_{id}" );

        // Constructor for state machine
        var stateMachineCtor = typeof( StateMachine<T> )
            .GetConstructor( [typeof( Task<T> )] );

        var assignStateMachine = Assign(
            stateMachineVar,
            New( stateMachineCtor!, task )
        );

        // Call MoveNext
        var moveNextMethod = typeof( StateMachine<T> ).GetMethod( nameof( StateMachine<T>.MoveNext ) );
        var moveNextCall = Call( stateMachineVar, moveNextMethod! );

        // Return task property
        var taskProperty = typeof( StateMachine<T> ).GetProperty( nameof( StateMachine<T>.Task ) );
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

    private struct StateMachine<T> : IAsyncStateMachine
    {
        private readonly Task<T> _task;
        private AsyncTaskMethodBuilder<T> _builder;
        private int _state;
        private ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter _awaiter;

        // ReSharper disable once UnusedMember.Local
        public StateMachine( Task<T> task )
        {
            _builder = AsyncTaskMethodBuilder<T>.Create();
            _state = -1;
            _task = task;
            SetStateMachine( this );
        }

        public Task<T> Task => _builder.Task;

        public void MoveNext()
        {
            try
            {
                ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter awaiter;

                if ( _state != 0 )
                {
                    // Initial state:
                    awaiter = _task.ConfigureAwait( false ).GetAwaiter();

                    if ( !awaiter.IsCompleted )
                    {
                        _state = 0;
                        _awaiter = awaiter;

                        // Schedule a continuation
                        _builder.AwaitUnsafeOnCompleted( ref awaiter, ref this );
                        return;
                    }
                }
                else
                {
                    // Continuation state: completed
                    awaiter = _awaiter;
                    _awaiter = default;
                    _state = -1;
                }

                // Final state: success
                var result = awaiter.GetResult();
                _state = -2;
                _builder.SetResult( result );
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

    internal static bool IsTask( Type returnType )
    {
        return returnType == typeof( Task ) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof( Task<> )) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof( ValueTask<> ));
    }

    public class AsyncBaseExpressionProxy( AsyncBaseExpression node )
    {
        public Expression Body => node._body;
    }
}
