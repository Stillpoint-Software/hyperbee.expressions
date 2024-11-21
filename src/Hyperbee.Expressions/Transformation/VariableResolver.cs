using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

public interface IVariableResolver
{
    Expression Resolve( Expression node );

    Expression GetResultVariable( Expression node, int stateId );
    Expression GetAwaiterVariable( Type type, int stateId );
    Expression GetTryVariable( int stateId );
    Expression GetExceptionVariable( int stateId );
    ParameterExpression GetReturnVariable( Type type );

    IReadOnlyCollection<Expression> GetLocalVariables();

    void AddLocalVariables( IEnumerable<ParameterExpression> variables );
}

internal sealed class VariableResolver : ExpressionVisitor, IVariableResolver
{
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

    private const int InitialCapacity = 8;

    private readonly Dictionary<ParameterExpression, ParameterExpression> _mappedVariables = new( InitialCapacity );
    private readonly HashSet<ParameterExpression> _variables;
    private readonly StateContext _states;

    private int _variableId = 0;

    public VariableResolver( ParameterExpression[] variables, StateContext states )
    {
        _states = states;
        _variables = [.. variables];
    }

    // Helpers

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof( void ) )
            return null;

        return AddVariable( Expression.Parameter( node.Type, VariableName.Result( stateId, ref _variableId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetAwaiterVariable( Type type, int stateId )
    {
        return AddVariable( Expression.Variable( type, VariableName.Awaiter( stateId, ref _variableId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetTryVariable( int stateId )
    {
        return AddVariable( Expression.Variable( typeof( int ), VariableName.Try( stateId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetExceptionVariable( int stateId )
    {
        return AddVariable( Expression.Variable( typeof( object ), VariableName.Exception( stateId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public ParameterExpression GetReturnVariable( Type type )
    {
        return AddVariable( Expression.Variable( type, VariableName.Return ) );
    }

    // Resolving Visitor

    public Expression Resolve( Expression node )
    {
        return Visit( node );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return TryAddVariable( node, CreateParameter, out var updatedVariable )
            ? updatedVariable
            : base.VisitParameter( node );

        ParameterExpression CreateParameter( ParameterExpression n )
        {
            return Expression.Parameter( n.Type, VariableName.Variable( n.Name, _states.TailState.StateId, ref _variableId ) );
        }
    }


    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public IReadOnlyCollection<Expression> GetLocalVariables()
    {
        return _mappedVariables.Values;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddLocalVariables( IEnumerable<ParameterExpression> variables )
    {
        foreach ( var variable in variables )
        {
            _variables.Add( variable );
        }
    }

    // helpers

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private ParameterExpression AddVariable( ParameterExpression variable )
    {
        if ( _mappedVariables.TryGetValue( variable, out var existingVariable ) )
            return existingVariable;

        _mappedVariables[variable] = variable;
        return variable;
    }

    private bool TryAddVariable( ParameterExpression parameter, Func<ParameterExpression, ParameterExpression> createParameter, out ParameterExpression updatedParameterExpression )
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
