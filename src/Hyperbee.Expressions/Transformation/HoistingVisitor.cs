using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal class HoistingVisitor : ExpressionVisitor, IHoistingSource
{
    private readonly IVariableResolver _variableResolver;

    public ParameterExpression StateMachine { get; init; }

    public LabelTarget ExitLabel { get; init; }
    public MemberExpression StateIdField { get; init; }
    public MemberExpression BuilderField { get; init; }
    public MemberExpression ResultField { get; init; }
    public ParameterExpression ReturnValue { get; init; }

    public HoistingVisitor(
        ParameterExpression stateMachine,
        IVariableResolver variableResolver,
        MemberExpression stateIdField,
        MemberExpression builderField,
        MemberExpression resultField,
        LabelTarget exitLabel,
        ParameterExpression returnValue )
    {
        StateMachine = stateMachine;
        ExitLabel = exitLabel;
        StateIdField = stateIdField;
        BuilderField = builderField;
        ResultField = resultField;
        ReturnValue = returnValue;

        _variableResolver = variableResolver;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( !_variableResolver.TryGetValue( node, out var fieldAccess ) )
            return node;

        return fieldAccess;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Update each expression in a block to use only state machine fields/variables
        return node.Update(
            _variableResolver.ExcludeMemberVariables( node.Variables ),
            node.Expressions.Select( Visit )
        );
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is not NodeExpression nodeExpression )
            return base.VisitExtension( node );

        nodeExpression.SetResolverSource( this );
        return Visit( nodeExpression.Reduce() );

    }
}
