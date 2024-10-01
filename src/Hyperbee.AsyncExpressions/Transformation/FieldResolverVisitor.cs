using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class FieldResolverVisitor : ExpressionVisitor, IFieldResolverSource
{
    private readonly Dictionary<string, MemberExpression> _mappingCache;

    public Expression StateMachine { get; init; }
    public MemberExpression[] Fields { get; init; }
    public LabelTarget ReturnLabel { get; init; }
    public MemberExpression StateIdField { get; init; }
    public MemberExpression BuilderField { get; init; }

    public FieldResolverVisitor(Expression stateMachine, 
        MemberExpression[] fields,
        LabelTarget returnLabel, 
        MemberExpression stateIdField,
        MemberExpression builderField)
    {
        StateMachine = stateMachine;
        ReturnLabel = returnLabel;
        StateIdField = stateIdField;
        BuilderField = builderField;

        Fields = fields;
        _mappingCache = fields.ToDictionary( x => x.Member.Name );
    }

    public Expression[] Visit( IEnumerable<Expression> nodes )
    {
        return nodes.Select( Visit ).ToArray();
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
            node.Variables.Where( v => !_mappingCache.ContainsKey( v.Name ?? v.ToString() ) ),
            node.Expressions.Select( Visit ) 
        );
    }

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AwaitCompletionExpression awaitCompletionExpression => awaitCompletionExpression.Reduce( this ),
            AwaitExpression awaitExpression => Visit( awaitExpression.Target )!,
            _ => base.VisitExtension( node )
        };
    }
}
