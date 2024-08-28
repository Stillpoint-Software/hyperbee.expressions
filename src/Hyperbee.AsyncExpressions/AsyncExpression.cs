using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{_body}" )]
[DebuggerTypeProxy( typeof(AsyncExpressionProxy) )]
public class AsyncExpression : Expression
{
    private readonly Expression _body;
    private Expression _visitedBody;
    private bool _isVisited;

    internal AsyncExpression( Expression body )
    {
        ArgumentNullException.ThrowIfNull( body, nameof(body) );

        _body = body;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => _body.Type;

    internal static Task<T> ExecuteAsync<T>( Expression body, ParameterExpression[] parameterExpressions, params object[] parameters )
    {
        var builder = AsyncTaskMethodBuilder<T>.Create();
        var stateMachine = new StateMachine<T>( ref builder, body, parameterExpressions, parameters );
        stateMachine.MoveNext();
        return stateMachine.Task;
    }

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( !_isVisited )
        {
            _isVisited = true;
            _visitedBody = new AsyncVisitor( _body ).Visit();
        }

        return _visitedBody;
    }

    internal class AsyncVisitor : ExpressionVisitor
    {
        private readonly Expression _body;
        private readonly List<ParameterExpression> _visitedParameters = [];

        private static MethodInfo GenericExecuteAsync => typeof(AsyncExpression)
            .GetMethod( nameof(ExecuteAsync), BindingFlags.Static | BindingFlags.NonPublic );

        public AsyncVisitor( Expression body )
        {
            _body = body;
        }

        public Expression Visit()
        {
            return Visit( _body );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( !_visitedParameters.Contains( node ) )
                _visitedParameters.Add( node );

            return base.VisitParameter( node );
        }

        protected override Expression VisitMethodCall( MethodCallExpression node )
        {
            if ( TryProcessCallableNode( node, node.Arguments, out var result ) )
                return result;

            return base.VisitMethodCall( node );
        }

        protected override Expression VisitInvocation( InvocationExpression node )
        {
            if ( TryProcessCallableNode( node, node.Arguments, out var result ) )
                return result;

            return base.VisitInvocation( node );
        }

        // Helpers

        public bool TryProcessCallableNode( Expression node, IReadOnlyCollection<Expression> arguments, out Expression result )
        {
            // Visit each argument to process any nested parameters
            foreach ( var argument in arguments )
            {
                Visit( argument );
            }

            if ( node.Type == typeof(AsyncExpression) )
            {
                result = node;
                return true;
            }

            if ( typeof(Task).IsAssignableFrom( node.Type ) )
            {
                var executeMethod = MakeGenericExecuteAsyncMethod( node.Type.GetGenericArguments()[0] );

                // MethodCallExpression and InvocationExpression have different argument structures
                var argItems = node is MethodCallExpression ? (IEnumerable<Expression>) _visitedParameters : arguments; 
                var argArray = NewArrayInit( typeof(object), ConvertArguments( argItems ) );

                //return ExecuteAsyncExpression(node.Type.GetGenericArguments()[0], _body, VisitedParameters.ToArray(), arguments);
                result = Call( executeMethod!, [Constant( _body ), Constant( _visitedParameters.ToArray() ), argArray] );
                return true;
            }

            result = null;
            return false;
        }

        private static MethodInfo MakeGenericExecuteAsyncMethod( Type typeArg )
        {
            return GenericExecuteAsync!.MakeGenericMethod( typeArg );
        }

        private static Expression[] ConvertArguments( IEnumerable<Expression> expressions )
        {
            return expressions.Select( x => Convert( x, typeof(object) ) ).Cast<Expression>().ToArray();
        }
    }

    /*
    public static Expression ExecuteAsyncExpression(Type resultType, Expression body, ParameterExpression[] parameterExpressions, params Expression[] parameters)
    {
        var builderVar = Expression.Variable(typeof(AsyncTaskMethodBuilder<>).MakeGenericType(resultType), "builder_" + Guid.NewGuid().ToString("N"));
        var stateMachineVar = Expression.Variable(typeof(StateMachine<>).MakeGenericType(resultType), "stateMachine_" + Guid.NewGuid().ToString("N"));

        var builderCreateMethod = typeof(AsyncTaskMethodBuilder<>)
            .MakeGenericType(resultType)
            .GetMethod(nameof(AsyncTaskMethodBuilder<object>.Create));

        var assignBuilder = Expression.Assign(builderVar, Expression.Call(builderCreateMethod));

        var stateMachineCtor = typeof(StateMachine<>)
            .MakeGenericType(resultType)
            .GetConstructor(new[]
            {
                typeof(AsyncTaskMethodBuilder<>).MakeGenericType(resultType).MakeByRefType(),
                typeof(Expression),
                typeof(ParameterExpression[]),
                typeof(object[])
            });

        var assignStateMachine = Expression.Assign(
            stateMachineVar,
            Expression.New(stateMachineCtor, builderVar, Expression.Constant(body), Expression.Constant(parameterExpressions), Expression.NewArrayInit(typeof(object), parameters))
        );

        var moveNextMethod = typeof(StateMachine<>)
            .MakeGenericType(resultType)
            .GetMethod(nameof(StateMachine<object>.MoveNext));

        var moveNextCall = Expression.Call(stateMachineVar, moveNextMethod);

        var taskProperty = typeof(StateMachine<>)
            .MakeGenericType(resultType)
            .GetProperty(nameof(StateMachine<object>.Task));

        var returnTask = Expression.Property(stateMachineVar, taskProperty);

        // Ensure the block encapsulates all variable declarations and usage
        return Expression.Block(
            new[] { builderVar, stateMachineVar }, // Declaring the variables in the block scope
            assignBuilder,
            assignStateMachine,
            moveNextCall,
            returnTask
        );
    }*/


    private struct StateMachine<T> : IAsyncStateMachine
    {
        private AsyncTaskMethodBuilder<T> _builder;
        private readonly Expression _body;
        private int _state;
        private ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter _awaiter;
        private readonly object[] _parameters;
        private readonly ParameterExpression[] _parameterExpressions;

        public StateMachine( ref AsyncTaskMethodBuilder<T> builder, Expression body, ParameterExpression[] parameterExpressions, object[] parameters )
        {
            _builder = builder; // critical: this makes a copy of builder
            _body = body;
            _parameterExpressions = parameterExpressions;
            _parameters = parameters;
            _state = -1;

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
                    // Initial state: compile the expression and execute it

                    var delegateType = GetDelegateType( _parameterExpressions.Select( p => p.Type ).Concat( [typeof(Task<T>)] ).ToArray() );
                    var lambda = Lambda( delegateType, _body, _parameterExpressions );
                    var compiledLambda = lambda.Compile();
                    var task = compiledLambda.DynamicInvoke( _parameters );

                    awaiter = ((Task<T>) task!).ConfigureAwait( false ).GetAwaiter();

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

    private static bool IsAsync( Type returnType )
    {
        return returnType == typeof(Task) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) ||
               (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    public class AsyncExpressionProxy( AsyncExpression node )
    {
        public Expression Body => node._body;
    }

    public static AsyncExpression CallAsync( MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !IsAsync( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method is not an async.", nameof(methodInfo) );

        return new AsyncExpression( Call( methodInfo, arguments ) );
    }

    public static AsyncExpression CallAsync( Expression instance, MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !IsAsync( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method is not an async.", nameof(methodInfo) );

        return new AsyncExpression( Call( instance, methodInfo, arguments ) );
    }

    public static AsyncExpression LambdaAsync( LambdaExpression lambdaExpression, params Expression[] arguments )
    {
        if ( !IsAsync( lambdaExpression.ReturnType ) )
            throw new ArgumentException( "The specified lambda is not an async.", nameof(lambdaExpression) );

        return new AsyncExpression( Invoke( lambdaExpression, arguments ) );
    }

    public static AwaitExpression Await( AsyncExpression asyncExpression, bool configureAwait )
    {
        return new AwaitExpression( asyncExpression, configureAwait );
    }
}