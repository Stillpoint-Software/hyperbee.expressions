using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;
using static System.Formats.Asn1.AsnWriter;

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

        public static string Try( int stateId ) => $"__try<{stateId}>";
        public static string Exception( int stateId) => $"__ex<{stateId}>";

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
            Nodes = _states.GetNodes(),   // TODO: now wrong
            Scopes = _states.Scopes,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = _variables
        };
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState, out NodeScope scope,
        ParameterExpression resultVariable = null, 
        Action<NodeExpression> init = null, 
        bool newScope = false,
        bool captureVisit = true )
    {
        // Create a new state for the branch
        var branchState = _states.AddBranchState();

        init?.Invoke( branchState );

        scope = newScope ? _states.EnterNodeScope() : null; 

        VisitInternal( expression, captureVisit );

        if ( newScope ) _states.ExitNodeScope();

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
            IfTrue = VisitBranch( node.IfTrue, joinState, out _, resultVariable ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState, out _, resultVariable )
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
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState, out _, resultVariable );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues], // TODO: Visit these because they could be async
                VisitBranch( switchCase.Body, joinState, out _, resultVariable )
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

        var tryStateVariable = Expression.Variable( typeof( int ), VariableName.Try( sourceState.StateId ) );
        _variables.Add( tryStateVariable );

        var exceptionVariable = Expression.Variable( typeof(object), VariableName.Exception( sourceState.StateId ) );
        _variables.Add( exceptionVariable );

        var nodeScope = _states.EnterNodeScope();

        var tryCatchTransition = new TryCatchTransition
        {
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            TryNode = VisitBranch( node.Body, joinState, out _, resultVariable, newScope: false ),
            NodeScope = nodeScope
        };

        _states.ExitNodeScope();

        for ( var index = 0; index < node.Handlers.Count; index++ )
        {
            var catchBlock = node.Handlers[index];
            tryCatchTransition.AddCatchBlock(
                catchBlock,
                VisitBranch( catchBlock.Body, joinState, out _ ),
                index );
        }

        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitBranch( node.Finally, joinState, out _ );
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
            BodyNode = VisitBranch( node.Body, joinState, out _, resultVariable, InitializeLabels )
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        // TODO: This seems wrong, I shouldn't have to cast to GotoTransition (maybe all types of a TargetNode?)
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

        var completionState = VisitBranch( node.Target, joinState, out _, resultVariable, captureVisit: false );

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

        _states.AddJumpCase( completionState.NodeLabel, sourceState.StateId );

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
        private int _nodeCount;
        private readonly Stack<int> _scopeIndexes;
        private readonly int _initialCapacity;

        public List<NodeScope> Scopes { get; }

        public StateContext( int initialCapacity )
        {
            _initialCapacity = initialCapacity;
            _scopeIndexes = new Stack<int>( _initialCapacity );
            _scopeIndexes.Push( 0 );

            Scopes =
            [
                new NodeScope( 0, null, _initialCapacity )
            ];
        }

        public NodeScope CurrentScope => Scopes[_scopeIndexes.Peek()];
        
        public List<NodeExpression> GetNodes() =>
            CurrentScope.Nodes;
        
        public NodeExpression GetBranchTailState() =>
            CurrentScope.GetBranchTailState();
        
        public NodeExpression AddState() =>
            CurrentScope.AddState( _nodeCount++ );
        
        public NodeExpression AddBranchState() =>
            CurrentScope.AddBranchState( _nodeCount++ );
        
        public NodeExpression EnterBranchState( out NodeExpression sourceState ) =>
            CurrentScope.EnterBranchState( AddState(), out sourceState );
        
        public void ExitBranchState( NodeExpression sourceState, Transition transition )
        {
            CurrentScope.ExitBranchState( sourceState, transition );
        }
        public void AddJumpCase( LabelTarget label, int stateId )
        {
            CurrentScope.AddJumpCase( label, stateId );
        }

        public NodeScope EnterNodeScope()
        {
            var tailState = CurrentScope.GetBranchTailState();
            var scope = new NodeScope( Scopes.Count, tailState, _scopeIndexes.Peek(), _initialCapacity );
            Scopes.Add( scope );
            _scopeIndexes.Push( scope.Id );
            return scope;
        }

        public NodeScope ExitNodeScope() => 
            Scopes[_scopeIndexes.Pop()];
    }
}

public class NodeScope
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public List<NodeExpression> Nodes { get; init; }
    private readonly Dictionary<LabelTarget, int> _jumpCases;
    public  Stack<NodeExpression> JoinStates { get; init; }

    private NodeExpression _tailState;

    public NodeScope( int id, NodeExpression tailState, int? parentId = null, int initialCapacity = 8 )
    {
        Id = id;
        ParentId = parentId;
        Nodes = new List<NodeExpression>( initialCapacity );

        _tailState = tailState;
        _jumpCases = new Dictionary<LabelTarget, int>( initialCapacity );
        JoinStates = new Stack<NodeExpression>( initialCapacity );
    }

    public NodeExpression AddState( int id )
    {
        var stateNode = new NodeExpression( id );

        if(Nodes.Count == 0)
            _tailState = stateNode;  // TODO: This seems wrong

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

    public void AddJumpCase( LabelTarget label, int stateId )
    {
        _jumpCases.Add( label, stateId );
    }

    public IReadOnlyDictionary<LabelTarget, int> GetJumpCases() => _jumpCases;
}
