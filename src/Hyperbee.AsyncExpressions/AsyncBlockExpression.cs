using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression: Expression
{
    private readonly Expression[] _expressions;
    private readonly ParameterExpression[] _variables;
    private readonly Type _resultType;

    private bool _isReduced;
    private Expression _stateMachine;

    public AsyncBlockExpression( Expression[] expressions )
        : this( [], expressions )
    {
    }

    public AsyncBlockExpression( ParameterExpression[] variables, Expression[] expressions )
    {
        if ( expressions == null || expressions.Length == 0 )
        {
            throw new ArgumentException( $"{nameof(AsyncBlockExpression)} must contain at least one expression.", nameof(expressions) );
        }

        _variables = variables;
        _expressions = expressions;
        _resultType = GetResultType();
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type
    {
        get
        {
            if ( !_isReduced )
                Reduce();

            return _stateMachine.Type;
        }
    }

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _stateMachine;

        _stateMachine = Transform( _resultType, _variables, _expressions );
        _isReduced = true;

        return _stateMachine;
    }

    private static Expression Transform( Type resultType, ParameterExpression[] variables, Expression[] expressions )
    {
        var transformer = new GotoTransformerVisitor();
        var source = transformer.Transform( variables, expressions );

        if ( source.AwaitCount == 0 )
            throw new InvalidOperationException( $"{nameof(AsyncBlockExpression)} must contain at least one await." );

        return StateMachineBuilder.Create( resultType, source );
    }

    internal Type GetResultType()
    {
        var type = _expressions[^1].Type;

        if ( typeof(Task).IsAssignableFrom( type ) )
        {
            return type.IsGenericType
                ? type.GetGenericArguments()[0]
                : typeof( void ); // Task without a result
        }

        return type;
    }

    private class AsyncBlockExpressionDebuggerProxy( AsyncBlockExpression node )
    {
        public Expression StateMachine => node._stateMachine;
        public bool IsReduced => node._isReduced;
        public Type ReturnType => node._resultType;

        public Expression[] Expressions => node._expressions;
        public ParameterExpression[] Variables => node._variables;
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

