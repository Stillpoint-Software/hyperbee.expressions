using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation;

namespace Hyperbee.Expressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression : Expression
{
    private readonly Expression[] _expressions;
    private readonly ParameterExpression[] _variables;
    private readonly Type _resultType;
    private readonly Type _type;

    private Expression _stateMachine;

    public AsyncBlockExpression( Expression[] expressions )
        : this( [], expressions )
    {
    }

    public AsyncBlockExpression( ParameterExpression[] variables, Expression[] expressions )
    {
        if ( expressions == null || expressions.Length == 0 )
            throw new ArgumentException( $"{nameof( AsyncBlockExpression )} must contain at least one expression.", nameof( expressions ) );

        _variables = variables;
        _expressions = expressions;
        _resultType = _expressions[^1].Type;
        _type = _resultType == typeof( void ) ? typeof( Task ) : typeof( Task<> ).MakeGenericType( _resultType );
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    // ReSharper disable once ConvertToAutoProperty
    public override Type Type => _type;

    public override Expression Reduce()
    {
        return _stateMachine ??= GenerateStateMachine( _resultType, _variables, _expressions );
    }

    private static Expression GenerateStateMachine( Type resultType, ParameterExpression[] variables, Expression[] expressions )
    {
        var visitor = new LoweringVisitor();
        var source = visitor.Transform( variables, expressions );

        if ( source.AwaitCount == 0 )
            throw new InvalidOperationException( $"{nameof(AsyncBlockExpression)} must contain at least one await." );

        return StateMachineBuilder.Create( resultType, source );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var variables = Array.AsReadOnly( _variables );
        var expressions = Array.AsReadOnly( _expressions );

        var newVariables = visitor.VisitAndConvert( variables, nameof( VisitChildren ) );
        var newExpressions = visitor.Visit( expressions );

        if ( newVariables == variables && newExpressions == expressions )
            return this;

        return new AsyncBlockExpression( newVariables.ToArray(), newExpressions.ToArray() );
    }

    private class AsyncBlockExpressionDebuggerProxy( AsyncBlockExpression node )
    {
        public Expression StateMachine => node._stateMachine;
        public Type ReturnType => node._resultType;

        public Expression[] Expressions => node._expressions;
        public ParameterExpression[] Variables => node._variables;
    }
}

public static partial class ExpressionExtensions
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

