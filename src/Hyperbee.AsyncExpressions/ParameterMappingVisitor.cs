using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions;

// TODO: This should not use a dictionary, but a list/hash of node instances
public class ParameterMappingVisitor( Expression instance, List<FieldBuilder> fields ) : ExpressionVisitor
{
    private readonly Dictionary<string, MemberExpression> _mappingCache = [];
    private readonly string[] _fieldNames = fields.Select( x => x.Name ).ToArray();
    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( node.Name == null )
            return node;

        if ( _mappingCache.TryGetValue( node.Name, out var fieldAccess ) )
            return fieldAccess;

        if ( !TryGetFieldInfo( node.Name, out var fieldInfo ) )
            return node;

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

    protected override Expression VisitBlock( BlockExpression node )
    {
        // TODO: Review with BF
        // Update each expression in a block to use only state machine fields/variables
        return node.Update( 
            node.Variables.Where( v => !_fieldNames.Contains(v.Name) ), 
            node.Expressions.Select( Visit ) );
    }
}
