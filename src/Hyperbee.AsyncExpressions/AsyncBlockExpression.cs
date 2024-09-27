using System.Diagnostics;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression: Expression
{
    private readonly Expression[] _expressions;
    private readonly ParameterExpression[] _initialVariables;

    private bool _isReduced;
    private Expression _stateMachine;
    private Type _resultType;

    public Expression[] Expressions => _expressions;
    public ParameterExpression[] InitialVariables => _initialVariables;

    public AsyncBlockExpression( Expression[] expressions )
        : this( [], expressions )
    {
    }

    public AsyncBlockExpression( ParameterExpression[] variables, Expression[] expressions )
    {
        if ( expressions == null || expressions.Length == 0 )
        {
            throw new ArgumentException( "AsyncBlockExpression must contain at least one expression.",
                nameof(expressions) );
        }

        _initialVariables = variables;
        _expressions = expressions;
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachine;

        // Restructure expressions so we can split them into awaitable blocks
        var transformer = new GotoTransformerVisitor();
        var transformResult = transformer.Transform( _initialVariables, _expressions );
        transformer.PrintStateMachine();

        _resultType = GetResultType();

        _stateMachine = StateMachineBuilder.Create( transformResult, _resultType, createRunner: true );
        _isReduced = true;

        return _stateMachine;
    }

    public override Type Type
    {
        get
        {
            if ( !_isReduced )
                Reduce();

            return _stateMachine.Type;
        }
    }

    internal Type GetResultType()
    {
        var lastExpr = _expressions[^1];

        if ( IsTask( lastExpr.Type ) )
        {
            return lastExpr.Type.IsGenericType
                ? lastExpr.Type.GetGenericArguments()[0]
                : typeof( void ); // Task without a result
        }

        return lastExpr.Type;

        static bool IsTask( Type type ) => typeof(Task).IsAssignableFrom( type );
    }

    private class AsyncBlockExpressionDebuggerProxy
    {
        private readonly AsyncBlockExpression _node;

        public AsyncBlockExpressionDebuggerProxy( AsyncBlockExpression node ) => _node = node;

        public Expression StateMachine => _node._stateMachine;
        public bool IsReduced => _node._isReduced;
        public Type ReturnType => _node._resultType;

        public Expression[] Expressions => _node._expressions;
        public ParameterExpression[] InitialVariables => _node._initialVariables;
    }
}

public static partial class AsyncExpression
{
    public static AsyncBlockExpression BlockAsync( params Expression[] expressions )
    {
        return new AsyncBlockExpression( expressions );
    }

    public static AsyncBlockExpression BlockAsync( ParameterExpression[] variables, params Expression[] expressions )
    {
        return new AsyncBlockExpression( variables, expressions );
    }
}

