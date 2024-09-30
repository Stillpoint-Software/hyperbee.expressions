using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class GotoTransformerVisitor : ExpressionVisitor
{
    private ParameterExpression _returnValue;   //BF move to StateNode?
    private ParameterExpression[] _initialVariables;
    private readonly HashSet<ParameterExpression> _variables = new(8); 
    private int _awaitCount;

    private readonly StateContext _states = new();

    private static class VariableName
    {
        // use special names to prevent collisions
        public static string Awaiter( int stateId ) => $"__awaiter<{stateId}>";
        public static string Result( int stateId ) => $"<>s__{stateId}";
    }

    public GotoTransformerResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _initialVariables = variables;
        _states.AddState();

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new GotoTransformerResult 
        { 
            Nodes = _states.GetNodes(), 
            JumpCases = _states.JumpCases, 
            ReturnValue = _returnValue, 
            AwaitCount = _awaitCount,
            Variables =  _variables
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

        VisitInternal( expression, !isAsyncResult );

        // Set a default Transition if the branch leaf didn't join
        var leafState = _states.GetVisitedLeafState(); 

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
        var updatedTest = VisitInternal( node.Test, false );

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
        var updatedSwitchValue = VisitInternal( node.SwitchValue, false );

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

        var awaiterState = nodes[sourceIndex];
        var joinState = nodes[joinIndex];

        // Assign awaiter 
        AddAwaiterVariableExpression( awaiterState, _variables, awaitExpression, out var awaiterVariable );
        awaiterState.Expressions.Add( new AwaitCompletionExpression( awaiterVariable, sourceIndex ) ); // Add a lazy expression to build the continuation

        var awaitResultState = VisitBranch( awaitExpression.Target, joinIndex, true );
        awaiterState.Expressions.Add( Expression.Goto( awaitResultState.Label ) );

        // Assign results
        AddGetResultExpression( awaitResultState, _variables, joinState, awaitExpression, awaiterVariable, out var localVariable );
        var resultExpression = (Expression) localVariable ?? Expression.Empty();
        
        // Create completion transition
        var awaitTransition = new AwaitTransition { CompletionNode = awaitResultState };
        _states.JumpCases.Add( awaitResultState.Label, sourceIndex );

        _states.ExitBranchState( sourceIndex, awaitTransition );

        return resultExpression;

        // Helper method
        //
        void AddAwaiterVariableExpression( StateNode sourceState, HashSet<ParameterExpression> variables, AwaitExpression expression, out ParameterExpression variable )
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

        // Helper method
        //
        static void AddGetResultExpression( StateNode sourceState, HashSet<ParameterExpression> variables, StateNode joinState, AwaitExpression expression, ParameterExpression awaiter, out ParameterExpression variable )
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
        if ( _initialVariables.Contains( node ) )
            //_states.GetVisitedLeafState().Variables.Add( node );
            _variables.Add( node );

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
                break;

            default:
                // Warning: visitation mutates the leaf state.
                if ( captureVisit )
                    _states.GetVisitedLeafState().Expressions.Add( result );
                break;
        }

        return result;
    }

    private class StateContext
    {
        private readonly List<StateNode> _nodes = new(8);
        private readonly Stack<int> _joinIndexes = new(8);
        private int _leafIndex;

        public Dictionary<LabelTarget, int> JumpCases { get; } = new(8);

        public List<StateNode> GetNodes() => _nodes;

        public StateNode GetState( int index ) => _nodes[index];
        public StateNode GetVisitedLeafState() => _nodes[_leafIndex];

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
