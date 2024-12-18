﻿#define FAST_COMPILER

using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

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

    private readonly Dictionary<ParameterExpression, ParameterExpression> _variableMap = new( InitialCapacity );
    private readonly Dictionary<ParameterExpression, ParameterExpression> _externVariableMap = new( InitialCapacity );
    private readonly HashSet<ParameterExpression> _variables;
    private readonly Stack<ICollection<ParameterExpression>> _variableBlockScope = new( InitialCapacity );
    private readonly StateContext _states;

    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private int _variableId;

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

        return AddVariable( Parameter( node.Type, VariableName.Result( stateId, ref _variableId ) ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression GetAwaiterVariable( Type type, int stateId )
    {
        return AddVariable( Variable( type, VariableName.Awaiter( stateId, ref _variableId ) ) );
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

    protected override Expression VisitBlock( BlockExpression node )
    {
        var newVars = CreateLocalVariables( node.Variables );

        _variableBlockScope.Push( newVars );

        var returnNode = base.VisitBlock( node.Update( newVars, node.Expressions ) );

        _variableBlockScope.Pop();

        return returnNode;
    }

#if FAST_COMPILER
    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        // Add Params to Externals
        var newVars = CreateLocalVariables( node.Parameters );

        _variableBlockScope.Push( newVars );

        var returnNode = base.VisitLambda( node );

        _variableBlockScope.Pop();

        return returnNode;
    }
#endif

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return TryAddVariable( node, CreateParameter, out var updatedVariable )
            ? updatedVariable
            : base.VisitParameter( node );
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression asyncBlockExpression )
        {
            asyncBlockExpression.ExternVariables = _variableBlockScope
                .SelectMany( x => x )
                .ToList()
                .AsReadOnly();
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
    public IReadOnlyCollection<Expression> GetMappedVariables()
    {
        return _variableMap.Values;
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
    private ParameterExpression CreateParameter( ParameterExpression parameter )
    {
        return Parameter( parameter.Type, VariableName.Variable( parameter.Name, _states.TailState.StateId, ref _variableId ) );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private ParameterExpression AddVariable( ParameterExpression variable )
    {
        if ( _variableMap.TryGetValue( variable, out var existingVariable ) )
            return existingVariable;

        _variableMap[variable] = variable;
        return variable;
    }

    List<ParameterExpression> CreateLocalVariables( ReadOnlyCollection<ParameterExpression> parameters )
    {
        var vars = new List<ParameterExpression>();

        foreach ( var variable in parameters )
        {
            if ( variable.Name!.StartsWith( "__extern." ) )
            {
                vars.Add( variable );
                _externVariableMap.TryAdd( variable, variable );
                continue;
            }

            var newVar = Parameter(
                variable.Type,
                VariableName.ExternVariable( variable.Name, _states.TailState.StateId, ref _variableId )
            );

            _externVariableMap.TryAdd( variable, newVar );
            vars.Add( newVar );
        }

        return vars;
    }

    private bool TryAddVariable(
        ParameterExpression parameter,
        Func<ParameterExpression, ParameterExpression> createParameter,
        out ParameterExpression updatedParameterExpression
    )
    {
        if ( _variableMap.TryGetValue( parameter, out updatedParameterExpression ) ||
             _externVariableMap.TryGetValue( parameter, out updatedParameterExpression ) )
        {
            return true;
        }

        if ( _variables == null || !_variables.Contains( parameter ) )
        {
            return false;
        }

        updatedParameterExpression = createParameter( parameter );
        _variableMap[parameter] = updatedParameterExpression;

        return true;
    }
}
