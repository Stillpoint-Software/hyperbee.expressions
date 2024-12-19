using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.CompilerServices.Collections;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

internal sealed class VariableResolver : ExpressionVisitor
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

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string ExternVariable( string name, int stateId, ref int variableId ) => $"__extern.{name}<{stateId}_{variableId++}>";

        public const string FinalResult = "__final<>";
    }

    private const int InitialCapacity = 8;

    private int _variableId;

    private readonly StateContext _states;

    private readonly Dictionary<Type, ParameterExpression> _awaiters = new( InitialCapacity );

    private readonly Dictionary<LabelTarget, Expression> _labels = [];
    private readonly LinkedDictionary<ParameterExpression, ParameterExpression> _scopedVariables;

    public VariableResolver(
        ParameterExpression[] variables,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables,
        StateContext states )
    {
        _scopedVariables = scopedVariables ?? [];
        _states = states;

        // initialize the scoped variables with the local variables
        _scopedVariables.Push( variables.ToDictionary( x => x, CreateParameter ) );
    }

    // Helpers

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof( void ) )
            return null;

        return AddVariable( Parameter( node.Type, VariableName.Result( stateId, ref _variableId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetAwaiterVariable( Type type, int stateId )
    {
        if ( _awaiters.TryGetValue( type, out var awaiter ) )
            return awaiter;

        awaiter = AddVariable( Variable( type, VariableName.Awaiter( stateId, ref _variableId ) ) );
        _awaiters[type] = awaiter;

        return AddVariable( awaiter );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetTryVariable( int stateId )
    {
        return AddVariable( Variable( typeof( int ), VariableName.Try( stateId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetExceptionVariable( int stateId )
    {
        return AddVariable( Variable( typeof( object ), VariableName.Exception( stateId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public ParameterExpression GetFinalResult( Type type )
    {
        return AddVariable( Variable( type, VariableName.FinalResult ) );
    }

    // Resolving Visitor

    public Expression Resolve( Expression node )
    {
        return Visit( node );
    }

    internal void ResolveLabel( LabelTarget targetLabel, LabelTarget nodeLabel )
    {
        if ( targetLabel != null )
            _labels[targetLabel] = Goto( nodeLabel );
    }

    internal bool TryResolveLabel( GotoExpression node, out Expression label )
    {
        return _labels.TryGetValue( node.Target, out label );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _scopedVariables.TryGetValue( node, out var updatedParameter ) )
        {
            return updatedParameter;
        }

        return base.VisitParameter( node );
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression asyncBlockExpression )
        {
            asyncBlockExpression.ScopedVariables = _scopedVariables;
        }

        return base.VisitExtension( node );
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        if ( _labels.TryGetValue( node.Target, out var label ) )
            return label;

        return base.VisitLabel( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( _labels.TryGetValue( node.Target, out var label ) )
            return label;

        return base.VisitGoto( node );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddLocalVariables( IEnumerable<ParameterExpression> variables )
    {
        foreach ( var variable in variables )
        {
            _scopedVariables.Add( variable, variable );
        }
    }

    // helpers

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private ParameterExpression CreateParameter( ParameterExpression parameter )
    {
        return Parameter( parameter.Type, VariableName.Variable( parameter.Name, _states.TailState.StateId, ref _variableId ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private ParameterExpression AddVariable( ParameterExpression variable )
    {
        if ( _scopedVariables.TryGetValue( variable, out var existingVariable ) )
            return existingVariable;

        _scopedVariables[variable] = variable;
        return variable;
    }
}
