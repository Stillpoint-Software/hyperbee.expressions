using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class LoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 8;

    private ParameterExpression _returnValue;
    private ParameterExpression[] _definedVariables;
    private readonly HashSet<ParameterExpression> _variables = new(InitialCapacity);
    private int _awaitCount;

    private readonly StateContext _states = new(InitialCapacity);
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private static class VariableName
    {
        // use special names to prevent collisions
        public static string Awaiter( int stateId ) => $"__awaiter<{stateId}>";
        public static string Result( int stateId ) => $"__result<{stateId}>";

        public const string Return = "return<>";
    }

    public LoweringResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _definedVariables = variables;
        _states.AddState();

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new LoweringResult
        {
            Nodes = _states.GetNodes(),
            JumpCases = _states.JumpCases,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = _variables
        };
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState,
        ParameterExpression resultVariable = null, Action<NodeExpression> init = null, bool captureVisit = true )
    {
        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        init?.Invoke( branchState );

        VisitInternal( expression, captureVisit );

        // Set a default Transition if the branch tail didn't join
        var tailState = _states.GetBranchTailState();
        tailState.ResultVariable = resultVariable;

        if ( tailState.Transition != null )
        {
            return branchState;
        }

        tailState.Transition = new GotoTransition { TargetNode = joinState };

        return branchState;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        foreach ( var expression in node.Expressions )
        {
            VisitInternal( expression );
        }

        return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var updatedTest = VisitInternal( node.Test, captureVisit: false );

        var joinState = _states.EnterBranchState( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinState, resultVariable ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState, resultVariable )
                : joinState,
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitBranchState( sourceState, conditionalTransition );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = VisitInternal( node.SwitchValue, captureVisit: false );

        var joinState = _states.EnterBranchState( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState, resultVariable );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues], // TODO: Visit these because they could be async
                VisitBranch( switchCase.Body, joinState, resultVariable )
            );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitBranchState( sourceState, switchTransition );

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var joinState = _states.EnterBranchState( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var tryCatchTransition = new TryCatchTransition { TryNode = VisitBranch( node.Body, joinState, resultVariable ) };

        foreach ( var catchBlock in node.Handlers )
        {
            tryCatchTransition.AddCatchBlock(
                catchBlock.Test,
                VisitBranch( catchBlock.Body, joinState ) );
        }

        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitInternal( node.Finally );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitBranchState( sourceState, tryCatchTransition );

        return node;
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinState = _states.EnterBranchState( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var loopTransition = new LoopTransition
        {
            BodyNode = VisitBranch( node.Body, joinState, resultVariable, InitializeLabels )
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        // TODO: This seems wrong
        var tailState = _states.GetBranchTailState();
        if ( tailState.Transition is GotoTransition gotoTransition )
            gotoTransition.TargetNode = loopTransition.BodyNode;
        
        _states.ExitBranchState( sourceState, loopTransition );

        return node;

        // Helper function for fixing loop labels
        void InitializeLabels( NodeExpression branchState )
        {
            if ( node.ContinueLabel != null )
                _labels[node.ContinueLabel] = Expression.Goto( branchState.NodeLabel );

            if ( node.BreakLabel != null )
                _labels[node.BreakLabel] = Expression.Goto( joinState.NodeLabel );
        }
    }

    protected static Expression VisitAsyncBlock( AsyncBlockExpression node )
    {
        return node.Reduce();
    }

    protected Expression VisitAwait( AwaitExpression node )
    {
        var joinState = _states.EnterBranchState( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var completionState = VisitBranch( node.Target, joinState, resultVariable, captureVisit: false );

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterType = awaitBinder.GetAwaiterMethod.ReturnType;

        var awaiterVariable = Expression.Variable( awaiterType, VariableName.Awaiter( sourceState.StateId ) ); 
        _variables.Add( awaiterVariable );

        completionState.Transition = new AwaitResultTransition
        {
            TargetNode = joinState, 
            AwaiterVariable = awaiterVariable, 
            ResultVariable = resultVariable,
            GetResultMethod = awaitBinder.GetResultMethod
        };

        _states.JumpCases.Add( completionState.NodeLabel, sourceState.StateId );

        var awaitTransition = new AwaitTransition
        {
            Target = node.Target,
            StateId = sourceState.StateId,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState,
            GetAwaiterMethod = awaitBinder.GetAwaiterMethod,
            ConfigureAwait = node.ConfigureAwait
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitBranchState( sourceState, awaitTransition );

        return (Expression) resultVariable ?? Expression.Empty();
    }

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AsyncBlockExpression asyncBlockExpression => VisitAsyncBlock( asyncBlockExpression ),
            AwaitExpression awaitExpression => VisitAwait( awaitExpression ),
            _ => base.VisitExtension( node )
        };
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _definedVariables.Contains( node ) )
            _variables.Add( node );

        return base.VisitParameter( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( _labels.TryGetValue( node.Target, out var labelExpression ) )
        {
            return labelExpression;
        }

        var updateNode = base.VisitGoto( node );

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _returnValue ??= Expression.Variable( gotoExpression.Value!.Type, VariableName.Return );

        // update this to assign to a return value versus a goto
        return Expression.Assign( _returnValue, gotoExpression.Value! );
    }

    private Expression VisitInternal( Expression expr, bool captureVisit = true )
    {
        var result = Visit( expr );

        switch ( expr )
        {
            case BlockExpression:
            case ConditionalExpression:
            case SwitchExpression:
            case TryExpression:
            case AwaitExpression:
            case AsyncBlockExpression:
            case LoopExpression:
                break;

            default:
                // Warning: visitation mutates the tail state.
                if ( captureVisit )
                    _states.GetBranchTailState().Expressions.Add( result );
                break;
        }

        return result;
    }

    private ParameterExpression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof(void) )
            return null;

        ParameterExpression returnVariable =
            Expression.Parameter( node.Type, VariableName.Result( stateId ) );
        _variables.Add( returnVariable );

        return returnVariable;
    }

    private class StateContext
    {
        private readonly List<NodeExpression> _nodes;
        private readonly Stack<int> _joinIndexes;
        private int _tailIndex;

        public Dictionary<LabelTarget, int> JumpCases { get; }

        public StateContext( int initialCapacity )
        {
            _tailIndex = 0;
            _nodes = new List<NodeExpression>( initialCapacity );
            _joinIndexes = new Stack<int>( initialCapacity );

            JumpCases = new Dictionary<LabelTarget, int>( initialCapacity );
        }

        public List<NodeExpression> GetNodes() => _nodes;

        public NodeExpression GetBranchTailState() => _nodes[_tailIndex];

        public NodeExpression AddState()
        {
            var stateNode = new NodeExpression( _nodes.Count );
            _nodes.Add( stateNode );

            return stateNode;
        }

        public NodeExpression AddBranchState()
        {
            var stateNode = AddState();
            _tailIndex = stateNode.StateId;

            return stateNode;
        }

        public NodeExpression EnterBranchState( out NodeExpression sourceState )
        {
            var joinState = AddState();

            _joinIndexes.Push( joinState.StateId );

            sourceState = _nodes[_tailIndex];

            return joinState;
        }

        public void ExitBranchState( NodeExpression sourceState, Transition transition )
        {
            sourceState.Transition = transition;
            _tailIndex = _joinIndexes.Pop();
        }
    }
}
