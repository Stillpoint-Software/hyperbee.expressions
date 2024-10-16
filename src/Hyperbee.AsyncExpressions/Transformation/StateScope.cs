using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

public class StateScope 
{
    public record struct JumpCase( LabelTarget ResultLabel, LabelTarget ContinueLabel, int StateId, int? ParentId );

    public int ScopeId { get; init; }
    public StateScope Parent { get; init; }
    public List<NodeExpression> Nodes { get; set; }
    public List<JumpCase> JumpCases { get; init; }
    public Stack<NodeExpression> JoinStates { get; init; }

    private NodeExpression _tailState;
    private int _currentJumpState;
    private readonly int? _parentJumpState;

    public StateScope( int scopeId, StateScope parent = null, int initialCapacity = 8 )
    {
        ScopeId = scopeId;
        Parent = parent;

        Nodes = new List<NodeExpression>( initialCapacity );
        JumpCases = new List<JumpCase>( initialCapacity );
        JoinStates = new Stack<NodeExpression>( initialCapacity );

        _tailState = parent?.GetTailState();
        _parentJumpState = parent?._currentJumpState;
    }

    public NodeExpression AddState( int id )
    {
        var stateNode = new NodeExpression( id, ScopeId );
        _tailState = stateNode;

        Nodes.Add( stateNode );

        return stateNode;
    }

    public NodeExpression GetTailState() => _tailState; 

    public NodeExpression EnterGroup( int id, out NodeExpression sourceState )
    {
        var joinState = new NodeExpression( id, ScopeId ); // add a state without setting tail

        Nodes.Add( joinState );
        JoinStates.Push( joinState );

        sourceState = _tailState;

        return joinState;
    }

    public void ExitGroup( NodeExpression sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        _tailState = JoinStates.Pop();
    }

    public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
    {
        _currentJumpState = stateId;
        JumpCases.Add( new JumpCase( resultLabel, continueLabel, stateId, _parentJumpState ) );
    }

    public Expression CreateJumpTable( List<StateScope> scopes, Expression stateFieldExpression )
    {
        var jumpTable = new List<SwitchCase>( JumpCases.Count );

        foreach ( var jumpCase in JumpCases )
        {
            // Go to the result of awaiter
            var resultJumpExpression = Expression.SwitchCase(
                Expression.Block(
                    Expression.Assign( stateFieldExpression, Expression.Constant( -1 ) ),
                    Expression.Goto( jumpCase.ResultLabel )
                ),
                Expression.Constant( jumpCase.StateId )
            );

            jumpTable.Add( resultJumpExpression );

            // go to nested jump cases
            var nestedJumps = JumpCaseTests( this, jumpCase.StateId ).ToArray();
            if ( nestedJumps.Length > 0 )
            {
                var nestedJumpExpression = Expression.SwitchCase(
                    Expression.Block(
                        Expression.Assign( stateFieldExpression, Expression.Constant( -1 ) ),
                        Expression.Goto( jumpCase.ContinueLabel )
                    ),
                    nestedJumps
                );

                jumpTable.Add( nestedJumpExpression );
            }

            continue;

            // recursive function to build jump table cases
            IEnumerable<Expression> JumpCaseTests( StateScope current, int currentStateId )
            {
                // recursive fallthrough jump tables
                for ( var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++ )
                {
                    var scope = scopes[scopeIndex];

                    if ( scope.Parent != current )
                        continue;

                    for ( var jumpIndex = 0; jumpIndex < scope.JumpCases.Count; jumpIndex++ )
                    {
                        var childJumpCase = scope.JumpCases[jumpIndex];

                        if ( childJumpCase.ParentId != currentStateId ) 
                            continue;

                        // return self
                        yield return Expression.Constant( childJumpCase.StateId );

                        // nested jump cases
                        foreach ( var c in JumpCaseTests( scope, childJumpCase.StateId ) )
                        {
                            yield return c;
                        }
                    }
                }
            }
        }

        return Expression.Switch(
            stateFieldExpression,
            Expression.Empty(),
            [..jumpTable]
        );
    }

}
