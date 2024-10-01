using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class LoweringVisitor : ExpressionVisitor
{
    private ParameterExpression _returnValue;
    private ParameterExpression[] _definedVariables;
    private readonly HashSet<ParameterExpression> _variables = new(8); 
    private int _awaitCount;

    private readonly StateContext _states = new();
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private static class VariableName
    {
        // use special names to prevent collisions
        public static string Awaiter( int stateId ) => $"__awaiter<{stateId}>";
        public static string Result( int stateId ) => $"__result<{stateId}>";
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

    private StateNode VisitBranch( Expression expression, int joinIndex, bool captureVisit = true )
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
        
        if( captureVisit )
            tailState.Expressions.Add( Expression.Goto( joinState.Label ) );

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
            IfTrue = VisitBranch( node.IfTrue, joinIndex ),
            IfFalse = (node.IfFalse is not DefaultExpression)
                ? VisitBranch( node.IfFalse, joinIndex )
                : nodes[joinIndex]
        };

        var gotoConditional = Expression.IfThenElse(
            updatedTest,
            Expression.Goto( conditionalTransition.IfTrue.Label ),
            Expression.Goto( conditionalTransition.IfFalse.Label ) );

        nodes[sourceIndex].Expressions.Add( gotoConditional );

        _states.ExitBranchState( sourceIndex, conditionalTransition );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = VisitInternal( node.SwitchValue, captureVisit: false );

        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var switchTransition = new SwitchTransition();

        Expression defaultBody = null;
        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinIndex );
            defaultBody = Expression.Goto( switchTransition.DefaultNode.Label );
        }

        List<SwitchCase> cases = [];
        foreach ( var switchCase in node.Cases )
        {
            var caseNode = VisitBranch( switchCase.Body, joinIndex );
            switchTransition.CaseNodes.Add( caseNode );

            // TODO: Visit test values because they could be async
            cases.Add( Expression.SwitchCase( Expression.Goto( caseNode.Label ), switchCase.TestValues ) );
        }

        var gotoSwitch = Expression.Switch(
            updatedSwitchValue,
            defaultBody,
            [.. cases] );

        nodes[sourceIndex].Expressions.Add( gotoSwitch );

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

        List<CatchBlock> catches = [];
        var joinLabel = nodes[joinIndex].Label;

        foreach ( var catchBlock in node.Handlers )
        {
            var catchNode = VisitBranch( catchBlock.Body, joinIndex );
            tryCatchTransition.CatchNodes.Add( catchNode );
            catches.Add( Expression.Catch( catchBlock.Test, Expression.Goto( catchNode.Label ) ) );

            catchNode.Expressions.Add( Expression.Goto( joinLabel ) );
        }

        Expression finallyBody = null;
        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitBranch( node.Finally, joinIndex );
            finallyBody = Expression.Goto( tryCatchTransition.FinallyNode.Label );
            tryCatchTransition.FinallyNode.Expressions.Add( Expression.Goto( joinLabel ) );
        }

        var newTry = Expression.TryCatchFinally(
            Expression.Goto( tryCatchTransition.TryNode.Label ),
            finallyBody,
            [.. catches]
        );

        nodes[sourceIndex].Expressions.Add( newTry );

        _states.ExitBranchState( sourceIndex, tryCatchTransition );

        return node;
    }

    protected override Expression VisitLoop(LoopExpression node)
    {
        var joinIndex = _states.EnterBranchState(out var sourceIndex, out var nodes);

        var joinState = _states.GetState( joinIndex );

        var loopTransition = new LoopTransition { TargetNode = joinState };

        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        var continueGoto = Expression.Goto( branchState.Label );
        var breakGoto = Expression.Goto( joinState.Label );

        if ( node.BreakLabel != null )
            _labels[node.BreakLabel] = breakGoto;
        if ( node.ContinueLabel != null )
            _labels[node.ContinueLabel] = continueGoto;

        VisitInternal( node.Body, captureVisit: false );

        var tailState = _states.GetBranchTailState();

        if ( tailState.Transition != null )
            return node;

        tailState.Expressions.Add( breakGoto );
        tailState.Transition = new GotoTransition { TargetNode = joinState };

        loopTransition.Body = branchState;

        nodes[sourceIndex].Expressions.Add( continueGoto );

        _states.ExitBranchState(sourceIndex, loopTransition);
    
        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression )
        {
            // return the complete inner state machine for nested async blocks
            return node.Reduce();
        }

        if ( node is not AwaitExpression awaitExpression )
        {
            return base.VisitExtension( node );
        }

        _awaitCount++;

        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        var awaiterState = nodes[sourceIndex];
        var joinState = nodes[joinIndex];

        // Assign awaiter and add await completion
        AddAwaiterVariableExpression( awaiterState, _variables, awaitExpression, out var awaiterVariable );
        awaiterState.Expressions.Add( new AwaitCompletionExpression( awaiterVariable, sourceIndex ) ); 

        var awaitResultState = VisitBranch( awaitExpression.Target, joinIndex, captureVisit: false );
        awaiterState.Expressions.Add( Expression.Goto( awaitResultState.Label ) );

        // Assign results
        AddGetResultExpression( awaitResultState, _variables, joinState, awaitExpression, awaiterVariable, out var localVariable );
        var resultExpression = (Expression) localVariable ?? Expression.Empty();
        
        // Create completion transition
        var awaitTransition = new AwaitTransition { CompletionNode = awaitResultState };
        _states.JumpCases.Add( awaitResultState.Label, sourceIndex );

        _states.ExitBranchState( sourceIndex, awaitTransition );

        return resultExpression;

        // Helper method that adds an awaiter variable to the source state
        static void AddAwaiterVariableExpression( StateNode sourceState, 
            HashSet<ParameterExpression> variables, 
            AwaitExpression expression, 
            out ParameterExpression variable )
        {
            // Add variable to source state
            var type = expression.Type == typeof(void)
                ? typeof(TaskAwaiter)
                : typeof(TaskAwaiter<>).MakeGenericType( expression.Type );

            variable = Expression.Variable( type, VariableName.Awaiter(sourceState.StateId) );
            variables.Add( variable );

            // Add GetAwaiter call to source state
            var getAwaiterMethod = expression.Target.Type.GetMethod( "GetAwaiter" )!;
            var assign = Expression.Assign(
                variable,
                Expression.Call( expression.Target, getAwaiterMethod )
            );
            
            sourceState.Expressions.Add( assign );
        }

        // Helper method that adds a GetResult call to the source state
        static void AddGetResultExpression( StateNode sourceState, 
            HashSet<ParameterExpression> variables, 
            StateNode joinState, 
            AwaitExpression expression, 
            ParameterExpression awaiter, 
            out ParameterExpression variable )
        {
            variables.Add( awaiter );

            if ( expression.Type == typeof(void) )
            {
                variable = null;
                var expr = Expression.Call( awaiter, "GetResult", Type.EmptyTypes );
                sourceState.Expressions.Add( expr );
            }
            else
            {
                variable = Expression.Variable( expression.Type, VariableName.Result( sourceState.StateId ) );
                variables.Add( variable );
                var expr = Expression.Assign( variable, Expression.Call( awaiter, "GetResult", Type.EmptyTypes ) );
                sourceState.Expressions.Add( expr );
            }

            sourceState.Expressions.Add( Expression.Goto( joinState.Label ) );
            sourceState.Transition = new AwaitResultTransition { TargetNode = joinState };
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _definedVariables.Contains( node ) )
            _variables.Add( node );

        return base.VisitParameter( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        var updateNode = base.VisitGoto( node );

        if ( _labels.TryGetValue( node.Target, out var labelExpression ) )
        {
            return labelExpression;
        }

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _returnValue ??= Expression.Variable( gotoExpression.Value!.Type, "returnValue" );

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
        private const int InitialCapacity = 8;

        private readonly List<StateNode> _nodes = new(InitialCapacity);
        private readonly Stack<int> _joinIndexes = new(InitialCapacity);
        private int _tailIndex;

        public Dictionary<LabelTarget, int> JumpCases { get; } = new(InitialCapacity);

        public List<StateNode> GetNodes() => _nodes;

        public StateNode GetState( int index ) => _nodes[index];
        public StateNode GetBranchTailState() => _nodes[_tailIndex];

        public StateNode AddState()
        {
            var stateNode = new StateNode( _nodes.Count );
            _nodes.Add( stateNode );

            return stateNode;
        }

        public StateNode AddBranchState()
        {
            var stateNode = AddState();
            _tailIndex = stateNode.StateId;

            return stateNode;
        }

        public int EnterBranchState( out int sourceIndex, out List<StateNode> nodes )
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
