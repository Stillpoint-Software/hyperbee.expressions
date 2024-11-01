using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation;

namespace Hyperbee.Expressions;


[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression : Expression
{
    public IVariableResolver VariableResolver { get; set; }
    public Expression[] Expressions { get; set; }
    public ParameterExpression[] Variables { get; set; }

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

        VariableResolver = new VariableResolver( variables );

        Variables = variables;
        Expressions = expressions;
        _resultType = Expressions[^1].Type;
        _type = _resultType == typeof( void ) ? typeof( Task ) : typeof( Task<> ).MakeGenericType( _resultType );
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    // ReSharper disable once ConvertToAutoProperty
    public override Type Type => _type;

    public override Expression Reduce()
    {
        if ( _stateMachine != null )
            return _stateMachine;

        var visitor = new LoweringVisitor();
        var source = visitor.Transform( VariableResolver, Expressions );

        _stateMachine = GenerateStateMachine( _resultType, source, VariableResolver );

        return _stateMachine;
    }

    private static Expression GenerateStateMachine( 
        Type resultType, 
        LoweringResult source,
        IVariableResolver variableResolver,
        bool createRunner = true )
    {
        if ( source.AwaitCount == 0 )
            throw new InvalidOperationException( $"{nameof( AsyncBlockExpression )} must contain at least one await." );

        var stateMachine = StateMachineBuilder.Create( resultType, source, variableResolver, createRunner);

        return stateMachine;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var variables = Array.AsReadOnly( Variables );
        var expressions = Array.AsReadOnly( Expressions );

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

        public Expression[] Expressions => node.Expressions;
        public ParameterExpression[] Variables => node.Variables;
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
