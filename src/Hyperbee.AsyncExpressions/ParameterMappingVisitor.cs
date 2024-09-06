using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions;

public class ParameterMappingVisitor( Expression instance, List<FieldBuilder> fields ) : ExpressionVisitor
{
    private readonly Dictionary<string, MemberExpression> _mappingCache = [];

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( node.Name == null )
            return base.VisitParameter( node );

        if ( _mappingCache.TryGetValue( node.Name, out var fieldAccess ) )
            return fieldAccess;

        if ( !TryGetFieldInfo( node.Name, out var fieldInfo ) )
            return base.VisitParameter( node );

        var fieldExpression = Expression.Field( instance, fieldInfo );
        _mappingCache.Add( node.Name, fieldExpression );

        return fieldExpression;

        bool TryGetFieldInfo( string name, out FieldInfo field )
        {
            var builderField = fields.FirstOrDefault( f => f.Name == $"_{name}" );
            if ( builderField != null )
            {
                field = instance.Type.GetField( builderField.Name, BindingFlags.Instance | BindingFlags.Public )!;
                return true;
            }

            field = null;
            return false;
        }
    }
}
