using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class FieldResolverVisitor( 
    Expression instance, 
    List<FieldBuilder> fields, 
    LabelTarget returnLabel, 
    MemberExpression stateIdField,
    MemberExpression stateMachineBuilderField ) : ExpressionVisitor
{
    private readonly Dictionary<Expression, MemberExpression> _mappingCache = [];
    private readonly string[] _fieldNames = fields.Select( x => x.Name ).ToArray();

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _mappingCache.TryGetValue( node, out var fieldAccess ) )
            return fieldAccess;

        if ( !TryGetFieldInfo( node, out var fieldInfo ) )
            return node;

        var fieldExpression = Expression.Field( instance, fieldInfo );
        _mappingCache.Add( node, fieldExpression );

        return fieldExpression;

        bool TryGetFieldInfo( ParameterExpression parameterExpression, out FieldInfo field )
        {
            var name = $"{parameterExpression.Name ?? parameterExpression.ToString()}";

            var builderField = fields.FirstOrDefault( f => f.Name == name );
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
        // Update each expression in a block to use only state machine fields/variables
        return node.Update(
            node.Variables.Where( v => !_fieldNames.Contains( v.Name ) ),
            node.Expressions.Select( Visit ) );
    }
    
    protected override Expression VisitExtension( Expression node )
    {
        switch (node)
        {
            case AwaitCompletionExpression awaitCompletionExpression:
                // TODO: clean up how we initialize the await completion expression
                awaitCompletionExpression.Initialize( instance, fields, returnLabel, stateIdField, stateMachineBuilderField );
                return awaitCompletionExpression.Reduce();
            case AwaitExpression awaitExpression:
                return Visit( awaitExpression.Target )!;
            default:
                return base.VisitExtension( node );
        }
    }
}
