using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

public interface IVariableResolver
{
    IVariableResolver Parent { get; set; }

    IReadOnlyCollection<ParameterExpression> GetLocalVariables();
    void SetFieldMembers( IDictionary<string, MemberExpression> memberExpressions );
    bool TryGetFieldMember( ParameterExpression variable, out MemberExpression fieldAccess );
    bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out Expression updatedParameterExpression );
    ParameterExpression AddVariable( ParameterExpression variable );
    ParameterExpression AddLocalVariable( ParameterExpression variable );

    IEnumerable<ParameterExpression> ExcludeFieldMembers( IEnumerable<ParameterExpression> variables );

    bool TryFindVariableInHierarchy( ParameterExpression variable, out Expression updatedVariable );
}

internal sealed class VariableResolver : IVariableResolver
{
    private const int InitialCapacity = 8;

    private readonly Dictionary<ParameterExpression, ParameterExpression> _mappedVariables = new( InitialCapacity );
    private readonly HashSet<ParameterExpression> _variables;
    private IDictionary<string, MemberExpression> _memberExpressions;

    public IVariableResolver Parent { get; set; }

    public VariableResolver()
    {
        _variables = [];
    }

    public VariableResolver( ParameterExpression[] variables )
    {
        _variables = [.. variables];
    }

    public VariableResolver( ReadOnlyCollection<ParameterExpression> variables )
    {
        _variables = [.. variables];
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetFieldMembers( IDictionary<string, MemberExpression> memberExpressions )
    {
        _memberExpressions = memberExpressions;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public IReadOnlyCollection<ParameterExpression> GetLocalVariables()
    {
        return _mappedVariables.Values;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public ParameterExpression AddVariable( ParameterExpression variable )
    {
        if ( _mappedVariables.TryGetValue( variable, out var existingVariable ) )
            return existingVariable;

        _mappedVariables[variable] = variable;
        return variable;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public ParameterExpression AddLocalVariable( ParameterExpression variable )
    {
        _variables.Add( variable );
        return variable;
    }

    public bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out Expression updatedParameterExpression )
    {
        if ( TryFindVariableInHierarchy( parameter, out updatedParameterExpression ) )
            return true;

        if ( _variables == null || !_variables.Contains( parameter ) )
            return false;

        if ( _mappedVariables.TryGetValue( parameter, out var mappedVariable ) )
        {
            updatedParameterExpression = mappedVariable;
            return true;
        }

        var updated = createParameter( parameter );
        _mappedVariables[parameter] = updated;
        updatedParameterExpression = updated;

        return true;
    }

    public bool TryGetFieldMember( ParameterExpression variable, out MemberExpression fieldAccess )
    {
        fieldAccess = null;

        if ( _memberExpressions == null )
            return false;

        var name = variable.Name ?? variable.ToString();
        return _memberExpressions.TryGetValue( name, out fieldAccess );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public IEnumerable<ParameterExpression> ExcludeFieldMembers( IEnumerable<ParameterExpression> variables )
    {
        return variables.Where( variable => _memberExpressions == null || !_memberExpressions.ContainsKey( variable.Name ?? variable.ToString() ) );
    }

    public bool TryFindVariableInHierarchy( ParameterExpression variable, out Expression updatedVariable )
    {
        // Check current resolver for mapped variable
        if ( _mappedVariables.TryGetValue( variable, out var updated ) )
            variable = updated;

        // Check current resolver for member expression
        if ( TryGetFieldMember( variable, out var expression ) )
        {
            updatedVariable = expression;
            return true;
        }

        // Check parent resolver for variable
        if ( Parent != null )
            return Parent.TryFindVariableInHierarchy( variable, out updatedVariable );

        updatedVariable = null;
        return false;
    }
}
