using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class GotoTransformerVisitor : ExpressionVisitor
{
    private readonly List<StateNode> _nodes = [];
    private readonly Stack<int> _joinIndexes = new();

    // jump table?
    private readonly JumpTableExpression _jumpTable = new();
    private ParameterExpression _returnValue;
    private ParameterExpression[] _variables;
    private int _awaitCount;

    private int _targetStateIndex;
    private StateNode GetTargetState() => _nodes[_targetStateIndex];
    
    public GotoTransformerResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _variables = variables;
        InsertState();

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new GotoTransformerResult { Nodes = _nodes, JumpTable = _jumpTable, ReturnValue = _returnValue, AwaitCount = _awaitCount };
    }

    public GotoTransformerResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TransitionToState( int stateIndex )
    {
        _targetStateIndex = stateIndex;
    }

    private StateNode InsertState()
    {
        var stateNode = new StateNode( _nodes.Count );
        _nodes.Add( stateNode );

        return stateNode;
    }

    private StateNode VisitState( Expression expression, int joinIndex, bool isAsyncResult = false )
    {
        var state = InsertState();
        TransitionToState( state.StateId );

        if ( isAsyncResult )
            Visit( expression );
        else
            VisitInternal( expression );

        // Transition was handling during the visit
        var targetState = GetTargetState();
        if ( targetState.Transition != null )
            return state;

        // We did not visit the expression, so we need to initialize the defaults
        targetState.Transition = new GotoTransition { TargetNode = _nodes[joinIndex] };
        
        if( !isAsyncResult )
            targetState.Expressions.Add( Expression.Goto( _nodes[joinIndex].Label ) );

        return state;
    }

    private int EnterTransitionContext( out int sourceIndex )
    {
        var stateNode = InsertState(); 

        _joinIndexes.Push( stateNode.StateId );
        sourceIndex = _targetStateIndex;

        return stateNode.StateId;
    }

    private void ExitTransitionContext( int sourceIndex, Transition transition )
    {
        _nodes[sourceIndex].Transition = transition;
        TransitionToState( _joinIndexes.Pop() ); // Transition back to the previous join state
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

        var joinIndex = EnterTransitionContext( out var sourceIndex );

        var conditionalTransition = new ConditionalTransition
        {
            IfTrue = VisitState( node.IfTrue, joinIndex ),
            IfFalse = (node.IfFalse is not DefaultExpression)
                ? VisitState( node.IfFalse, joinIndex )
                : _nodes[joinIndex]
        };

        var gotoConditional = Expression.IfThenElse(
            updatedTest,
            Expression.Goto( conditionalTransition.IfTrue.Label ),
            Expression.Goto( conditionalTransition.IfFalse.Label ) );

        _nodes[sourceIndex].Expressions.Add( gotoConditional );

        ExitTransitionContext( sourceIndex, conditionalTransition );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = Visit( node.SwitchValue );

        var joinIndex = EnterTransitionContext( out var sourceIndex );

        var switchTransition = new SwitchTransition();

        Expression defaultBody = null;
        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitState( node.DefaultBody, joinIndex );
            defaultBody = Expression.Goto( switchTransition.DefaultNode.Label );
        }

        List<SwitchCase> cases = [];
        foreach ( var switchCase in node.Cases )
        {
            var caseNode = VisitState( switchCase.Body, joinIndex );
            switchTransition.CaseNodes.Add( caseNode );

            // TODO: Visit test values because they could be async
            cases.Add( Expression.SwitchCase( Expression.Goto( caseNode.Label ), switchCase.TestValues ) );
        }

        var gotoSwitch = Expression.Switch(
            updatedSwitchValue,
            defaultBody,
            [.. cases] );

        _nodes[sourceIndex].Expressions.Add( gotoSwitch );

        ExitTransitionContext( sourceIndex, switchTransition );

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var joinIndex = EnterTransitionContext( out var sourceIndex );

        var tryCatchTransition = new TryCatchTransition
        {
            TryNode = VisitState( node.Body, joinIndex )
        };

        List<CatchBlock> catches = [];
        foreach ( var catchBlock in node.Handlers )
        {
            var catchNode = VisitState( catchBlock.Body, joinIndex );
            tryCatchTransition.CatchNodes.Add( catchNode );
            catches.Add( Expression.Catch( catchBlock.Test, Expression.Goto( catchNode.Label ) ) );

            catchNode.Expressions.Add( Expression.Goto( _nodes[joinIndex].Label ) );
        }

        Expression finallyBody = null;
        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitState( node.Finally, joinIndex );
            finallyBody = Expression.Goto( tryCatchTransition.FinallyNode.Label );
            tryCatchTransition.FinallyNode.Expressions.Add( Expression.Goto( _nodes[joinIndex].Label ) );
        }

        var newTry = Expression.TryCatchFinally(
            Expression.Goto( tryCatchTransition.TryNode.Label ),
            finallyBody,
            [.. catches]
        );

        _nodes[sourceIndex].Expressions.Add( newTry );

        ExitTransitionContext( sourceIndex, tryCatchTransition );

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
        var joinIndex = EnterTransitionContext( out var sourceIndex );

        // Create awaiter variable
        var awaiterState = GetTargetState();
        var awaiterVariable = CreateAwaiterVariable( awaitExpression, awaiterState );

        awaiterState.Expressions.Add(
            Expression.Assign( awaiterVariable,
                Expression.Call( awaitExpression.Target, awaitExpression.Target.Type.GetMethod( "GetAwaiter" )! ) ) );

        // Add a lazy expression to build the continuation
        awaiterState.Expressions.Add( new AwaitCompletionExpression( awaiterVariable, sourceIndex ) );

        var awaitResultState = VisitState( awaitExpression.Target, joinIndex, true );

        awaiterState.Expressions.Add( Expression.Goto( awaitResultState.Label ) );

        // Keep awaiter variable in scope for results
        CreateGetResults( awaitResultState, awaitExpression, awaiterVariable, out var localVariable );
        if(localVariable != null)
            awaitResultState.Variables.Add( localVariable );

        awaitResultState.Expressions.Add( Expression.Goto( _nodes[joinIndex].Label ) );

        awaitResultState.Transition = new AwaitResultTransition { TargetNode = _nodes[joinIndex] };

        _jumpTable.Add( awaitResultState.Label, sourceIndex );

        // get awaiter
        var awaitTransition = new AwaitTransition { CompletionNode = awaitResultState };

        ExitTransitionContext( sourceIndex, awaitTransition );

        return (Expression) localVariable ?? Expression.Empty();

        // helper methods
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

        void CreateGetResults( StateNode state,
            AwaitExpression expression,
            ParameterExpression awaiter,
            out ParameterExpression variable )
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
            GetTargetState().Variables.Add( node );
        
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
                GetTargetState().Expressions.Add( updateNode );
                return updateNode;
        }
    }
}

public class JumpTableExpression : Expression
{
    private readonly Dictionary<LabelTarget, int> _jumpCases = new();

    public Expression State { get; set; }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( State == null )
            throw new NullReferenceException( "State is not set" );

        return Switch( State, Empty(),
            _jumpCases.Select( c =>
                    SwitchCase(
                        Block(
                            Assign( State, Constant( -1 ) ),
                            Goto( c.Key )
                        ),
                        Constant( c.Value ) ) )
                .ToArray() );
    }

    public void Add( LabelTarget jumpLabel, int stateId )
    {
        _jumpCases.Add( jumpLabel, stateId );
    }
}
