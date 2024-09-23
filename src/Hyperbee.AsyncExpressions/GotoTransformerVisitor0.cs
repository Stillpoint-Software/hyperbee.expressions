using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions;

public class GotoTransformerVisitor0 : ExpressionVisitor
{
    private readonly List<StateNode0> _states = [];
    private int _continuationCounter;
    private int _labelCounter;
    private StateNode0 _currentState;
    private readonly Stack<StateNode0> _finalNodes = [];
    private readonly Dictionary<LabelTarget, int> _labelMappings = [];

    public List<StateNode0> Transform( Expression expression )
    {
        // Initialize the first state (n0)
        _currentState = new StateNode0( _labelCounter++ );
        _states.Add( _currentState );

        Visit( expression );

        return _states;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        foreach ( var expr in node.Expressions )
        {
            Visit( expr );
        }

        return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        // Always lift Condition to current state
        Visit( node.Test );

        var conditionalNode = _currentState;

        // Push the final node to stack for later convergence
        var hasFalse = node.IfFalse is not DefaultExpression;
        var ifTrueNode = new StateNode0( _labelCounter++ );
        var ifFalseNode = hasFalse ? new StateNode0( _labelCounter++ ) : null;
        var finalNode = new StateNode0( _labelCounter++ );

        conditionalNode.IfTrue = ifTrueNode;
        conditionalNode.IfFalse = ifFalseNode;

        _states.Add( finalNode );
        _finalNodes.Push( finalNode );

        // Process IfTrue branch
        ProcessBranch( node.IfTrue, ifTrueNode, finalNode );

        // Process IfFalse branch
        if ( hasFalse )
            ProcessBranch( node.IfFalse, ifFalseNode, finalNode );

        // Pop the final node and set it as current state
        _currentState = _finalNodes.Pop();

        return node;

    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        // Always lift SwitchValue to current state
        Visit( node.SwitchValue );

        var switchNode = _currentState;
        switchNode.Cases = [];

        // Create the final node where all cases will converge
        var finalNode = new StateNode0( _labelCounter++ );
        _states.Add( finalNode );
        _finalNodes.Push( finalNode );

        // Process each case
        List<SwitchCase> cases = [];
        foreach ( var switchCase in node.Cases )
        {
            var caseNode = new StateNode0( _labelCounter++ );
            ProcessBranch( switchCase.Body, caseNode, finalNode );

            switchNode.Cases.Add( caseNode );

            // Add case label to the state
            cases.Add( SwitchCase( Goto( caseNode.Label ), switchCase.TestValues ) );

        }

        // Handle default case if present
        Expression defaultBody = null;
        if ( node.DefaultBody != null )
        {
            var defaultNode = new StateNode0( _labelCounter++ );

            // TODO: Can't use `ProcessBranch` because GoTos are add differently

            _states.Add( defaultNode );
            _currentState = defaultNode;

            Visit( node.DefaultBody );

            //_currentState.Expressions.Add( Goto( finalNode.Label ) );
            defaultBody = Goto( finalNode.Label );
            _currentState.Final = finalNode;
        }

        var gotoSwitch = Switch(
            node.SwitchValue,
            defaultBody,
            [.. cases] );
        switchNode.Expressions.Add( gotoSwitch );


        // Pop the final node and set it as current state
        _currentState = _finalNodes.Pop();

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {        
        // Always lift body to current state
        Visit( node.Body );

        var tryNode = _currentState;
        tryNode.Catches = [];

        var hasFinally = node.Finally != null;

        // TODO: fault block
        //var hasFault = node.Fault != null;
        //var faultNode = hasFault ? new StateNode( _labelCounter++ ) : null;
        var finallyNode = hasFinally ? new StateNode0( _labelCounter++ ) : null;
        var finalNode = new StateNode0( _labelCounter++ );

        _states.Add( finalNode );
        _finalNodes.Push( finalNode );

        // Process each case
        List<CatchBlock> catches = [];
        foreach ( var catchBlock in node.Handlers )
        {
            var catchNode = new StateNode0( _labelCounter++ );
            tryNode.Catches.Add( catchNode );

            // TODO: catchBlock.Filter
            // Add case label to the state
            // TODO: verify node.Body.Type as the correct type
            catches.Add( Catch( catchBlock.Test, Goto( catchNode.Label, node.Body.Type ) ) );  

            ProcessBranch( catchBlock.Body, catchNode, finalNode );
        }

        // Visit the finally-block, if it exists
        Expression finallyBody = null;
        if ( finallyNode != null )
        {
            var defaultNode = new StateNode0( _labelCounter++ );

            // TODO: Can't use `ProcessBranch` because GoTos are add differently

            _states.Add( defaultNode );
            _currentState = defaultNode;

            Visit( node.Finally );

            finallyBody = Goto( finalNode.Label );
            _currentState.Final = finalNode;
        }

        // Visit the fault-block, if it exists
        // Expression faultBody = null;
        // if ( faultNode != null )
        // {
        // }

        // TODO replace?
        var newTry = TryCatchFinally(
            node.Body,
            finallyBody,
            [..catches]
        );
        tryNode.Expressions.Add( newTry );

        // Pop the final node and set it as current state
        _currentState = _finalNodes.Pop();

        return node;

    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        // var loopNode = _currentState;
        //
        // var breakNode = new StateNode( _labelCounter++ ); // { Label = node.BreakLabel };
        // var finalNode = new StateNode( _labelCounter++ ); 
        // _states.Add( finalNode );
        // _finalNodes.Push( finalNode );
        //
        // var loopBodyNode = new StateNode( _labelCounter++ );
        // _states.Add( loopBodyNode );
        // _currentState = loopBodyNode;
        //
        // Visit( node.Body );
        //
        // _currentState.Expressions.Add( Goto( finalNode.Label ) );
        // _currentState.Final = finalNode;
        //
        // loopNode.Continue = loopBodyNode;
        // loopNode.Break = breakNode;
        // breakNode.Final = finalNode;
        //
        //
        // _currentState = _finalNodes.Pop();

        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is not AwaitExpression awaitExpression )
        {
            _currentState.Expressions.Add( node );
            return node;
        }

        var stateId = _continuationCounter++;
        var awaitNode = new StateNode0( _labelCounter++ ) { ContinuationId = stateId };
        var finalNode = new StateNode0( _labelCounter++ ) { ContinuationId = stateId };

        _currentState.Await = awaitNode;

        _states.Add( finalNode );
        _finalNodes.Push( finalNode );

        ProcessBranch( awaitExpression.Target, awaitNode, finalNode );

        // build awaiter
        /*
           awaiter8 = GetRandom().GetAwaiter();
           if (!awaiter8.IsCompleted)
           {
               num = (<>1__state = 0);
               <>u__1 = awaiter8;
               <Main>d__0 stateMachine = this;
               <>t__builder.AwaitUnsafeOnCompleted(ref awaiter8, ref stateMachine);
               return;
           }
           goto IL_00fe;
         */

        // build awaiter continue:
        /*
           awaiter8 = <>u__1;
           <>u__1 = default(TaskAwaiter<int>);
           num = (<>1__state = -1);
           goto IL_00fe;
        */

        // Pop the final node and set it as current state
        _currentState = _finalNodes.Pop();

        return node;
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        foreach ( var nodeArgument in node.Arguments )
        {
            Visit( nodeArgument );
        }

        _currentState.Expressions.Add( node );

        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        _currentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        _currentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        _currentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        // Handle goto if necessary
        _currentState.Expressions.Add( node );

        var gotoNode = new StateNode0( _labelCounter++ );
        _states.Add( gotoNode );
        _currentState.Goto = gotoNode;
        gotoNode.Final = CreateLabelBlock( node.Target );

        return node;
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        if(node.NodeType == ExpressionType.Throw)
        {
            _currentState.Expressions.Add( node );
            return node;
        }

        return base.VisitUnary( node );
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        // Create a label state block and map it to the label target
        CreateLabelBlock( node.Target );
        return node;
    }

    private StateNode0 CreateLabelBlock( LabelTarget label )
    {
        if ( _labelMappings.TryGetValue( label, out var id ) )
        {
            return _states.First( x => x.BlockId == id );
        }

        var block = new StateNode0( _labelCounter++ );
        _labelMappings[label] = block.BlockId;
        _states.Add( block );
        return block;
    }

    private void ProcessBranch( Expression expression, StateNode0 stateNode, StateNode0 final )
    {
        _states.Add( stateNode );
        _currentState = stateNode;

        //Console.WriteLine("Before: {0}", _currentState.Label.Name);

        Visit( expression );

        //Console.WriteLine( "After: {0}", _currentState.Label.Name );

        // TODO: This Add doesn't work for everyone
        _currentState.Expressions.Add( Goto( final.Label ) );
        _currentState.Final = final;
    }

    public void PrintStateMachine()
    {
        PrintStateMachine( _states );
    }

    public static void PrintStateMachine( List<StateNode0> states )
    {
        foreach ( var state in states )
        {
            Console.WriteLine( $"{state.Label}: {(state.ContinuationId != null ? $" (state: {state.ContinuationId})" : string.Empty)}" );
            foreach ( var expr in state.Expressions )
            {
                Console.WriteLine( $"\t{ExpressionToString( expr )}" );
            }

            if ( state.Cases != null )
            {
                foreach ( var caseNode in state.Cases )
                {
                    Console.WriteLine( $"\tCase -> {caseNode.Label}" );
                }
            }
            if ( state.Await != null )
                Console.WriteLine( $"\tAwait -> {state.Await.Label}" );
            if ( state.IfTrue != null )
                Console.WriteLine( $"\tIfTrue -> {state.IfTrue.Label}" );
            if ( state.IfFalse != null )
                Console.WriteLine( $"\tIfFalse -> {state.IfFalse.Label}" );
            if ( state.Final != null )
                Console.WriteLine( $"\tFinal -> {state.Final.Label}" );
            if( state.Goto != null )
                Console.WriteLine( $"\tGoto -> {state.Goto.Label}" );
            if ( state.IsTerminal )
                Console.WriteLine( "\tTerminal" );
            Console.WriteLine();
        }

        return;


        static string GetBinaryOperator( ExpressionType nodeType )
        {
            return nodeType switch
            {
                ExpressionType.Assign => "=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.LessThan => "<",
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                _ => nodeType.ToString()
            };
        }

        static string ExpressionToString( Expression expr )
        {
            switch ( expr )
            {
                case MethodCallExpression m:
                    var args = string.Join( ", ", m.Arguments.Select( ExpressionToString ) );
                    return $"{m.Method.Name}({args})";
                case BinaryExpression b:
                    return $"{ExpressionToString( b.Left )} {GetBinaryOperator( b.NodeType )} {ExpressionToString( b.Right )}";
                case ParameterExpression p:
                    return p.Name;
                case ConstantExpression c:
                    return c.Value?.ToString() ?? "empty";
                case GotoExpression g:
                    return $"goto {g.Target.Name}";
                case UnaryExpression u:
                    return $"{u.NodeType} {ExpressionToString( u.Operand )}";
                default:
                    return expr.ToString();
            }
        }
    }
}

public class StateNode0
{
    public int BlockId { get; }
    public LabelTarget Label { get; set; }
    public List<Expression> Expressions { get; } = [];
    public StateNode0 Final { get; set; }

    // Condition-specific fields
    public StateNode0 IfTrue { get; set; }
    public StateNode0 IfFalse { get; set; }

    // Switch-specific fields
    public List<StateNode0> Cases { get; set; }

    // For Async/Await fields
    public int? ContinuationId { get; set; }
    public StateNode0 Await { get; set; }

    // Goto-specific fields
    public StateNode0 Continue { get; set; }
    public StateNode0 Break { get; set; }
    public StateNode0 Goto { get; set; }

    // TryCatch-specific fields
    public StateNode0 Try { get; set; }
    public List<StateNode0> Catches { get; set; }
    public StateNode0 Finally { get; set; }
    public StateNode0 Fault { get; set; }

    public bool IsTerminal
    {
        get
        {
            return Final == null &&
                   IfTrue == null &&
                   IfFalse == null &&
                   Cases == null &&
                   Catches == null &&
                   Await == null;
        }
    }

    public StateNode0( int blockId )
    {
        BlockId = blockId;
        Label = Expression.Label( $"block_{BlockId}" );
    }
}
