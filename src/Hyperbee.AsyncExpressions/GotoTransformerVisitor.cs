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
            _currentStateIndex = InsertState();

            Visit( expression );

            return _states;
        }

        private int InsertState()
        {
            _states.Add( new StateNode( _labelCounter++ ) );
            return _states.Count - 1;
        }

        private int InsertState( Expression expression )
        {
            var stateIndex = InsertState();
            _currentStateIndex = stateIndex;

            Visit( expression ); // Visit may mutate _currentStateIndex
            
            return stateIndex;
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

            var currentStateIndex = _currentStateIndex;

            var ifTrueIndex = InsertState( node.IfTrue );
            var ifFalseIndex = (node.IfFalse is not DefaultExpression) ? InsertState( node.IfFalse ) : -1;

            var continueToIndex = InsertState();
            PushContinueTo( continueToIndex );

            var conditionalTransition = new ConditionalTransition 
            { 
                IfTrue = _states[ifTrueIndex], 
                IfFalse = ifFalseIndex >= 0 ? _states[ifFalseIndex] : null, 
                ContinueTo = _states[continueToIndex] 
            };

            _states[currentStateIndex].Transition = conditionalTransition;
            _currentStateIndex = PopContinueTo();

            return node;
        }

        protected override Expression VisitSwitch( SwitchExpression node )
        {
            Visit( node.SwitchValue );

            var currentStateIndex = _currentStateIndex;
            var switchTransition = new SwitchTransition();

            var continueToIndex = InsertState();
            PushContinueTo( continueToIndex );

            foreach ( var switchCase in node.Cases )
            {
                var caseIndex = InsertState( switchCase.Body );

                _states[caseIndex].Transition = new DefaultTransition 
                { 
                    ContinueTo = _states[continueToIndex]
                };

                switchTransition.CaseNodes.Add( _states[caseIndex] );
            }

            if ( node.DefaultBody != null )
            {
                var defaultIndex = InsertState( node.DefaultBody );

                _states[defaultIndex].Transition = new DefaultTransition 
                { 
                    ContinueTo = _states[continueToIndex] 
                };

                switchTransition.DefaultNode = _states[defaultIndex];
            }

            continueToIndex = PopContinueTo();

            switchTransition.ContinueTo = _states[continueToIndex];

            _states[currentStateIndex].Transition = switchTransition;
            _currentStateIndex = continueToIndex;

            return node;
        }

        protected override Expression VisitTry( TryExpression node ) //BF awaits aren't allowed in try-catch-finally. Are we doing too much?
        {
            var currentStateIndex = _currentStateIndex;

            var tryCatchTransition = new TryCatchTransition();

            var continueToIndex = InsertState();
            PushContinueTo( continueToIndex );

            var tryIndex = InsertState( node.Body );
            tryCatchTransition.TryNode = _states[tryIndex];

            foreach ( var catchBlock in node.Handlers )
            {
                var catchIndex = InsertState( catchBlock.Body );
                tryCatchTransition.CatchNodes.Add( _states[catchIndex] );
            }

            if ( node.Finally != null )
            {
                var finallyIndex = InsertState( node.Finally );
                tryCatchTransition.FinallyNode = _states[finallyIndex];
            }

            continueToIndex = PopContinueTo();
            
            tryCatchTransition.ContinueTo = _states[continueToIndex];

            _states[currentStateIndex].Transition = tryCatchTransition;
            _currentStateIndex = continueToIndex;

            return node;
        }

        protected override Expression VisitExtension( Expression node )
        {
            if ( node is not AwaitExpression awaitExpression )
            {
                CurrentState.Expressions.Add( node );
                return node;
            }

            var currentStateIndex = _currentStateIndex;

            // awaiter-finally
            var continueToIndex = InsertState();
            PushContinueTo( continueToIndex );

            // awaiter-continuation
            var completionStateIndex = InsertState( awaitExpression.Target ); 

            var gotoTransition = new GotoTransition
            {
                TargetNode = _states[continueToIndex]
            };

            _states[completionStateIndex].Transition = gotoTransition;

            // awaiter
            var awaitTransition = new AwaitTransition
            {
                ContinuationId = _continuationCounter++,
                CompletionNode = _states[completionStateIndex],
                ContinueTo = _states[continueToIndex]
            };

            _states[currentStateIndex].Transition = awaitTransition;

            _currentStateIndex = PopContinueTo();

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

        protected override Expression VisitGoto( GotoExpression node )
        {
            var currentStateIndex = _currentStateIndex;
            
            var gotoTransition = new GotoTransition 
            { 
                TargetNode = _states[GetOrCreateLabelIndex( node.Target )] 
            };

            _states[currentStateIndex].Transition = gotoTransition;
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

            index = InsertState();
            _labelMappings[label] = index;

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

                var transitionName = state?.Transition?.GetType().Name ?? "Null";

                Console.WriteLine( $"{state.Label.Name}: [{transitionName}]" );

                foreach ( var expr in state.Expressions )
                {
                    Console.WriteLine( $"\t{ExpressionToString( expr )}" );
                }

                var transition = state.Transition;

                if ( transition != null )
                {
                    switch ( transition )
                    {
                        case ConditionalTransition condNode:
                            Console.WriteLine( $"\tIfTrue -> {condNode.IfTrue?.Label}" );
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
                        case DefaultTransition:
                            break;
                    }
                }

                if ( state.Transition?.ContinueTo != null )
                    Console.WriteLine( $"\tContinueTo -> {state.Transition.ContinueTo.Label}" );

                if ( state.Transition == null )
                    Console.WriteLine( "\tTerminal" );

                Console.WriteLine();
            }
        }

        private static string ExpressionToString( Expression expr )
        {
            return expr.ToString();
        }
    }
}
