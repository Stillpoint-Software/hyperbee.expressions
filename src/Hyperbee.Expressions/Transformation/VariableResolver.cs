using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public interface IFieldResolver
{
    void SetFieldMembers( IDictionary<string, MemberExpression> memberExpressions );
}

public interface IVariableResolver : IFieldResolver
{
    IVariableResolver Parent { get; set; }

    ParameterExpression[] GetLocalVariables();

    bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out Expression updatedParameterExpression );
    ParameterExpression AddVariable( ParameterExpression variable );
}

internal class VariableResolver( ParameterExpression[] variables ) : IVariableResolver
{
    private readonly Dictionary<ParameterExpression, ParameterExpression> _mappedVariables = new();
    private IDictionary<string, MemberExpression> _memberExpressions;

    public IVariableResolver Parent { get; set; }

    public void SetFieldMembers( IDictionary<string, MemberExpression> memberExpressions )
    {
        _memberExpressions = memberExpressions;
    }

    public bool TryGetUpdateVariable( ParameterExpression variable, out Expression updatedVariable )
    {
        if ( FindMemberExpression( variable, out updatedVariable ) )
            return true;

        if ( WalkParentVariables( variable, out updatedVariable ) )
            return true;

        updatedVariable = null;
        return false;
    }

    public ParameterExpression[] GetLocalVariables()
    {
        return _mappedVariables.Select( x => x.Value ).ToArray();
    }

    public ParameterExpression AddVariable( ParameterExpression variable )
    {
        if ( _mappedVariables.TryGetValue( variable, out var existingVariable ) )
            return existingVariable;

        _mappedVariables[variable] = variable;
        return variable;
    }

    public bool TryAddVariable(
        ParameterExpression parameter,
        Func<ParameterExpression, ParameterExpression> createParameter,
        out Expression updatedParameterExpression )
    {
        updatedParameterExpression = null;

        if ( TryGetUpdateVariable( parameter, out updatedParameterExpression ) )
            return true;

        if ( !variables.Contains( parameter ) )
            return false;

        if ( _mappedVariables.TryGetValue( parameter, out var variable ) )
        {
            updatedParameterExpression = variable;
            return true;
        }

        var updated = createParameter( parameter );

        _mappedVariables[parameter] = updated;

        updatedParameterExpression = updated;

        return true;
    }

    private bool FindMemberExpression( ParameterExpression variable, out Expression updatedVariable )
    {
        // map original variable to updated/renamed version
        if ( _mappedVariables.TryGetValue( variable, out var updated ) )
            variable = updated;

        if ( _memberExpressions?.TryGetValue( variable.Name ?? variable.ToString(), out var expression ) == true )
        {
            updatedVariable = expression;
            return true;
        }

        updatedVariable = null;
        return false;
    }

    private bool WalkParentVariables( ParameterExpression variable, out Expression updatedVariable )
    {
        var current = Parent as VariableResolver;

        while ( current != null )
        {
            if ( current.TryGetUpdateVariable( variable, out updatedVariable ) == true )
                return true;

            current = current.Parent as VariableResolver;
        }

        updatedVariable = null;
        return false;
    }

}
