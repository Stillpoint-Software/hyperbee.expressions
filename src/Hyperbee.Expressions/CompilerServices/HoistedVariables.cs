using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Collections;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

// Support for hoisting lowered local variables onto state-machine fields.
//
// Distinct ParameterExpression instances may share the same name, and a name may be
// null. Names can never identify a variable, so field names are uniquified and variables
// are tracked by instance.

internal static class HoistedVariables
{
    // Define one field per hoisted variable, returning the generated field names.

    public static Dictionary<ParameterExpression, string> DefineFields(
        TypeBuilder typeBuilder,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables,
        params string[] reservedNames )
    {
        var fieldNames = new Dictionary<ParameterExpression, string>();
        var usedNames = new HashSet<string>( reservedNames, StringComparer.Ordinal );

        foreach ( var (_, variable) in scopedVariables.EnumerateItems( LinkedNode.Current ) )
        {
            if ( fieldNames.ContainsKey( variable ) )
                continue;

            var fieldName = UniqueName( variable, usedNames );

            fieldNames[variable] = fieldName;

            typeBuilder.DefineField( fieldName, variable.Type, FieldAttributes.Public );
        }

        return fieldNames;

        static string UniqueName( ParameterExpression variable, HashSet<string> usedNames )
        {
            var baseName = string.IsNullOrEmpty( variable.Name ) ? "__var<>" : variable.Name;

            if ( usedNames.Add( baseName ) )
                return baseName;

            for ( var index = 1; ; index++ )
            {
                var name = $"{baseName}#{index}";

                if ( usedNames.Add( name ) )
                    return name;
            }
        }
    }

    // Bind the generated field names to the fields of the created type.

    public static Dictionary<ParameterExpression, FieldInfo> MapFields(
        Dictionary<ParameterExpression, string> fieldNames,
        FieldInfo[] fields )
    {
        var fieldsByName = fields.ToDictionary( field => field.Name, StringComparer.Ordinal );
        var variableFields = new Dictionary<ParameterExpression, FieldInfo>( fieldNames.Count );

        foreach ( var (variable, fieldName) in fieldNames )
        {
            variableFields[variable] = fieldsByName[fieldName];
        }

        return variableFields;
    }
}

// Rewrites hoisted variable references to state-machine field accesses.
//
// A variable is matched by instance, never by name. Declaration sites that re-declare a
// hoisted variable (blocks, catch blocks, lambdas, and nested coroutine blocks) introduce
// a new scope, so the variable is shadowed for the duration of that subtree.

internal sealed class HoistingVisitor : ExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, MemberExpression> _fieldMembers;
    private readonly List<ParameterExpression> _shadowed = [];

    public HoistingVisitor( ParameterExpression stateMachine, IReadOnlyDictionary<ParameterExpression, FieldInfo> variableFields )
    {
        _fieldMembers = variableFields.ToDictionary(
            variableField => variableField.Key,
            variableField => Field( stateMachine, variableField.Value )
        );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _shadowed.Contains( node ) )
            return node;

        return _fieldMembers.TryGetValue( node, out var fieldAccess )
            ? fieldAccess
            : node;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var count = Shadow( node.Variables );

        try
        {
            return base.VisitBlock( node );
        }
        finally
        {
            Unshadow( count );
        }
    }

    protected override CatchBlock VisitCatchBlock( CatchBlock node )
    {
        var count = node.Variable != null ? Shadow( [node.Variable] ) : 0;

        try
        {
            return base.VisitCatchBlock( node );
        }
        finally
        {
            Unshadow( count );
        }
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        var count = Shadow( node.Parameters );

        try
        {
            return base.VisitLambda( node );
        }
        finally
        {
            Unshadow( count );
        }
    }

    protected override Expression VisitExtension( Expression node )
    {
        var variables = node switch
        {
            AsyncBlockExpression asyncBlock => asyncBlock.Variables,
            EnumerableBlockExpression enumerableBlock => enumerableBlock.Variables,
            _ => null
        };

        if ( variables == null )
            return base.VisitExtension( node );

        var count = Shadow( variables );

        try
        {
            return base.VisitExtension( node );
        }
        finally
        {
            Unshadow( count );
        }
    }

    private int Shadow( IReadOnlyList<ParameterExpression> variables )
    {
        var count = 0;

        for ( var index = 0; index < variables.Count; index++ )
        {
            var variable = variables[index];

            if ( !_fieldMembers.ContainsKey( variable ) )
                continue;

            _shadowed.Add( variable );
            count++;
        }

        return count;
    }

    private void Unshadow( int count )
    {
        if ( count > 0 )
            _shadowed.RemoveRange( _shadowed.Count - count, count );
    }
}
