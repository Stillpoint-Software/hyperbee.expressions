using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class HoistingVisitor( IVariableResolver variableResolver ) : ExpressionVisitor
{
    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( variableResolver.TryGetFieldMember( node, out var fieldAccess ) )
            return fieldAccess;

        return node;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Update each expression in a block to use only state machine fields/variables
        return node.Update(
            variableResolver.ExcludeFieldMembers( node.Variables ),
            node.Expressions.Select( Visit )
        );
    }
}
