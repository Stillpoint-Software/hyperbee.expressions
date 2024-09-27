using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

public record GotoTransformResult
{
    public List<StateNode> Nodes { get; set; }
    public JumpTableExpression JumpTable { get; set; }
    public ParameterExpression ReturnValue { get; set; }

    public void Deconstruct( out List<StateNode> states, out JumpTableExpression jumpTable )
    {
        states = Nodes;
        jumpTable = JumpTable;
    }
}

public class GotoTransformerVisitor : ExpressionVisitor
{
    private readonly List<StateNode> _nodes = [];
    private readonly Stack<int> _joinIndexes = new();

    // jump table?
    private readonly JumpTableExpression _jumpTable = new();
    private ParameterExpression _returnValue;

    private ParameterExpression[] _initialVariables;

    private int _targetStateIndex;
    private StateNode GetTargetState() => _nodes[_targetStateIndex];

    public GotoTransformResult Transform( ParameterExpression[] initialVariables, params Expression[] expressions )
    {
        _initialVariables = initialVariables;
        InsertState();

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new GotoTransformResult { Nodes = _nodes, JumpTable = _jumpTable, ReturnValue = _returnValue};
    }

    public GotoTransformResult Transform( params Expression[] expressions )
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

    private void ExitTransitionContext( int sourceIndex, TransitionNode transitionNode )
    {
        _nodes[sourceIndex].Transition = transitionNode;
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
        if ( _initialVariables.Contains( node ) )
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

    public void PrintStateMachine()
    {
        PrintStateMachine( _nodes );
    }

    public static void PrintStateMachine( List<StateNode> states )
    {
        foreach ( var state in states )
        {
            if ( state == null )
                continue;

            // label

            Console.WriteLine( state.Label.Name + ":" );

            // variables

            if ( state.Variables.Count > 0 )
            {
                Console.WriteLine( "\tVariables" );
                Console.WriteLine( $"\t\t[{VariablesToString( state.Variables )}]" );
            }

            // expressions

            if ( state.Expressions.Count > 0 )
            {
                Console.WriteLine( "\tExpressions" );

                foreach ( var expr in state.Expressions )
                {
                    Console.WriteLine( $"\t\t{expr}" );
                }
            }

            // transitions

            var transition = state.Transition;

            Console.WriteLine( $"\t{transition?.GetType().Name ?? "Terminal"}" );

            if ( transition != null )
            {
                switch ( transition )
                {
                    case ConditionalTransition condNode:
                        Console.WriteLine( $"\t\tIfTrue -> {condNode.IfTrue?.Label}" );
                        Console.WriteLine( $"\t\tIfFalse -> {condNode.IfFalse?.Label}" );
                        break;
                    case SwitchTransition switchNode:
                        foreach ( var caseNode in switchNode.CaseNodes )
                        {
                            Console.WriteLine( $"\t\tCase -> {caseNode?.Label}" );
                        }
                        Console.WriteLine( $"\t\tDefault -> {switchNode.DefaultNode?.Label}" );
                        break;
                    case TryCatchTransition tryNode:
                        Console.WriteLine( $"\t\tTry -> {tryNode.TryNode?.Label}" );
                        foreach ( var catchNode in tryNode.CatchNodes )
                        {
                            Console.WriteLine( $"\t\tCatch -> {catchNode?.Label}" );
                        }
                        Console.WriteLine( $"\t\tFinally -> {tryNode.FinallyNode?.Label}" );
                        break;
                    case AwaitTransition awaitNode:
                        Console.WriteLine( $"\t\tCompletion -> {awaitNode.CompletionNode?.Label}" );
                        break;
                    case AwaitResultTransition awaitResultNode:
                        Console.WriteLine( $"\t\tGoto -> {awaitResultNode.TargetNode?.Label}" );
                        break;
                    case GotoTransition gotoNode:
                        Console.WriteLine( $"\t\tGoto -> {gotoNode.TargetNode?.Label}" );
                        break;
                    case LabelTransition:
                        break;
                }
            }

            if ( state.Transition == null )
            {
                Console.WriteLine( "\t\tExit" );
            }

            Console.WriteLine();
        }

        return;

        static string VariablesToString( IEnumerable<ParameterExpression> parameterExpressions )
        {
            return string.Join( ", ", parameterExpressions.Select( x => $"{TypeToString( x.Type )} {x.Name}" ) );

            static string TypeToString( Type type )
            {
                return type switch
                {
                    null => "null",
                    { IsGenericType: true } => $"{type.Name.Split( '`' )[0]}<{string.Join( ", ", type.GetGenericArguments().Select( TypeToString ) )}>",
                    { IsArray: true } => $"{TypeToString( type.GetElementType() )}[]",
                    { IsByRef: true } => $"{TypeToString( type.GetElementType() )}&",
                    { IsPointer: true } => $"{TypeToString( type.GetElementType() )}*",
                    { IsGenericType: false } => type.Name
                };
            }
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

internal class AwaitCompletionExpression : Expression
{
    private readonly ParameterExpression _awaiter;
    private readonly int _stateId;

    // initialize before reduce
    private Expression _stateMachine;
    private List<FieldBuilder> _fields;
    private LabelTarget _returnLabel;
    private MemberExpression _stateIdField;
    private MemberExpression _builderField;

    public AwaitCompletionExpression( ParameterExpression awaiter, int stateId )
    {
        _awaiter = awaiter;
        _stateId = stateId;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var awaiterField = _fields.First( x => x.Name == _awaiter.Name );
        var awaiterFieldInfo = GetFieldInfo( _stateMachine.Type, awaiterField );
        var stateMachineAwaiterField = Field( _stateMachine, awaiterFieldInfo );

        return IfThen(
            IsFalse( Property( stateMachineAwaiterField, "IsCompleted" ) ),
            Block(
                Assign( _stateIdField, Constant( _stateId ) ),
                Call(
                    _builderField,
                    "AwaitUnsafeOnCompleted",
                    [awaiterField.FieldType, typeof(IAsyncStateMachine)],
                    stateMachineAwaiterField,
                    _stateMachine
                ),
                Return( _returnLabel )
            )
        );

        static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
        {
            return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
        }
    }

    public void Initialize( Expression stateMachine, List<FieldBuilder> fields, LabelTarget returnLabel, MemberExpression stateIdField, MemberExpression buildField )
    {
        _stateMachine = stateMachine;
        _fields = fields;
        _returnLabel = returnLabel;
        _stateIdField = stateIdField;
        _builderField = buildField;
    }
}
