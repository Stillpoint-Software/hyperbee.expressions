using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class GotoTransformerVisitor : ExpressionVisitor
{
    private readonly Dictionary<LabelTarget, int> _jumpCases = new();

    private ParameterExpression _returnValue;
    private ParameterExpression[] _variables;
    private int _awaitCount;

    private readonly StateContext _states = new();

    public GotoTransformerResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _variables = variables;
        _states.AddState();

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new GotoTransformerResult 
        { 
            Nodes = _states.GetNodes(), 
            JumpCases = _jumpCases, 
            ReturnValue = _returnValue, 
            AwaitCount = _awaitCount 
        };
    }

    public GotoTransformerResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    private StateNode VisitBranch( Expression expression, int joinIndex, bool isAsyncResult = false )
    {
        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        if ( isAsyncResult )
            Visit( expression );
        else
            VisitInternal( expression );

        // Set a default Transition if the visit didn't join
        var leafState = _states.GetLeafState(); // The last StateNode visited for this branch (the branch leaf)

        if ( leafState.Transition != null )
            return branchState;

        var joinState = _states.GetState( joinIndex );

        leafState.Transition = new GotoTransition { TargetNode = joinState };
        
        if( !isAsyncResult )
            leafState.Expressions.Add( Expression.Goto( joinState.Label ) );

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
        var updatedTest = Visit( node.Test );

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
        var updatedSwitchValue = Visit( node.SwitchValue );

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

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression )
        {
            throw new NotSupportedException( "Async blocks within async blocks are not currently supported." );
            //TargetState.Expressions.Add( asyncBlockExpression.Reduce() );
        }

        if ( node is not AwaitExpression awaitExpression )
        {
            return base.VisitExtension( node );
        }

        _awaitCount++;

        var joinIndex = _states.EnterBranchState( out var sourceIndex, out var nodes );

        // Create awaiter variable
        var awaiterState = nodes[sourceIndex];
        var awaiterVariable = CreateAwaiterVariable( awaitExpression, awaiterState );

        awaiterState.Expressions.Add( Expression.Assign( 
            awaiterVariable,
            Expression.Call( awaitExpression.Target, awaitExpression.Target.Type.GetMethod( "GetAwaiter" )! ) ) 
        );

        // Add a lazy expression to build the continuation
        awaiterState.Expressions.Add( new AwaitCompletionExpression( awaiterVariable, sourceIndex ) );

        var awaitResultState = VisitBranch( awaitExpression.Target, joinIndex, true );

        awaiterState.Expressions.Add( Expression.Goto( awaitResultState.Label ) );

        // Keep awaiter variable in scope for results
        CreateGetResults( awaitResultState, awaitExpression, awaiterVariable, out var localVariable );
        
        if( localVariable != null )
            awaitResultState.Variables.Add( localVariable );

        awaitResultState.Expressions.Add( Expression.Goto( nodes[joinIndex].Label ) );
        awaitResultState.Transition = new AwaitResultTransition { TargetNode = nodes[joinIndex] };

        _jumpCases.Add( awaitResultState.Label, sourceIndex );

        // get awaiter
        var awaitTransition = new AwaitTransition { CompletionNode = awaitResultState };

        _states.ExitBranchState( sourceIndex, awaitTransition );

        return (Expression) localVariable ?? Expression.Empty();

        // Helper method to create an awaiter variable
        ParameterExpression CreateAwaiterVariable( AwaitExpression expression, StateNode stateNode )
        {
            var variable = Expression.Variable(
                expression.Type == typeof(void)
                    ? typeof(TaskAwaiter)
                    : typeof(TaskAwaiter<>).MakeGenericType( expression.Type ),
                $"awaiter<{stateNode.StateId}>" );

            stateNode.Variables.Add( variable );
            return variable;
        }

        // Helper method to create the GetResult call
        void CreateGetResults( StateNode state, AwaitExpression expression, ParameterExpression awaiter, out ParameterExpression variable )
        {
            state.Variables.Add( awaiter );
            if ( expression.Type == typeof(void) )
            {
                variable = null;
                var expr = Expression.Call( awaiter, "GetResult", Type.EmptyTypes );
                state.Expressions.Add( expr );
            }
            else
            {
                variable = Expression.Variable( expression.Type, $"<>s__{state.StateId}" );
                state.Variables.Add( variable );
                var expr = Expression.Assign( variable, Expression.Call( awaiter, "GetResult", Type.EmptyTypes ) );
                state.Expressions.Add( expr );
            }
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _variables.Contains( node ) )
            _states.GetLeafState().Variables.Add( node );
        
        return base.VisitParameter( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        var updateNode = base.VisitGoto( node );

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _returnValue ??= Expression.Variable( gotoExpression.Value!.Type, "returnValue" );

        // update this to assign to a return value versus a goto
        return Expression.Assign( _returnValue, gotoExpression.Value! );
    }

    private Expression VisitInternal( Expression expr )
    {
        switch ( expr )
        {
            case BlockExpression:
            case ConditionalExpression:
            case SwitchExpression:
            case TryExpression:
            case AwaitExpression:
            case AsyncBlockExpression:
                return Visit( expr );

            default:
                var updateNode = Visit( expr );
                // Cannot pass this in as it's updated after visiting.
                _states.GetLeafState().Expressions.Add( updateNode );
                return updateNode;
        }
    }

    private class StateContext
    {
        private readonly List<StateNode> _nodes = [];
        private readonly Stack<int> _joinIndexes = new();
        private int _leafIndex;

        public List<StateNode> GetNodes() => _nodes;

        public StateNode GetState( int index ) => _nodes[index];
        public StateNode GetLeafState() => _nodes[_leafIndex];

        public StateNode AddState()
        {
            var stateNode = new StateNode( _nodes.Count );
            _nodes.Add( stateNode );

            return stateNode;
        }

        public StateNode AddBranchState()
        {
            var stateNode = AddState();
            _leafIndex = stateNode.StateId;

            return stateNode;
        }

        public int EnterBranchState( out int sourceIndex, out List<StateNode> nodes )
        {
            var joinState = AddState();

            _joinIndexes.Push( joinState.StateId );

            sourceIndex = _leafIndex;
            nodes = _nodes;

            return joinState.StateId;
        }

        public void ExitBranchState( int sourceIndex, Transition transition )
        {
            _nodes[sourceIndex].Transition = transition;
            _leafIndex = _joinIndexes.Pop();
        }
    }
}
