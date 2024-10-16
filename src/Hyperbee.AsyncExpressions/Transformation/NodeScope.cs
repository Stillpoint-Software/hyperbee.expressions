using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

public class NodeScope
{
    public record struct JumpCase( LabelTarget ResultLabel, LabelTarget ContinueLabel, int StateId, int? ParentId );

    public int ScopeId { get; init; }
    public NodeScope Parent { get; init; }
    public List<NodeExpression> Nodes { get; set; }
    public List<JumpCase> JumpCases { get; init; }
    public  Stack<NodeExpression> JoinStates { get; init; }

    private NodeExpression _tailState;
    private int _currentJumpState;
    private readonly int? _parentJumpState;

    public NodeScope( int scopeId, NodeExpression tailState, NodeScope parent = null, int initialCapacity = 8 )
    {
        ScopeId = scopeId;
        Parent = parent;
        _parentJumpState = parent?._currentJumpState;

        Nodes = new List<NodeExpression>( initialCapacity );

        _tailState = tailState;
        JumpCases = new List<JumpCase>( initialCapacity );
        JoinStates = new Stack<NodeExpression>( initialCapacity );
    }

    public NodeExpression AddState( int id )
    {
        var stateNode = new NodeExpression( id, ScopeId );

        // On first add set the tail state
        if ( Nodes.Count == 0)
            _tailState = stateNode;

        Nodes.Add( stateNode );

        return stateNode;
    }

    public NodeExpression AddBranchState( int id )
    {
        var stateNode = AddState( id );
        _tailState = stateNode;

        return stateNode;
    }

    public NodeExpression GetBranchTailState() => _tailState;

    public NodeExpression EnterBranchState( NodeExpression joinState, out NodeExpression sourceState )
    {
        JoinStates.Push( joinState );

        sourceState = _tailState;

        return joinState;
    }

    public void ExitBranchState( NodeExpression sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        _tailState = JoinStates.Pop();
    }

    public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
    {
        _currentJumpState = stateId;
        JumpCases.Add( new JumpCase( resultLabel, continueLabel, stateId, _parentJumpState ) );
    }

    public Expression CreateJumpTable( List<NodeScope> scopes, Expression stateFieldExpression )
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
            IEnumerable<Expression> JumpCaseTests( NodeScope current, int currentStateId )
            {
                // recursive fallthrough jump tables
                foreach (var scope in scopes.Where(scope => scope.Parent == current))
                {
                    foreach ( var childJumpCase in scope.JumpCases )
                    {
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
