using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

public sealed class StateScope
{
    public record struct JumpCase( LabelTarget ResultLabel, LabelTarget ContinueLabel, int StateId, int? ParentId );

    public int ScopeId { get; init; }
    public StateScope Parent { get; init; }
    public List<NodeExpression> Nodes { get; set; }
    public List<JumpCase> JumpCases { get; init; }
    public Stack<NodeExpression> JoinStates { get; init; }

    private int _currentJumpState;
    private readonly int? _parentJumpState;

    public StateScope( int scopeId, StateScope parent = null, int initialCapacity = 8 )
    {
        ScopeId = scopeId;
        Parent = parent;

        Nodes = new List<NodeExpression>( initialCapacity );
        JumpCases = new List<JumpCase>( initialCapacity );
        JoinStates = new Stack<NodeExpression>( initialCapacity );

        TailState = parent?.TailState;
        _parentJumpState = parent?._currentJumpState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public NodeExpression AddState( int stateId )
    {
        var node = new NodeExpression( stateId, ScopeId );

        Nodes.Add( node );
        TailState = node;

        return node;
    }

    public NodeExpression TailState { get; private set; }

    public NodeExpression EnterGroup( int stateId, out NodeExpression sourceState )
    {
        var joinState = new NodeExpression( stateId, ScopeId ); // add a state without setting tail

        Nodes.Add( joinState );
        JoinStates.Push( joinState );

        sourceState = TailState;

        return joinState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitGroup( NodeExpression sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        TailState = JoinStates.Pop();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
    {
        _currentJumpState = stateId;
        JumpCases.Add( new JumpCase( resultLabel, continueLabel, stateId, _parentJumpState ) );
    }
}
