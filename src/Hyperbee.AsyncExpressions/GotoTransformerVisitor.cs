using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.TimeZoneInfo;

namespace Hyperbee.AsyncExpressions;

public class GotoTransformerVisitor : ExpressionVisitor
{
    private readonly List<StateNode> _states = [];
    private readonly Stack<int> _continueToIndexes = new();
    private readonly Dictionary<LabelTarget, int> _labelMappings = new();

    private int _continuationCounter;
    private int _labelCounter;

    private int _currentStateIndex;
    private StateNode CurrentState => _states[_currentStateIndex];

    public List<StateNode> Transform( Expression expression )
    {
        InsertState( out _currentStateIndex );

        Visit( expression );

        return _states;
    }

    private StateNode InsertState( out int index )
    {
        var stateNode = new StateNode( _labelCounter++ );
        _states.Add( stateNode );

        index = _states.Count - 1;
        return stateNode;
    }

    private StateNode VisitState( Expression expression, int continueToIndex )
    {
        var state = InsertState( out var index );
        _currentStateIndex = index;

        Visit( expression );

        _states[_currentStateIndex].Transition ??= new GotoTransition
        {
            TargetNode = _states[continueToIndex]
        };

        return state;
    }

    private int EnterTransition( out int currentStateIndex )
    {
        InsertState( out var continueToIndex );
        _continueToIndexes.Push( continueToIndex );
        currentStateIndex = _currentStateIndex;

        return continueToIndex;
    }

    private void ExitTransition( TransitionNode transitionNode, int transitionStateIndex )
    {
        _states[transitionStateIndex].Transition = transitionNode;
        _currentStateIndex = _continueToIndexes.Pop();
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
        Visit( node.Test );

        var continueToIndex = EnterTransition( out var currentStateIndex );

        var conditionalTransition = new ConditionalTransition
        {
            IfTrue = VisitState( node.IfTrue, continueToIndex ),
            IfFalse = (node.IfFalse is not DefaultExpression)
                ? VisitState( node.IfFalse, continueToIndex )
                : _states[continueToIndex]
        };

        ExitTransition( conditionalTransition, currentStateIndex );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        Visit( node.SwitchValue );

        var continueToIndex = EnterTransition( out var currentStateIndex );
        var switchTransition = new SwitchTransition();

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitState( node.DefaultBody, continueToIndex );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.CaseNodes.Add( VisitState( switchCase.Body, continueToIndex ) );
        }

        ExitTransition( switchTransition, currentStateIndex );

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var continueToIndex = EnterTransition( out var currentStateIndex );
        var tryCatchTransition = new TryCatchTransition
        {
            TryNode = VisitState( node.Body, continueToIndex )
        };

        foreach ( var catchBlock in node.Handlers )
        {
            tryCatchTransition.CatchNodes.Add( VisitState( catchBlock.Body, continueToIndex ) );
        }

        if ( node.Finally != null )
        {
            tryCatchTransition.FinallyNode = VisitState( node.Finally, continueToIndex );
        }

        ExitTransition( tryCatchTransition, currentStateIndex );

        return node;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( node is AsyncBlockExpression asyncBlockExpression )
        {
            foreach ( var expression in asyncBlockExpression.Expressions )
            {
                Visit( expression );
            }

            return node;
        }

        if ( node is not AwaitExpression awaitExpression )
        {
            CurrentState.Expressions.Add( node );
            return node;
        }

        var continueToIndex = EnterTransition( out var currentStateIndex );

        // get awaiter
        var awaitTransition = new AwaitTransition { ContinuationId = _continuationCounter++ };

        // Create awaiter variable
        var variable = CreateAwaiterVariable( awaitExpression, awaitTransition.ContinuationId );

        var awaitResultState = VisitState( awaitExpression.Target, continueToIndex );

        // Keep awaiter variable in scope for results
        CurrentState.Variables.Add( variable );
        awaitResultState.Transition = new AwaitResultTransition { TargetNode = _states[continueToIndex] };

        // awaiter Results
        awaitTransition.CompletionNode = awaitResultState;

        ExitTransition( awaitTransition, currentStateIndex );

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

        return node;
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        foreach ( var nodeArgument in node.Arguments )
        {
            Visit( nodeArgument );
        }

        CurrentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        foreach ( var nodeArgument in node.Arguments )
        {
            Visit( nodeArgument );
        }

        CurrentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var left = Visit( node.Left );
        var right = Visit( node.Right );

        var newNode = node.Update( left, node.Conversion, right );
        CurrentState.Expressions.Add( newNode );

        return node;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        CurrentState.Variables.Add( node );
        CurrentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        CurrentState.Expressions.Add( node );
        return node;
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        if ( node.NodeType != ExpressionType.Throw )
        {
            return base.VisitUnary( node );
        }

        CurrentState.Expressions.Add( node );
        return node;
    }

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

    private int GetOrCreateLabelIndex( LabelTarget label )
    {
        if ( _labelMappings.TryGetValue( label, out var index ) )
        {
            return index;
        }

        InsertState( out var stateIndex );
        _labelMappings[label] = stateIndex;

        return index;
    }

    private ParameterExpression CreateAwaiterVariable( AwaitExpression awaitExpression, int stateId )
    {
        var variable = Expression.Variable(
            awaitExpression.ReturnType == typeof(void)
                ? typeof(TaskAwaiter)
                : typeof(TaskAwaiter<>).MakeGenericType( awaitExpression.ReturnType ),
            $"awaiter<{stateId}>" );

        CurrentState.Variables.Add( variable );
        return variable;
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
