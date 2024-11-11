using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// MemberAccessVisitor: Constant Field and Property Access Simplification
//
// This visitor simplifies member access by precomputing values for constant fields or
// properties. When a field or property is accessed through a constant object, it evaluates
// the expression and replaces it with the value.
//
// Before:
//   .MemberAccess(.Constant(obj), Property)
//
// After:
//   .Constant(value)
//
public class MemberAccessVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        node = (MemberExpression) base.VisitMember( node );

        if ( node.Expression is not ConstantExpression constExpr )
        {
            return node;
        }

        switch ( node.Member )
        {
            case FieldInfo fieldInfo:
                var value = fieldInfo.GetValue( constExpr.Value );
                return Expression.Constant( value, node.Type );

            case PropertyInfo propertyInfo when propertyInfo.CanRead && propertyInfo.GetMethod!.IsStatic:
                var staticValue = propertyInfo.GetValue( null );
                return Expression.Constant( staticValue, node.Type );

            default:
                return node;
        }
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.NodeType != ExpressionType.Coalesce || node.Left is not ConstantExpression leftConst || leftConst.Value != null )
        {
            return base.VisitBinary( node );
        }

        return Visit( node.Right );
    }

    protected override Expression VisitIndex( IndexExpression node )
    {
        if ( node.Object is not ConstantExpression constantArray || constantArray.Value is not Array array || node.Arguments[0] is not ConstantExpression indexExpr )
        {
            return base.VisitIndex( node );
        }

        var index = (int) indexExpr.Value!;
        return Expression.Constant( array.GetValue( index ), node.Type );
    }
}
