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
}
