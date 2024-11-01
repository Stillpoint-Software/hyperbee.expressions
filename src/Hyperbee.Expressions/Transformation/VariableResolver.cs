using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

public interface IVariableResolver
{
    IVariableResolver Parent { get; set; }

    IReadOnlyCollection<ParameterExpression> GetLocalVariables();
    void SetFieldMembers( IDictionary<string, MemberExpression> memberExpressions );
    bool TryGetValue( ParameterExpression variable, out MemberExpression fieldAccess );
    bool Contains( ParameterExpression variable );
    bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out Expression updatedParameterExpression );
    ParameterExpression AddVariable( ParameterExpression variable );

    bool TryFindVariableInHierarchy( ParameterExpression variable, out Expression updatedVariable );
}

internal sealed class VariableResolver : IVariableResolver
{
    private const int InitialCapacity = 8;

    private readonly Dictionary<ParameterExpression, ParameterExpression> _mappedVariables = new( InitialCapacity );
    private readonly ParameterExpression[] _variables;
    private IDictionary<string, MemberExpression> _memberExpressions;

    public IVariableResolver Parent { get; set; }

    public VariableResolver( ParameterExpression[] variables )
    {
        _variables = variables;
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

    public bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out Expression updatedParameterExpression )
    {
        if ( TryFindVariableInHierarchy( parameter, out updatedParameterExpression ) )
            return true;

        if ( Array.IndexOf( _variables, parameter ) == -1 )
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

    public bool TryGetValue( ParameterExpression variable, out MemberExpression fieldAccess )
    {
        fieldAccess = null;

        if ( _memberExpressions == null )
            return false;

        var name = variable.Name ?? variable.ToString();
        return _memberExpressions.TryGetValue( name, out fieldAccess );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public bool Contains( ParameterExpression variable )
    {
        var name = variable.Name ?? variable.ToString();
        return _memberExpressions?.ContainsKey( name ) == true;
    }

    public bool TryFindVariableInHierarchy( ParameterExpression variable, out Expression updatedVariable )
    {
        // Check current resolver for mapped variable
        if ( _mappedVariables.TryGetValue( variable, out var updated ) )
            variable = updated;

        // Check current resolver for member expression
        if ( TryGetValue( variable, out var expression ) )
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
