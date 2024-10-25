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

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public Expression CreateJumpTable( List<StateScope> scopes, Expression stateFieldExpression )
    {
        return JumpTableBuilder.Build( this, JumpCases, scopes, stateFieldExpression );
    }

    private static class JumpTableBuilder
    {
        public static Expression Build( StateScope current, List<JumpCase> jumpCases, List<StateScope> scopes, Expression stateFieldExpression )
        {
            var jumpTable = new List<SwitchCase>( jumpCases.Count );

            for ( var index = 0; index < jumpCases.Count; index++ )
            {
                var jumpCase = jumpCases[index];

                // Go to the result of awaiter
                var resultJumpExpression = Expression.SwitchCase(
                    Expression.Block(
                        Expression.Assign( stateFieldExpression, Expression.Constant( -1 ) ),
                        Expression.Goto( jumpCase.ResultLabel )
                    ),
                    Expression.Constant( jumpCase.StateId )
                );

                jumpTable.Add( resultJumpExpression );

                // Go to nested jump cases

                var testValues = JumpCaseTests( current, jumpCase.StateId, scopes );

                if ( testValues.Count <= 0 )
                    continue;

                var nestedJumpExpression = Expression.SwitchCase(
                    Expression.Block(
                        Expression.Assign( stateFieldExpression, Expression.Constant( -1 ) ),
                        Expression.Goto( jumpCase.ContinueLabel )
                    ),
                    testValues
                );

                jumpTable.Add( nestedJumpExpression );
            }

            return Expression.Switch(
                stateFieldExpression,
                Expression.Empty(),
                [.. jumpTable]
            );
        }

        // Iterative function to build jump table cases
        private static List<Expression> JumpCaseTests( StateScope current, int currentStateId, List<StateScope> scopes )
        {
            var testValues = new List<Expression>();
            var stack = new Stack<(StateScope, int)>();

            var scope = current;
            var stateId = currentStateId;

            while ( true )
            {
                for ( var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++ )
                {
                    var nestedScope = scopes[scopeIndex];

                    if ( nestedScope.Parent != scope )
                        continue;

                    for ( var jumpIndex = 0; jumpIndex < nestedScope.JumpCases.Count; jumpIndex++ )
                    {
                        var childJumpCase = nestedScope.JumpCases[jumpIndex];

                        if ( childJumpCase.ParentId != stateId )
                            continue;

                        // Return self
                        testValues.Add( Expression.Constant( childJumpCase.StateId ) );

                        // Push nested jump cases onto the stack
                        stack.Push( (nestedScope, childJumpCase.StateId) );
                    }
                }

                if ( !stack.TryPop( out var item ) )
                    break;

                (scope, stateId) = item;
            }

            return testValues;
        }
    }
}
