using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class FieldResolverVisitor : ExpressionVisitor
{
    private readonly Dictionary<Expression, MemberExpression> _mappingCache = [];
    private readonly string[] _fieldNames;
    private readonly List<FieldBuilder> _fields;
    private readonly Expression _stateMachine;
    private readonly LabelTarget _returnLabel;
    private readonly MemberExpression _stateIdField;
    private readonly MemberExpression _builderField;


    public FieldResolverVisitor(Expression stateMachine, 
        List<FieldBuilder> fields, 
        LabelTarget returnLabel, 
        MemberExpression stateIdField,
        MemberExpression builderField)
    {
        _stateMachine = stateMachine;
        _returnLabel = returnLabel;
        _stateIdField = stateIdField;
        _builderField = builderField;
        _fields = fields;
        
        _fieldNames = fields.Select( x => x.Name ).ToArray();
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _mappingCache.TryGetValue( node, out var fieldAccess ) )
            return fieldAccess;

        if ( !TryGetFieldInfo( node, out var fieldInfo ) )
            return node;

        var fieldExpression = Expression.Field( _stateMachine, fieldInfo );
        _mappingCache.Add( node, fieldExpression );

        return fieldExpression;
    }

    private bool TryGetFieldInfo( ParameterExpression parameterExpression, out FieldInfo field )
    {
        var name = $"{parameterExpression.Name ?? parameterExpression.ToString()}";

        var builderField = _fields.FirstOrDefault( f => f.Name == name );
        if ( builderField != null )
        {
            field = _stateMachine.Type.GetField( builderField.Name, BindingFlags.Instance | BindingFlags.Public )!;
            return true;
        }

        field = null;
        return false;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Update each expression in a block to use only state machine fields/variables
        return node.Update(
            node.Variables.Where( v => !_fieldNames.Contains( v.Name ) ),
            node.Expressions.Select( Visit ) 
        );
    }
    
    protected override Expression VisitExtension( Expression node )
    {
        switch (node)
        {
            case AwaitCompletionExpression awaitCompletionExpression:
                return awaitCompletionExpression.Reduce( (FieldResolverSource) this );

            case AwaitExpression awaitExpression:
                return Visit( awaitExpression.Target )!;
        }

        return base.VisitExtension( node );
    }

    public static explicit operator FieldResolverSource( FieldResolverVisitor visitor )
    {
        return new FieldResolverSource
        {
            StateMachine = visitor._stateMachine,
            Fields =visitor._fields,
            ReturnLabel = visitor._returnLabel,
            StateIdField = visitor._stateIdField,
            BuilderField = visitor._builderField
        };
    }
}
