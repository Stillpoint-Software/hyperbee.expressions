using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions
{
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

            _states[_currentStateIndex].ContinueTo = _states[continueToIndex];

            return state;
        }

        private int EnterTransition( out int currentStateIndex )
        {
            InsertState( out var continueToIndex );
            PushContinueTo( continueToIndex );
            currentStateIndex = _currentStateIndex;

            return continueToIndex;
        }

        private void ExitTransition( TransitionNode transitionNode, int transitionStateIndex, int continueToIndex )
        {
            var transitionState = _states[transitionStateIndex];

            transitionState.Transition = transitionNode;
            transitionState.ContinueTo = _states[continueToIndex];

            _currentStateIndex = PopContinueTo();
        }

        private void PushContinueTo( int index ) => _continueToIndexes.Push( index );

        private int PopContinueTo() => _continueToIndexes.Pop();

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
                    : null
            };

            ExitTransition( conditionalTransition, currentStateIndex, continueToIndex );

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

            ExitTransition( switchTransition, currentStateIndex, continueToIndex );

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

            ExitTransition( tryCatchTransition, currentStateIndex, continueToIndex );

            return node;
        }

        protected override Expression VisitExtension( Expression node )
        {
            if ( node is not AwaitExpression awaitExpression )
            {
                CurrentState.Expressions.Add( node );
                return node;
            }

            var continueToIndex = EnterTransition( out var currentStateIndex );
            var awaitTransition = new AwaitTransition();

            var awaitResultState = VisitState( awaitExpression.Target, continueToIndex ); 

            var gotoTransition = new GotoTransition
            {
                TargetNode = _states[continueToIndex]
            };

            awaitResultState.Transition = gotoTransition;

            // awaiter
            awaitTransition.ContinuationId = _continuationCounter++;
            awaitTransition.CompletionNode = awaitResultState;

            ExitTransition( awaitTransition, currentStateIndex, continueToIndex );

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

        protected override Expression VisitBinary( BinaryExpression node )
        {
            CurrentState.Expressions.Add( node );
            return node;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
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
        //     CurrentState.Label = node;
        //     return node;
        // }

        protected override Expression VisitGoto( GotoExpression node )
        {
            var gotoTransition = new GotoTransition();

            var continueToIndex = GetOrCreateLabelIndex( node.Target );
            VisitLabelTarget( node.Target );
            PushContinueTo( continueToIndex );

            gotoTransition.TargetNode = _states[continueToIndex];

            _states[continueToIndex].ContinueTo = _states[_currentStateIndex];

            _currentStateIndex = PopContinueTo();

            // var currentStateIndex = _currentStateIndex;
            //
            // var gotoTransition = new GotoTransition 
            // { 
            //     TargetNode = _states[GetOrCreateLabelIndex( node.Target )] 
            // };
            //
            // _states[currentStateIndex].Transition = gotoTransition;
            return node;
        }

        protected override Expression VisitLabel( LabelExpression node )
        {
            var labelIndex = GetOrCreateLabelIndex( node.Target );
            
            _states[labelIndex].Transition ??= new LabelTransition();

            return node;
        }

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

                var transition = state.Transition;

                var transitionName = transition?.GetType().Name ?? "Null";
                
                var continuationId = (transition is AwaitTransition awaitTransition) ? awaitTransition.ContinuationId : -1;

                Console.WriteLine( $"{state.Label.Name}: [{transitionName}] {(continuationId != -1 ? $" (state: {continuationId})" : string.Empty)}" );

                foreach ( var expr in state.Expressions )
                {
                    Console.WriteLine( $"\t{ExpressionToString( expr )}" );
                }

                if ( transition != null )
                {
                    switch ( transition )
                    {
                        case ConditionalTransition condNode:
                            Console.WriteLine( $"\tIfTrue -> {condNode.IfTrue?.Label}" );

                            if ( condNode.IfFalse != null )
                                Console.WriteLine( $"\tIfFalse -> {condNode.IfFalse?.Label}" );
                            break;
                        case SwitchTransition switchNode:
                            foreach ( var caseNode in switchNode.CaseNodes )
                            {
                                Console.WriteLine( $"\tCase -> {caseNode?.Label}" );
                            }

                            Console.WriteLine( $"\tDefault -> {switchNode.DefaultNode?.Label}" );
                            break;
                        case TryCatchTransition tryNode:
                            Console.WriteLine( $"\tTry -> {tryNode.TryNode?.Label}" );
                            foreach ( var catchNode in tryNode.CatchNodes )
                            {
                                Console.WriteLine( $"\tCatch -> {catchNode?.Label}" );
                            }

                            Console.WriteLine( $"\tFinally -> {tryNode.FinallyNode?.Label}" );
                            break;
                        case AwaitTransition awaitNode:
                            Console.WriteLine( $"\tAwait -> {awaitNode.CompletionNode?.Label}" );
                            break;
                        case GotoTransition gotoNode:
                            Console.WriteLine( $"\tGoto -> {gotoNode.TargetNode?.Label}" );
                            break;

                        case LabelTransition:
                            break;
                    }
                }

                Console.WriteLine(
                    state.ContinueTo != null 
                        ? $"\tContinueTo -> {state.ContinueTo.Label}"
                        : "\tTerminal" );

                Console.WriteLine();
            }
        }

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
