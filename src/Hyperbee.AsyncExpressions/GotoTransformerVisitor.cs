using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;


public class GotoTransformerVisitor : ExpressionVisitor
{
    private readonly List<StateNode> _states = [];
    private readonly Stack<int> _joinIndexes = new();
    private readonly Dictionary<LabelTarget, int> _labelMappings = new();

    // jump table?
    private readonly ParameterExpression _state; // state machine needs this
    private readonly JumpTableExpression _jumpTable;

    private int _continuationCounter;
    private int _labelCounter;

    private int _currentStateIndex;
    private StateNode CurrentState => _states[_currentStateIndex];

    public GotoTransformerVisitor()
    {
        _state = Expression.Parameter( typeof(int), "state" );
        _jumpTable = new JumpTableExpression( _state );
    }

    public List<StateNode> Transform( Expression expression )
    {
        InsertState( out _currentStateIndex );

        InternalVisit( expression );

        return _states;
    }

    private StateNode InsertState( out int stateIndex )
    {
        var stateNode = new StateNode( _labelCounter++ );
        _states.Add( stateNode );

        stateIndex = _states.Count - 1;
        return stateNode;
    }

    private StateNode VisitState( Expression expression, int joinIndex, bool ignoreExpression = false )
    {
        var state = InsertState( out var stateIndex );
        _currentStateIndex = stateIndex;

        if ( ignoreExpression )
            Visit( expression );
        else
            InternalVisit( expression );

        _states[_currentStateIndex].Transition ??= new GotoTransition
        {
            TargetNode = _states[joinIndex]
        };

        return state;
    }

    private int EnterTransitionContext( out int sourceIndex )
    {
        InsertState( out var joinIndex ); 

        _joinIndexes.Push( joinIndex );
        sourceIndex = _currentStateIndex;

        return joinIndex;
    }

    private void ExitTransitionContext( int sourceIndex, TransitionNode transitionNode )
    {
        _states[sourceIndex].Transition = transitionNode;
        _currentStateIndex = _joinIndexes.Pop();
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        foreach ( var expression in node.Expressions )
        {
            InternalVisit( expression );
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
                : _states[joinIndex]
        };

        var gotoConditional = Expression.IfThenElse(
            updatedTest,
            Expression.Goto( conditionalTransition.IfTrue.Label ),
            Expression.Goto( conditionalTransition.IfFalse.Label ) );

        _states[joinIndex].Expressions.Add( gotoConditional );

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
            cases.Add( Expression.SwitchCase( Expression.Goto( caseNode.Label ), switchCase.TestValues ) );
        }

        var gotoSwitch = Expression.Switch(
            updatedSwitchValue,
            defaultBody,
            [.. cases] );

        _states[joinIndex].Expressions.Add( gotoSwitch );

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
        }

        Expression finallyBody = null;
        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitState( node.Finally, joinIndex );
            finallyBody = Expression.Goto( tryCatchTransition.FinallyNode.Label );
        }

        var newTry = Expression.TryCatchFinally(
            Expression.Goto( tryCatchTransition.TryNode.Label ),
            finallyBody,
            [.. catches]
        );

        _states[joinIndex].Expressions.Add( newTry );

        ExitTransitionContext( sourceIndex, tryCatchTransition );

        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression )
        {
            return base.VisitExtension( node );
        }

        if ( node is not AwaitExpression awaitExpression )
        {
            return base.VisitExtension( node );
        }

        var joinIndex = EnterTransitionContext( out var sourceIndex );

        // get awaiter
        var awaitTransition = new AwaitTransition { ContinuationId = _continuationCounter++ };

        // Create awaiter variable
        var awaiterVariable = CreateAwaiterVariable( awaitExpression, awaitTransition.ContinuationId );

        var awaiterState = CurrentState;
        awaiterState.Expressions.Add(
            Expression.Assign( awaiterVariable,
                Expression.Call( awaitExpression.Target, awaitExpression.Target.Type.GetMethod( "GetAwaiter" )! ) ) );

        /*
        TODO: We need at least the return label, 
            but ideally we would have the state machine instance and the last await field too.
            This is where we can add a custom lazy Expression that will reduce to this:
        */
        awaiterState.Expressions.Add( new AwaitCompletionExpression( awaiterVariable ) );

        var awaitResultState = VisitState( awaitExpression.Target, joinIndex, true );
        awaiterState.Expressions.Add( Expression.Goto( awaitResultState.Label ) );

        // Keep awaiter variable in scope for results
        CurrentState.Variables.Add( awaiterVariable );
        CurrentState.Expressions.Add( CreateGetResults( awaitExpression, awaiterVariable, CurrentState.BlockId, out var localVariable ) );
        CurrentState.Expressions.Add( Expression.Goto( _states[joinIndex].Label ) );

        awaitResultState.Transition = new AwaitResultTransition { TargetNode = _states[joinIndex] };

        _jumpTable.Add( awaitResultState.Label, awaitTransition.ContinuationId );

        // awaiter Results
        awaitTransition.CompletionNode = awaitResultState;

        ExitTransitionContext( sourceIndex, awaitTransition );

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

        // build awaiter continuation
        /*
           awaiter8 = <>u__1;
           <>u__1 = default(TaskAwaiter<int>);
           num = (<>1__state = -1);
           goto IL_00fe;
        */

        return (Expression) localVariable ?? Expression.Empty();


        ParameterExpression CreateAwaiterVariable( AwaitExpression expression, int stateId )
        {
            var variable = Expression.Variable(
                expression.ReturnType == typeof( void )
                    ? typeof( TaskAwaiter )
                    : typeof( TaskAwaiter<> ).MakeGenericType( expression.ReturnType ),
                $"awaiter<{stateId}>" );

            CurrentState.Variables.Add( variable );
            return variable;
        }

        Expression CreateGetResults( AwaitExpression expression, ParameterExpression awaiter, int blockId, out ParameterExpression variable )
        {
            if ( expression.ReturnType == typeof( void ) )
            {
                variable = null;
                return Expression.Call( awaiter, "GetResult", Type.EmptyTypes );
            }

            variable = Expression.Variable( expression.ReturnType, $"<>s__{blockId}" );
            CurrentState.Variables.Add( variable );
            return Expression.Assign( variable, Expression.Call( awaiter, "GetResult", Type.EmptyTypes ) );
        }



    }
    //
    // protected override Expression VisitMethodCall( MethodCallExpression node )
    // {
    //     foreach ( var nodeArgument in node.Arguments )
    //     {
    //         VisitDepth( nodeArgument );
    //     }
    //
    //     AddNodeToBlock( node );
    //     return node;
    // }

    // protected override Expression VisitInvocation( InvocationExpression node )
    // {
    //     foreach ( var nodeArgument in node.Arguments )
    //     {
    //         VisitDepth( nodeArgument );
    //     }
    //
    //     AddNodeToBlock( node );
    //     return node;
    // }
    //
    // protected override Expression VisitBinary( BinaryExpression node )
    // {
    //     // var left = Visit( node.Left );
    //     // var right = Visit( node.Right );
    //     //
    //     // var newNode = node.Update( left, node.Conversion, right );
    //
    //     var updatedNode = base.VisitBinary( node );
    //     return updatedNode;
    // }

    // protected override Expression VisitParameter( ParameterExpression node )
    // {
    //     CurrentState.Variables.Add( node );
    //     AddNodeToBlock( node );
    //     return node;
    // }
    //
    // protected override Expression VisitConstant( ConstantExpression node )
    // {
    //     AddNodeToBlock(node);
    //     return node;
    // }

    // protected override Expression VisitUnary( UnaryExpression node )
    // {
    //     if ( node.NodeType != ExpressionType.Throw )
    //     {
    //         return base.VisitUnary( node );
    //     }
    //
    //     AddNodeToBlock( node );
    //     return node;
    // }


    // protected override LabelTarget VisitLabelTarget( LabelTarget node )
    // {
    //     var labelIndex = GetOrCreateLabelIndex( node );
    //     _states[labelIndex].Transition ??= new LabelTransition();
    //
    //     CurrentState.Label = node;
    //
    //     return node;
    // }
    //
    // protected override Expression VisitGoto( GotoExpression node )
    // {
    //     var continueToIndex = EnterTransition( out var currentStateIndex );
    //     var gotoTransition = new GotoTransition();
    //
    //     base.VisitGoto( node );
    //     //VisitLabelTarget( node.Target );
    //     PushContinueTo( continueToIndex );
    //     gotoTransition.TargetNode = _states[continueToIndex];
    //
    //     _states[continueToIndex].ContinueTo = _states[_currentStateIndex];
    //     _currentStateIndex = PopContinueTo();
    //
    //     ExitTransition( gotoTransition, currentStateIndex, continueToIndex );
    //
    //     return node;
    // }
    //
    // protected override Expression VisitLabel( LabelExpression node )
    // {
    //     var labelIndex = GetOrCreateLabelIndex( node.Target );
    //     _states[labelIndex].Transition ??= new LabelTransition();
    //
    //     return node;
    // }
    //
    // private int GetOrCreateLabelIndex( LabelTarget label )
    // {
    //     if ( _labelMappings.TryGetValue( label, out var index ) )
    //     {
    //         return index;
    //     }
    //
    //     InsertState( out var stateIndex );
    //     _labelMappings[label] = stateIndex;
    //
    //     return index;
    // }

    private Expression InternalVisit( Expression expr )
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
                CurrentState.Expressions.Add( updateNode );
                return updateNode;

        }
    }


    public void PrintStateMachine()
    {
        PrintStateMachine( _states );
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
                    Console.WriteLine( $"\t\t{ExpressionToString( expr )}" );
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
    }

    private static string VariablesToString( IEnumerable<ParameterExpression> parameterExpressions )
    {
        return string.Join( ", ", parameterExpressions.Select( x => $"{TypeToString(x.Type)} {x.Name}" ) );

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

    private static string ExpressionToString( Expression expr )
    {
        return expr.ToString();
    }
}

public class JumpTableExpression : Expression
{
    private readonly Expression _state;
    private readonly List<SwitchCase> _cases = [];

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    

    public JumpTableExpression( Expression state )
    {
        _state = state;
    }

    public override Expression Reduce()
    {
        return Switch( _state, Empty(), [.. _cases] );
    }

    public void Add( LabelTarget jumpLabel, int stateId )
    {
        _cases.Add(
            SwitchCase(
                Block(
                    Assign( _state, Expression.Constant( -1 ) ),
                    Goto( jumpLabel ) ),
                Constant( stateId ) ) );
    }
}

public class AwaitCompletionExpression : Expression
{
    private readonly Expression _awaiter;

    // set before reduce
    private Type _stateMachineType;
    private Expression _stateMachine;
    private LabelTarget _returnLabel;

    public AwaitCompletionExpression( Expression awaiter )
    {
        _awaiter = awaiter;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return Constant("if not completed then await on completed and return");

        // return IfThen(
        //     IsFalse( Property( _awaiter, "IsCompleted" ) ),
        //     Block(
        //         Call(
        //             Field( _stateMachine, buildFieldInfo ),
        //             nameof(AsyncTaskMethodBuilder<TResult>.AwaitUnsafeOnCompleted),
        //             [_awaiter.FieldType, typeof(IAsyncStateMachine)],
        //             Field( _stateMachine, _awaiter ),
        //             _stateMachine
        //         ),
        //         Return( _returnLabel )
        //     )
        // );
    }
}
