using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class HoistingVisitor( IDictionary<string, MemberExpression> memberExpressions ) : ExpressionVisitor
{
    protected override Expression VisitParameter( ParameterExpression node )
    {
        var name = node.Name ?? node.ToString();

        if ( memberExpressions.TryGetValue( name, out var fieldAccess ) )
            return fieldAccess;

        return node;
    }
}
