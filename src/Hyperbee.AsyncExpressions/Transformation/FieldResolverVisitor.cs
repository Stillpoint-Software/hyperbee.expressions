using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class FieldResolverVisitor : ExpressionVisitor, IFieldResolverSource
{
    private readonly Dictionary<string, MemberExpression> _mappingCache;

    public Type StateMachineType { get; init; }
    public Expression StateMachine { get; init; }
    public LabelTarget ReturnLabel { get; init; }
    public MemberExpression StateIdField { get; init; }
    public MemberExpression BuilderField { get; init; }
    public MemberExpression ResultField { get; init; }
    public ParameterExpression ReturnValue { get; init; }

    public FieldResolverVisitor(
        Type stateMachineType,
        Expression stateMachine, 
        MemberExpression[] fields,
        LabelTarget returnLabel, 
        MemberExpression stateIdField,
        MemberExpression builderField,
        MemberExpression resultField,
        ParameterExpression returnValue )
    {
        StateMachineType = stateMachineType;
        StateMachine = stateMachine;
        ReturnLabel = returnLabel;
        StateIdField = stateIdField;
        BuilderField = builderField;
        ResultField = resultField;
        ReturnValue = returnValue;

        _mappingCache = fields.ToDictionary( x => x.Member.Name );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        var name = node.Name ?? node.ToString();

        if ( !_mappingCache.TryGetValue( name, out var fieldAccess ) )
            return node;

        return fieldAccess;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Update each expression in a block to use only state machine fields/variables
        return node.Update(
            node.Variables.Where( x => !_mappingCache.ContainsKey( x.Name ?? x.ToString() ) ),
            node.Expressions.Select( Visit ) 
        );
    }

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AwaitExpression awaitExpression => Visit( awaitExpression.Target )!,
            NodeExpression stateNode => Visit( stateNode.Reduce( this ) ),
            _ => base.VisitExtension( node )
        } ?? Expression.Empty();
    }
}
