using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;


internal static class VariableName
{
    // use special names to prevent collisions
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static string Awaiter( int stateId, ref int variableId ) => $"__awaiter<{stateId}_{variableId++}>";

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static string Result( int stateId, ref int variableId ) => $"__result<{stateId}_{variableId++}>";

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static string Try( int stateId ) => $"__try<{stateId}>";

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static string Exception( int stateId ) => $"__ex<{stateId}>";

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static string Variable( string name, int stateId, ref int variableId ) => $"__{name}<{stateId}_{variableId++}>";

    public const string Return = "return<>";
}

internal class VariableVisitor : ExpressionVisitor
{
    internal int VariableId = 0;


    public IVariableResolver VariableResolver { get; private set; }
    public StateContext States { get; private set; }

    public VariableVisitor( IVariableResolver variableResolver, StateContext states )
    {
        VariableResolver = variableResolver;
        States = states;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return VariableResolver.TryAddVariable( node, CreateParameter, out var updatedVariable )
            ? updatedVariable
            : base.VisitParameter( node );

        ParameterExpression CreateParameter( ParameterExpression n )
        {
            return Expression.Parameter( n.Type, VariableName.Variable( n.Name, States.TailState.StateId, ref VariableId ) );
        }
    }


    // Helpers

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal void AddLocalVariables( ReadOnlyCollection<ParameterExpression> variables )
    {
        VariableResolver.AddLocalVariables( variables );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal Expression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof( void ) )
            return null;

        return VariableResolver.AddVariable(
            Expression.Parameter( node.Type, VariableName.Result( stateId, ref VariableId ) )
        );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal ParameterExpression CreateVariable( Type type, string name )
    {
        return VariableResolver.AddVariable( Expression.Variable( type, name ) );
    }


}

public interface IVariableResolver
{
    IReadOnlyCollection<Expression> GetLocalVariables();

    bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out ParameterExpression updatedParameterExpression );
    ParameterExpression AddVariable( ParameterExpression variable );
    void AddLocalVariables( ReadOnlyCollection<ParameterExpression> variables );
}

internal sealed class VariableResolver : IVariableResolver
{
    private const int InitialCapacity = 8;

    private readonly Dictionary<ParameterExpression, ParameterExpression> _mappedVariables = new( InitialCapacity );
    private readonly HashSet<ParameterExpression> _variables;

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
    public IReadOnlyCollection<Expression> GetLocalVariables()
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
    public void AddLocalVariables( ReadOnlyCollection<ParameterExpression> variables )
    {
        for ( var i = 0; i < variables.Count; i++ )
        {
            _variables.Add( variables[i] );
        }
    }

    public bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out ParameterExpression updatedParameterExpression )
    {
        if ( _mappedVariables.TryGetValue( parameter, out var mappedVariable ) ) 
        { 
            updatedParameterExpression = mappedVariable;
            return true;
        }

        if ( _variables == null || !_variables.Contains( parameter ) ) 
        {
            updatedParameterExpression = null;
            return false;
        }

        updatedParameterExpression = createParameter( parameter );
        _mappedVariables[parameter] = updatedParameterExpression;

        return true;
    }

}
