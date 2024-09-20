using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public enum StateBlockType
{
    Default,

    Await,
    Result,

    Assign,
    Block,
    Constant,


    Goto,
    Break,
    Continue,
    Return,

    Conditional,
    Loop,
    Label,
    Try,
    Switch
}

public class StateBlock
{
    public int Id { get; set; }
    public StateBlockType Type { get; set; }
    public List<Expression> Expressions { get; set; }



    // For general flow
    public int? NextStateId { get; set; }


    // For Async/Await flow
    public int? AwaitResultStateId { get; set; }

    // Condition-specific fields
    public int? TrueStateId { get; set; }
    public int? FalseStateId { get; set; }

    // Goto-specific fields
    public int? ContinueStateId { get; set; }
    public int? BreakStateId { get; set; }
    
    public int? GotoTargetId { get; set; }

    // Switch-specific fields
    public Dictionary<object, int> CaseStateIds { get; set; }

    // For TryCatch expressions
    public int? TryStateId { get; set; }
    public Dictionary<object, int> CatchStateIds { get; set; }
    public int? FinallyStateId { get; set; }
    public int? FaultStateId { get; set; }

    public StateBlock( int id, StateBlockType type, Expression expression = null )
    {
        Id = id;
        Type = type;
        Expressions = expression == null ? [] : [expression];
        CaseStateIds = []; 
        CatchStateIds = [];
    }
}

public class AwaitExpressionSplitter : ExpressionVisitor
{
    private readonly List<StateBlock> _stateBlocks = [];
    private int _currentStateId = 1;  // Start state ID from 1
    private readonly Dictionary<LabelTarget, int> _labelMappings = [];  // Label mappings for Goto
    private readonly Stack<int> _reservedStateIdStack = [];  // Stack to track reserved state IDs

    public IReadOnlyList<StateBlock> StateBlocks => _stateBlocks.AsReadOnly();

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is not AwaitExpression awaitExpression )
            return base.VisitExtension( node );

        /*
         * I wonder if we use different expressions here?
         *   The user/developer use the basic AwaitExpression that handles Type Correctly (don't need ReturnTask and ReturnType anymore)
         *   But we make different expressions for Await and Result that understand the unwrapped type
         *      - This could also hold variable references for the awaiter and result
         */
        var lastBlock = _stateBlocks.Last();
        var awaitState = CreateStateBlock( StateBlockType.Await, awaitExpression );
        lastBlock.NextStateId = awaitState.Id;
        Visit( awaitExpression.Target );

        // TODO: Create Result Expression?
        var awaitResultState = new StateBlock( GetNextStateId(), StateBlockType.Result, null ); 
        _stateBlocks.Add( awaitResultState );
        awaitState.AwaitResultStateId = awaitResultState.Id;

        return node;
    }


    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var conditionState = VisitWithAwait( StateBlockType.Conditional, node.Test, node );

        // Visit true and false branches with their reserved state IDs
        conditionState.TrueStateId = VisitWithState( node.IfTrue );

        var hasFalse = !(node.IfFalse is DefaultExpression && node.IfFalse.Type == typeof(void));
        conditionState.FalseStateId = hasFalse ? VisitWithState( node.IfFalse ) : null;

        return node;
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        var type = node.Kind switch
        {
            GotoExpressionKind.Break => StateBlockType.Break,
            GotoExpressionKind.Continue => StateBlockType.Continue,
            GotoExpressionKind.Return => StateBlockType.Return,
            GotoExpressionKind.Goto => StateBlockType.Goto,
            _ => throw new ArgumentOutOfRangeException()
        };

        // var gotoState = CreateStateBlock( type, node );
        // Visit( node.Value );
        var gotoState = VisitWithAwait( type, node.Value, node );

        // Map the Goto target to its pre-visited label
        var labelState = CreateLabelBlock( node.Target );
        gotoState.GotoTargetId = labelState.Id;

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        // Create a Try state block
        var tryState = CreateStateBlock( StateBlockType.Try, node );

        // Visit the try body
        var bodyId = VisitWithState( node.Body );

        tryState.TryStateId = bodyId;

        // Visit catch blocks with unique state IDs for each
        foreach ( var catchBlock in node.Handlers )
        {
            var catchId = VisitWithState( catchBlock.Body );
            Visit( catchBlock.Filter );
            Visit( catchBlock.Variable );

            tryState.CatchStateIds[catchBlock.Body] = catchId;
        }

        // Visit the finally-block, if it exists
        if ( node.Finally != null )
        {
            var finalId = VisitWithState( node.Finally );
            tryState.FinallyStateId = finalId;
        }

        // Visit the fault-block, if it exists
        if ( node.Fault != null )
        {
            var faultId = VisitWithState( node.Fault );
            tryState.FaultStateId = faultId;
        }

        return node;
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        // Create a label state block and map it to the label target
        CreateLabelBlock( node.Target );
        return node;
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        // Create the loop state block
        var loopState = CreateStateBlock( StateBlockType.Loop, node );
        loopState.ContinueStateId = CreateLabelBlock( node.ContinueLabel ).Id;
        loopState.BreakStateId = CreateLabelBlock( node.BreakLabel ).Id;

        VisitWithState( node.Body );

        return node;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // This might split into multiple blocks if there are awaits
        var blockState = CreateStateBlock( StateBlockType.Block, node );

        // Visit all expressions inside the block

        var currentBlock = blockState;
        /*
         * For each await found there will be a two new blocks create await and return
         * The await block should do everything up to the await and result gets the result.
         *
         * So this:
         *    [exp1, exp2, exp3]
         *    [exp1, exp2(await), exp3]
         *    [exp1, exp2, exp3(await)]
         * becomes:
         *    [exp1, exp2, exp3]
         *    [exp1 -> exp2Await -> exp2Result -> exp3]
         *    [exp1, exp2 -> exp3Await -> exp3Result]
         *
         */
        foreach ( var expr in node.Expressions )
        {
            //Visit( expr );
            VisitWithAwait( expr, resultBlock =>
            {
                currentBlock = CreateStateBlock( StateBlockType.Block, expr );
                resultBlock.NextStateId = currentBlock.Id;
            } );
            

            currentBlock.Expressions.Add( expr );
        }

        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var assignState = CreateStateBlock( StateBlockType.Assign, node );

        // Visit the left and right expressions of the assignment
        Visit( node.Left );
        Visit( node.Right );

        // VisitWithAwait( node.Right, resultBlock =>
        // {
        //     var right = CreateStateBlock( StateBlockType.Block, node.Right );
        //     resultBlock.NextStateId = right.Id;
        // } );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        // Create a state block for the switch expression
        var switchState = CreateStateBlock( StateBlockType.Switch, node );

        // Handle the default case if it exists
        if ( node.DefaultBody != null )
        {
            var defaultStateId = VisitWithState( node.DefaultBody );
            switchState.CaseStateIds[node.DefaultBody] = defaultStateId;
        }

        // Reserve and visit case bodies
        foreach ( var switchCase in node.Cases )
        {
            var caseStateId = VisitWithState( switchCase.Body );

            foreach ( var testValue in switchCase.TestValues )
            {
                Visit( testValue );
                switchState.CaseStateIds[testValue] = caseStateId;
            }
        }

        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        //CreateStateBlock( StateBlockType.Constant, node );
        return base.VisitConstant( node );
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        return base.VisitInvocation( node );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        return base.VisitMethodCall( node );
    }

    protected override SwitchCase VisitSwitchCase( SwitchCase node )
    {
        return base.VisitSwitchCase( node );
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        // if ( node.NodeType == ExpressionType.Throw )
        // {
        //     Visit( node.Operand );
        // }

        return base.VisitUnary( node );
    }

    private StateBlock CreateLabelBlock( LabelTarget label )
    {
        if ( _labelMappings.TryGetValue( label, out var id ) )
        {
            return _stateBlocks.First( x => x.Id == id );
        }

        var reservedId = GetNextStateId();
        var block = new StateBlock( reservedId, StateBlockType.Label );
        _labelMappings[label] = reservedId;
        _stateBlocks.Add( block );
        return block;
    }

    private StateBlock CreateStateBlock( StateBlockType type, Expression node = null )
    {
        if ( _reservedStateIdStack.TryPeek( out var stateId ) )
        {
            var block = _stateBlocks.FirstOrDefault( x => x.Id == stateId );
            if ( block != null )
                return block;
        }

        stateId = GetNextStateId();

        var newBlock = new StateBlock( stateId, type, node );
        _stateBlocks.Add( newBlock );

        return newBlock;
    }

    private int GetNextStateId()
    {
        return _currentStateId++;
    }

    private int VisitWithState( Expression node )
    {
        try
        {
            var reservedId = GetNextStateId();
            _reservedStateIdStack.Push( reservedId );
            Visit( node );
            return reservedId;
        }
        finally
        {
            _reservedStateIdStack.Pop();
        }
    }

    private StateBlock VisitWithAwait( StateBlockType type, Expression childNode, Expression node )
    {
        // Visit Child First
        Visit( childNode );

        // If last block was a result block, then set next state
        var resultBlock = _stateBlocks.Last();

        // Create a new state block for the current expression
        var block = CreateStateBlock( type, node );

        // If last block was a result block, then set next state
        //var resultBlock = _stateBlocks.Last();
        if ( resultBlock.Type == StateBlockType.Result )
            resultBlock.NextStateId = block.Id;

        return block;
    }


    private Expression VisitWithAwait( Expression node, Action<StateBlock> handleAwait )
    {
        // Visit Child First
        var updatedNode = Visit( node );

        var resultBlock = _stateBlocks.Last();
        if ( resultBlock.Type != StateBlockType.Result )
            return updatedNode;

        handleAwait( resultBlock );

        return updatedNode;
    }



}




/*
public record SpitResults(
    List<StateBlock> StateBlocks,
    Dictionary<ParameterExpression, ParameterExpression> HoistedVariables );

public class AwaitExpressionSplitter2 : ExpressionVisitor
{
    private readonly List<StateBlock> _stateBlocks = [];
    private readonly Dictionary<ParameterExpression, ParameterExpression> _hoistedVariables = new();
    private readonly Stack<HashSet<ParameterExpression>> _variableScopes = new();
    private int _currentStateId;

    // Keeps track of variables that need to be hoisted
    private readonly HashSet<ParameterExpression> _variablesToHoist = [];

    // Stores the mapping from labels to state IDs (for loops, conditionals, etc.)
    private readonly Stack<int> _breakStateIds = new();
    private readonly Stack<int> _continueStateIds = new();
    private readonly Stack<int> _returnStateIds = new();

    public SpitResults Split( Expression rootExpression )
    {
        // Start with a global scope
        _variableScopes.Push( [] );

        // Start with the initial state
        var initialStateId = GetNextStateId();
        StartNewState( initialStateId );

        Visit( rootExpression );

        // Finalize the last state
        FinalizeCurrentState();

        // Pop the global scope
        _variableScopes.Pop();

        return new SpitResults( _stateBlocks, _hoistedVariables);
    }

    private StateBlock _currentStateBlock;
    private List<Expression> _currentStateExpressions = [];

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Push a new scope for variables
        var currentScope = new HashSet<ParameterExpression>();
        _variableScopes.Push( currentScope );

        // Add variables to current scope
        foreach ( var variable in node.Variables )
        {
            currentScope.Add( variable );
        }

        // Visit expressions
        foreach ( var expr in node.Expressions )
        {
            Visit( expr );
        }

        // Pop the scope
        _variableScopes.Pop();

        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AwaitExpression awaitExpression )
        {
            HandleAwaitExpression( awaitExpression );
            return node;
        }

        return base.VisitExtension( node );
    }

    private void HandleAwaitExpression(AwaitExpression awaitExpression, BinaryExpression? binaryExpression = null )
    {
        // Check if the await is part of an assignment
        if ( binaryExpression?.NodeType == ExpressionType.Assign )
        {
            // The await expression is on the right side of an assignment
            var left = Visit(binaryExpression.Left);
            var right = Visit(awaitExpression.Target);

            // Hoist variables used in the assignment
            CollectVariables(left);
            CollectVariables(right);

            // Add the expression to get the task (before await)
            _currentStateExpressions.Add(Expression.Assign(left, right));

            // Finalize the current state before the await
            FinalizeCurrentState();

            // Prepare for the after-await state
            var afterAwaitStateId = GetNextStateId();

            // Set the NextStateId of before-await state to after-await state
            _currentStateBlock.NextStateId = afterAwaitStateId;

            // Start a new state for after the await
            StartNewState(afterAwaitStateId);

            // Handle getting the result after await
            var getResultExpression = awaitExpression.Reduce();

            // Hoist variables used in getResultExpression
            CollectVariables(getResultExpression);

            // Assign the result to the variable
            var assignResult = Expression.Assign(left, getResultExpression);

            _currentStateExpressions.Add(assignResult);
        }
        else
        {
            // The await expression is not part of an assignment
            // Proceed as before
            var awaitTarget = Visit(awaitExpression.Target);

            // Hoist variables used in awaitTarget
            CollectVariables(awaitTarget);

            // Add the expression to get the task (before await)
            _currentStateExpressions.Add(awaitTarget);

            // Finalize the current state before the await
            FinalizeCurrentState();

            // Prepare for the after-await state
            var afterAwaitStateId = GetNextStateId();

            // Set the NextStateId of before-await state to after-await state
            _currentStateBlock.NextStateId = afterAwaitStateId;

            // Start a new state for after the await
            StartNewState(afterAwaitStateId);

            // Handle getting the result after await
            var getResultExpression = awaitExpression.Reduce();

            // Hoist variables used in getResultExpression
            CollectVariables(getResultExpression);

            // Add the getResultExpression to the after-await state
            _currentStateExpressions.Add(getResultExpression);
        }
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (!IsVariableInScope(node))
        {
            // Variable is from an outer scope or needs to be hoisted
            if (!_hoistedVariables.ContainsKey(node))
            {
                var hoistedVariable = Expression.Parameter(node.Type, node.Name + "_" + node.Type.Name);
                _hoistedVariables[node] = hoistedVariable;
            }

            _variablesToHoist.Add(node);

            return _hoistedVariables[node];
        }

        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var left = Visit( node.Left );
        var right = Visit( node.Right );

        if( node.Right is AwaitExpression awaitExpression )
        {
            HandleAwaitExpression( awaitExpression, node );
        }

        // If assigning to a variable, check if it needs to be hoisted
        if ( node.NodeType == ExpressionType.Assign && node.Left is ParameterExpression leftParam )
        {
            if ( !IsVariableInScope( leftParam ) )
            {
                if ( !_hoistedVariables.ContainsKey( leftParam ) )
                {
                    var hoistedVariable = Expression.Parameter( leftParam.Type, leftParam.Name + "_" + leftParam.Type.Name );
                    _hoistedVariables[leftParam] = hoistedVariable;
                }

                _variablesToHoist.Add( leftParam );

                left = _hoistedVariables[leftParam];
            }
        }

        var newNode = node.Update( left, node.Conversion, right );
        _currentStateExpressions.Add( newNode );
        return newNode;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        // Visit the test expression
        var test = Visit( node.Test );
        CollectVariables( test );

        // Finalize the current state with the test expression
        _currentStateExpressions.Add( test );
        FinalizeCurrentState();

        var testStateId = _currentStateBlock.StateId;

        // Prepare to visit the true branch
        var trueBranchStateId = GetNextStateId();
        StartNewState( trueBranchStateId );
        Visit( node.IfTrue );
        FinalizeCurrentState();

        // Set the TrueBranchStateId of the test state
        _stateBlocks.First( s => s.StateId == testStateId ).TrueBranchStateId = trueBranchStateId;

        // Handle the false branch if it exists
        if ( node.IfFalse is not DefaultExpression )
        {
            var falseBranchStateId = GetNextStateId();
            StartNewState( falseBranchStateId );
            Visit( node.IfFalse );
            FinalizeCurrentState();

            // Set the FalseBranchStateId of the test state
            _stateBlocks.First( s => s.StateId == testStateId ).FalseBranchStateId = falseBranchStateId;

            // Determine the next state ID after the conditional
            var afterConditionalStateId = GetNextStateId();

            // Set NextStateId for the last states of true and false branches
            SetNextStateForLastState( trueBranchStateId, afterConditionalStateId );
            SetNextStateForLastState( falseBranchStateId, afterConditionalStateId );

            // Start the after-conditional state
            StartNewState( afterConditionalStateId );
        }
        else
        {
            // Only true branch exists
            var afterConditionalStateId = GetNextStateId();
            SetNextStateForLastState( trueBranchStateId, afterConditionalStateId );

            // Start the after-conditional state
            StartNewState( afterConditionalStateId );
        }

        return node;
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        // Start the loop body state
        var loopBodyStateId = GetNextStateId();
        StartNewState( loopBodyStateId );

        // Push break and continue targets
        var afterLoopStateId = GetNextStateId(); // State after loop
        _breakStateIds.Push( afterLoopStateId );
        _continueStateIds.Push( loopBodyStateId ); // Loop back to body

        // Visit the loop body
        Visit( node.Body );

        // After visiting the body, set NextStateId to loop back to the loop body state
        FinalizeCurrentState();
        SetNextStateForLastState( _currentStateBlock.StateId, loopBodyStateId );

        // Pop break and continue targets
        _breakStateIds.Pop();
        _continueStateIds.Pop();

        // Start the after-loop state
        StartNewState( afterLoopStateId );

        return node;
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        node.NodeType == 
        if ( node.Kind == GotoExpressionKind.Break )
        {
            // Finalize the current state and set its NextStateId to the break target
            FinalizeCurrentState();
            var breakStateId = _breakStateIds.Peek();
            _currentStateBlock.NextStateId = breakStateId;

            // Start a new state to prevent further expressions from being added to the current state
            StartNewState();

            return node;
        }

        if ( node.Kind == GotoExpressionKind.Continue )
        {
            // Finalize the current state and set its NextStateId to the continue target
            FinalizeCurrentState();
            var continueStateId = _continueStateIds.Peek();
            _currentStateBlock.NextStateId = continueStateId;

            // Start a new state to prevent further expressions from being added to the current state
            StartNewState();

            return node;
        }

        if ( node.Kind == GotoExpressionKind.Return )
        {
            // Finalize the current state and set its NextStateId to the continue target
            FinalizeCurrentState();
            var continueStateId = _continueStateIds.Peek();
            _currentStateBlock.NextStateId = continueStateId;

            // Start a new state to prevent further expressions from being added to the current state
            StartNewState();

            return node;
        }
        throw new NotSupportedException( $"GotoExpression of kind '{node.Kind}' is not supported." );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        // Visit the instance and arguments
        var instance = Visit( node.Object );
        var arguments = node.Arguments.Select( Visit ).ToList();

        // Hoist variables used
        CollectVariables( instance );
        foreach ( var arg in arguments )
        {
            CollectVariables( arg );
        }

        var newNode = node.Update( instance, arguments );
        _currentStateExpressions.Add( newNode );

        return newNode;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        // Visit the expression and arguments
        var expression = Visit( node.Expression );
        var arguments = node.Arguments.Select( Visit ).ToList();

        // Hoist variables used
        CollectVariables( expression );
        foreach ( var arg in arguments )
        {
            CollectVariables( arg );
        }

        var newNode = node.Update( expression, arguments );
        _currentStateExpressions.Add( newNode );

        return newNode;
    }

    private bool IsVariableInScope( ParameterExpression variable )
    {
        foreach ( var scope in _variableScopes )
        {
            if ( scope.Contains( variable ) )
                return true;
        }
        return false;
    }

    private int GetNextStateId()
    {
        return _currentStateId++;
    }

    private void CollectVariables( Expression expr )
    {
        if ( expr == null )
            return;

        var visitor = new VariableCollector( _variablesToHoist, _variableScopes, _hoistedVariables );
        visitor.Visit( expr );
    }

    private class VariableCollector : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _variablesToHoist;
        private readonly Stack<HashSet<ParameterExpression>> _variableScopes;
        private readonly Dictionary<ParameterExpression, ParameterExpression> _hoistedVariables;

        public VariableCollector( HashSet<ParameterExpression> variablesToHoist, Stack<HashSet<ParameterExpression>> variableScopes, Dictionary<ParameterExpression, ParameterExpression> hoistedVariables )
        {
            _variablesToHoist = variablesToHoist;
            _variableScopes = variableScopes;
            _hoistedVariables = hoistedVariables;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( !IsVariableInScope( node ) )
            {
                if ( !_hoistedVariables.ContainsKey( node ) )
                {
                    var hoistedVariable = Expression.Parameter( node.Type, node.Name + "_" + node.Type.Name );
                    _hoistedVariables[node] = hoistedVariable;
                }

                _variablesToHoist.Add( node );
            }

            return node;
        }

        private bool IsVariableInScope( ParameterExpression variable )
        {
            foreach ( var scope in _variableScopes )
            {
                if ( scope.Contains( variable ) )
                    return true;
            }
            return false;
        }
    }

    private void StartNewState( int? stateId = null )
    {
        _currentStateExpressions = [];
        _currentStateBlock = new StateBlock
        {
            StateId = stateId ?? GetNextStateId(),
            BlockExpression = null,
            NextStateId = null
        };
        _stateBlocks.Add( _currentStateBlock );
        //_currentStateId = _currentStateBlock.StateId;
    }

    private void FinalizeCurrentState()
    {
        if ( _currentStateExpressions.Count > 0 )
        {
            _currentStateBlock.BlockExpression = Expression.Block( _currentStateExpressions );
        }
        else
        {
            _currentStateBlock.BlockExpression = Expression.Empty();
        }
    }

    private void SetNextStateForLastState( int stateId, int nextStateId )
    {
        var stateBlock = _stateBlocks.FirstOrDefault( s => s.StateId == stateId );
        if ( stateBlock != null )
        {
            stateBlock.NextStateId = nextStateId;
        }
        else
        {
            throw new InvalidOperationException( $"State with ID {stateId} not found." );
        }
    }
}
*/

/*
public class StateBlock
{
    public int StateId { get; set; }
    public Expression BlockExpression { get; set; }
    public int? NextStateId { get; set; }
    public int? TrueBranchStateId { get; set; }
    public int? FalseBranchStateId { get; set; }
    public Dictionary<object, int> CaseStateIds { get; set; }
    public int? DefaultStateId { get; set; }

    public bool IsFinalState => !NextStateId.HasValue && !TrueBranchStateId.HasValue && !FalseBranchStateId.HasValue;

    public bool IsAwaitState => ContainsAwaitExpression( BlockExpression );

    private bool ContainsAwaitExpression( Expression expression )
    {
        var visitor = new AwaitExpressionDetector();
        visitor.Visit( expression );
        return visitor.ContainsAwait;
    }

    private class AwaitExpressionDetector : ExpressionVisitor
    {
        public bool ContainsAwait { get; private set; }

        protected override Expression VisitExtension( Expression node )
        {
            if ( node is AwaitExpression )
            {
                ContainsAwait = true;
                return node;
            }

            return base.VisitExtension( node );
        }
    }
}

*/
