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
            Variables =  _variables
        };
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    private NodeExpression VisitBranch( Expression expression, int joinIndex, bool captureVisit = true )
    {
        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        VisitInternal( expression, captureVisit );

        // Set a default Transition if the branch tail didn't join
        var tailState = _states.GetBranchTailState(); 

        if ( tailState.Transition != null )
            return branchState;

        var joinState = _states.GetState( joinIndex );

        tailState.Transition = new GotoTransition { TargetNode = joinState };

        return branchState;
    }

    private NodeExpression VisitLoopBranch( Expression expression, LabelTarget breakLabel, LabelTarget continueLabel, out Expression continueGoto, int joinIndex )
    {
        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        var joinState = _states.GetState( joinIndex );

        var breakGoto = Expression.Goto( joinState.NodeLabel );
        continueGoto = Expression.Goto( branchState.NodeLabel );

        if ( breakLabel != null )
            _labels[breakLabel] = breakGoto;

        if ( continueLabel != null )
            _labels[continueLabel] = continueGoto;

        VisitInternal( expression, captureVisit: false );

        var tailState = _states.GetBranchTailState();

        if ( tailState.Transition != null )
            return branchState;

        tailState.Expressions.Add( breakGoto );
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

        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinIndex ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinIndex )
                : nodes[joinIndex]
        };

        _states.ExitBranchState( sourceIndex, conditionalTransition );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = VisitInternal( node.SwitchValue, captureVisit: false );

        var joinIndex = _states.EnterBranchState( out var sourceIndex, out _ );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinIndex );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues], // TODO: Visit these because they could be async
                VisitBranch( switchCase.Body, joinIndex )
            );
        }

        _states.ExitBranchState( sourceIndex, switchTransition );

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var tryCatchTransition = new TryCatchTransition
        {
            TryNode = VisitBranch( node.Body, joinIndex )
        };

        var joinLabel = nodes[joinIndex].NodeLabel;

        foreach ( var catchBlock in node.Handlers )
        {
            tryCatchTransition.AddCatchBlock(
                catchBlock.Test,
                VisitBranch( catchBlock.Body, joinIndex ) );
        }

        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitBranch( node.Finally, joinIndex );
            tryCatchTransition.FinallyNode.Expressions.Add( Expression.Goto( joinLabel ) );
        }

        _states.ExitBranchState( sourceIndex, tryCatchTransition );

        return node;
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var loopTransition = new LoopTransition
        {
            BodyNode = VisitLoopBranch( 
                node.Body, 
                node.BreakLabel, 
                node.ContinueLabel, 
                out var continueGoto,
                joinIndex ),
            TargetNode = nodes[joinIndex],
            ContinueGoto = continueGoto
        };

        _states.ExitBranchState( sourceIndex, loopTransition );

        return node;
    }

    protected static Expression VisitAsyncBlock( AsyncBlockExpression node )
    {
        return node.Reduce();
    }

    protected Expression VisitAwait( AwaitExpression node )
    {
        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var completionState = VisitBranch( node.Target, joinIndex, captureVisit: false );

        _awaitCount++;

        var awaiterVariable = Expression.Variable( GetAwaiterType(), VariableName.Awaiter( sourceIndex ) );
        _variables.Add( awaiterVariable );

        ParameterExpression resultVariable = null;

        if ( node.Type != typeof( void ) )
        {
            resultVariable = Expression.Variable( node.Type, VariableName.Result( completionState.StateId ) );
            _variables.Add( resultVariable );
        }

        completionState.Transition = new AwaitResultTransition
        {
            TargetNode = nodes[joinIndex],
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable
        };

        _states.JumpCases.Add( completionState.NodeLabel, sourceIndex );

        var awaitTransition = new AwaitTransition
        {
            Target = node.Target,
            StateId = sourceIndex,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState
        };

        _states.ExitBranchState( sourceIndex, awaitTransition );

        return (Expression) resultVariable ?? Expression.Empty();

        // Helper method to get the awaiter type
        Type GetAwaiterType() => node.Type == typeof(void)
            ? typeof(TaskAwaiter)
            : typeof(TaskAwaiter<>).MakeGenericType( node.Type );
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

        public NodeExpression GetState( int index ) => _nodes[index];
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

        public int EnterBranchState( out int sourceIndex, out List<NodeExpression> nodes )
        {
            var joinState = AddState();

            _joinIndexes.Push( joinState.StateId );

            sourceIndex = _tailIndex;
            nodes = _nodes;

            return joinState.StateId;
        }

        public void ExitBranchState( int sourceIndex, Transition transition )
        {
            _nodes[sourceIndex].Transition = transition;
            _tailIndex = _joinIndexes.Pop();
        }
    }
}
