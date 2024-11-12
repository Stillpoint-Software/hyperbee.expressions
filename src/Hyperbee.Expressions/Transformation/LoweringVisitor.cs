using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

public class LoweringVisitor : ExpressionVisitor, IDisposable
{
    private const int InitialCapacity = 4;

    private ParameterExpression _returnValue;

    private int _awaitCount;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private IVariableResolver _variableResolver;
    private VariableVisitor _variableVisitor;

    internal LoweringResult Transform( IVariableResolver variableResolver, IReadOnlyCollection<Expression> expressions )
    {
        _variableVisitor = new VariableVisitor( variableResolver, _states );
        _variableResolver = variableResolver;

        VisitExpressions( expressions );

        return new LoweringResult
        {
            Scopes = _states.Scopes,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = variableResolver.GetLocalVariables()
        };
    }

    public LoweringResult Transform( ParameterExpression[] variables, Expression[] expressions )
    {
        return Transform( new VariableResolver( variables ), expressions );
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( new VariableResolver(), expressions );
    }
    public void Dispose()
    {
        _states?.Dispose();
    }

    // Visit methods

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState,
        ParameterExpression resultVariable = null,
        Action<NodeExpression> init = null )
    {
        // Create a new state for the branch

        var branchState = _states.AddState();

        init?.Invoke( branchState );

        // Visit the branch expression

        var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.

        UpdateTailState( expression, updateNode, joinState ?? branchState ); // if no join-state, join to the branch-state (e.g. loops)

        _states.TailState.ResultVariable = resultVariable;

        return branchState;
    }

    private void VisitExpressions( IEnumerable<Expression> expressions )
    {
        foreach ( var expression in expressions )
        {
            var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.
            UpdateTailState( expression, updateNode );
        }
    }

    private void UpdateTailState( Expression expression, Expression visited, NodeExpression defaultTransitionTarget = null )
    {
        var tailState = _states.TailState;

        if ( !IsExplicitlyHandledType( expression ) )
        {
            // goto expressions should _never_ be added to the expressions list.
            // instead, they should always be represented as a transition.
            //
            // goto expressions should set the transition - the first goto should win

            if ( tailState.Transition == null && visited is GotoExpression gotoExpression )
            {
                if ( _states.TryGetLabelTarget( gotoExpression.Target, out var targetNode ) )
                {
                    tailState.Transition = new GotoTransition { TargetNode = targetNode };
                }
            }
            else
            {
                tailState.Expressions.Add( visited );
            }
        }

        // default transition handling

        if ( tailState.Transition == null && defaultTransitionTarget != null )
        {
            tailState.Transition = new GotoTransition { TargetNode = defaultTransitionTarget };
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static bool IsExplicitlyHandledType( Expression expr )
    {
        // These expression types are explicitly handled by the visitor.

        return expr
            is BlockExpression
            or ConditionalExpression
            or SwitchExpression
            or TryExpression
            or AwaitExpression
            or LoopExpression;
    }

    // Override methods for specific expression types


    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        // Lambda expressions should not be lowered with this visitor.
        // But we still need to track the variables used in the lambda.

        return new ContainerExpression( new ReadOnlyCollection<Expression>( [node] ), _variableVisitor );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        //var joinState = _states.EnterGroup( out var sourceState );

        //var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        //var blockState = _states.AddState();

        //blockState.Expressions.AddRange( Visit(node.Expressions) );
        //blockState.Transition = new GotoTransition { TargetNode = joinState };

        //sourceState.ResultVariable = resultVariable;
        //joinState.ResultValue = resultVariable;

        //_states.ExitGroup( sourceState, new GotoTransition { TargetNode = blockState } );

        //return sourceState;


        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        foreach ( var parameterExpression in node.Variables )
            _variableVisitor.AddLocalVariable( parameterExpression );

        var currentSource = sourceState;
        NodeExpression firstGoto = null;
        NodeExpression previousTail = null;

        var previousVariable = resultVariable;

        foreach ( var expression in node.Expressions )
        {
            var handlingVisitor = new HandlingVisitor();
            handlingVisitor.Visit( expression );

            if ( handlingVisitor.Handled )
            {
                var updated = VisitBranch( expression, joinState, resultVariable ); // Warning: visitation mutates the tail state.

                previousVariable = updated.ResultVariable;
                joinState.ResultVariable = previousVariable;

                // Fix tail link list of Transitions.
                if ( previousTail != null )
                    previousTail.Transition = new GotoTransition { TargetNode = updated };

                firstGoto ??= updated;
                currentSource = _states.TailState; // updated;
                previousTail = _states.TailState;
            }
            else
            {
                currentSource.Expressions.Add( _variableVisitor.Visit( Visit( expression ) ) );
            }

        }

        var blockTransition = new GotoTransition { TargetNode = firstGoto ?? joinState };

        sourceState.ResultVariable = previousVariable;
        joinState.ResultValue = previousVariable;

        _states.ExitGroup( sourceState, blockTransition );

        return sourceState;


        //VisitExpressions( node.Expressions );

        //return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var updatedTest = base.Visit( node.Test );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinState, resultVariable ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState, resultVariable )
                : joinState,
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, conditionalTransition );

        return sourceState;
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

        _returnValue ??= _variableVisitor.CreateVariable( gotoExpression.Value!.Type, VariableVisitor.VariableName.Return );

        return Expression.Assign( _returnValue, gotoExpression.Value! );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        var newBody = VisitBranch( node.Body, default, resultVariable, InitializeLabels );

        var loopTransition = new LoopTransition
        {
            BodyNode = newBody, // pass default to join back to the branch-state 
            ContinueLabel = node.ContinueLabel != null ? newBody.NodeLabel : null,
            BreakLabel = node.BreakLabel != null ? joinState.NodeLabel : null,
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, loopTransition );

        return sourceState;

        // Helper function for assigning loop labels
        void InitializeLabels( NodeExpression branchState )
        {
            if ( node.ContinueLabel != null )
                _labels[node.ContinueLabel] = Expression.Goto( branchState.NodeLabel );

            if ( node.BreakLabel != null )
                _labels[node.BreakLabel] = Expression.Goto( joinState.NodeLabel );
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return _variableVisitor.Visit( node );
        //
        // return _variableResolver.TryAddVariable( node, CreateParameter, out var updatedVariable )
        //     ? updatedVariable
        //     : base.VisitParameter( node );
        //
        // ParameterExpression CreateParameter( ParameterExpression n )
        // {
        //     return Expression.Parameter( n.Type, VariableName.Variable( n.Name, _states.TailState.StateId, ref _variableId ) );
        // }
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = base.Visit( node.SwitchValue );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState, resultVariable );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues], // TODO: Visit these because they could be async
                VisitBranch( switchCase.Body, joinState, resultVariable )
            );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, switchTransition );

        return sourceState;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );

        var tryStateVariable = _variableVisitor.CreateVariable( typeof( int ), VariableVisitor.VariableName.Try( sourceState.StateId ) );
        var exceptionVariable = _variableVisitor.CreateVariable( typeof( object ), VariableVisitor.VariableName.Exception( sourceState.StateId ) );

        // If there is a finally block then that is the join for a try/catch.
        NodeExpression finalExpression = null;

        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );
            joinState = finalExpression;
        }

        var nodeScope = _states.EnterScope();

        var tryCatchTransition = new TryCatchTransition
        {
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            TryNode = VisitBranch( node.Body, joinState, resultVariable ),
            FinallyNode = finalExpression,
            StateScope = nodeScope,
            Scopes = _states.Scopes
        };

        _states.ExitScope();

        for ( var index = 0; index < node.Handlers.Count; index++ )
        {
            // use a non-zero based index for catch states to avoid
            // conflicts with default catch state value (zero).

            var catchState = index + 1;
            var catchBlock = node.Handlers[index];

            tryCatchTransition.AddCatchBlock(
                catchBlock,
                VisitBranch( catchBlock.Body, joinState ),
                catchState );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return sourceState;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var updatedLeft = Visit( node.Left );
        var updatedRight = Visit( node.Right );

        if ( updatedRight is NodeExpression nodeExpression )
        {
            return node.Update( updatedLeft, node.Conversion, nodeExpression.ResultVariable );
        }

        return node.Update( updatedLeft, node.Conversion, updatedRight );
    }

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        switch ( node )
        {
            case AwaitExpression awaitExpression:
                return VisitAwait( awaitExpression );

            case AsyncBlockExpression asyncBlockExpression:
                // In order to ensure variables belong to the current state machine we track the hierarchy
                // through the parent resolver
                asyncBlockExpression.VariableResolver.Parent = _variableResolver;

                // Returning asyncBlockExpression here stops the processing of child async block expressions
                // This allows the visitor to process the async block expression as a single node 
                // and allows it to process from the root to the leaf versus inside out.
                return asyncBlockExpression;

            case ContainerExpression containerExpression:
                containerExpression.VariableVisitor.VariableResolver.Parent = _variableResolver;
                return containerExpression;

            default:
                return base.VisitExtension( node );
        }
    }

    protected Expression VisitAwait( AwaitExpression node )
    {
        var updatedNode = Visit( node.Target );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableVisitor.GetResultVariable( node, sourceState.StateId );
        var completionState = _states.AddState();
        _states.TailState.ResultVariable = resultVariable;

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterVariable = _variableVisitor.CreateVariable(
            awaitBinder.GetAwaiterMethod.ReturnType,
            VariableVisitor.VariableName.Awaiter( sourceState.StateId, ref _variableVisitor.VariableId )
        );

        completionState.Transition = new AwaitResultTransition { TargetNode = joinState, AwaiterVariable = awaiterVariable, ResultVariable = resultVariable, AwaitBinder = awaitBinder };

        _states.AddJumpCase( completionState.NodeLabel, joinState.NodeLabel, sourceState.StateId );

        // If we already visited a branching node we only want to use the result variable
        // else it is most likely direct awaitable (e.g. Task)
        var targetNode = updatedNode is NodeExpression nodeExpression
            ? nodeExpression.ResultVariable
            : updatedNode;

        var awaitTransition = new AwaitTransition
        {
            Target = targetNode,
            StateId = sourceState.StateId,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState,
            AwaitBinder = awaitBinder,
            ConfigureAwait = node.ConfigureAwait
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, awaitTransition );

        return (Expression) resultVariable ?? Expression.Empty();
    }

    //// Helpers

    //[MethodImpl( MethodImplOptions.AggressiveInlining )]
    //private ParameterExpression GetResultVariable( Expression node, int stateId )
    //{
    //    if ( node.Type == typeof( void ) )
    //        return null;

    //    return _variableResolver.AddVariable(
    //        Expression.Parameter( node.Type, VariableName.Result( stateId, ref _variableId ) )
    //    );
    //}

    //[MethodImpl( MethodImplOptions.AggressiveInlining )]
    //private ParameterExpression CreateVariable( Type type, string name )
    //{
    //    return _variableResolver.AddVariable( Expression.Variable( type, name ) );
    //}

    internal class HandlingVisitor : ExpressionVisitor
    {
        public bool Handled { get; set; }

        public override Expression Visit( Expression node )
        {
            if ( !IsHandled( node ) )
                return base.Visit( node );

            Handled = true;
            return node;
        }

        private static bool IsHandled( Expression expr )
        {
            return expr
                is BlockExpression
                or ConditionalExpression
                or SwitchExpression
                or TryExpression
                or AwaitExpression
                or LoopExpression;

            // TODO: would like to only lower if async/await exists and ignore internal lower,
            //       there seems to be issues with hoisting and it's hacky reduce
            // return expr is AwaitExpression;
        }

    }

    internal class VariableVisitor : ExpressionVisitor
    {
        internal int VariableId = 0;

        internal static class VariableName
        {
            // use special names to prevent collisions
            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public static string Awaiter( int stateId, ref int variableId ) => $"__awaiter<{stateId}_{variableId++}>";

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public static string Result( int stateId, ref int variableId ) => $"__result<{stateId}_{variableId++}>";

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public static string Try( int stateId ) => $"__try<{stateId}>";

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public static string Exception( int stateId ) => $"__ex<{stateId}>";

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public static string Variable( string name, int stateId, ref int variableId ) => $"__{name}<{stateId}_{variableId++}>";

            public const string Return = "return<>";
        }


        public IVariableResolver VariableResolver { get; private set; }
        public StateContext States { get; private set; }

        public VariableVisitor( IVariableResolver variableResolver, StateContext states )
        {
            VariableResolver = variableResolver;
            States = states;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return VariableResolver.TryAddVariable( node, CreateParameter, out var updatedVariable )
                ? updatedVariable
                : base.VisitParameter( node );

            ParameterExpression CreateParameter( ParameterExpression n )
            {
                return Expression.Parameter( n.Type, VariableName.Variable( n.Name, States.TailState.StateId, ref VariableId ) );
            }
        }


        // Helpers

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal void AddLocalVariable( ParameterExpression node )
        {
            VariableResolver.AddLocalVariable( node );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal ParameterExpression GetResultVariable( Expression node, int stateId )
        {
            if ( node.Type == typeof( void ) )
                return null;

            return VariableResolver.AddVariable(
                Expression.Parameter( node.Type, VariableName.Result( stateId, ref VariableId ) )
            );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal ParameterExpression CreateVariable( Type type, string name )
        {
            return VariableResolver.AddVariable( Expression.Variable( type, name ) );
        }


    }

    internal class ContainerExpression : Expression
    {
        public VariableVisitor VariableVisitor { get; }
        public ReadOnlyCollection<Expression> Expressions { get; }

        public ContainerExpression( ReadOnlyCollection<Expression> target, VariableVisitor variableVisitor )
        {
            VariableVisitor = variableVisitor;
            Expressions = VariableVisitor.Visit( target );
        }

        public ContainerExpression( BlockExpression target, VariableVisitor variableVisitor )
        {
            VariableVisitor = variableVisitor;

            foreach ( var variable in target.Variables )
                VariableVisitor.AddLocalVariable( variable );

            Expressions = VariableVisitor.Visit( target.Expressions );
        }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => Expressions[^1].Type;

        public override bool CanReduce => true;

        public override Expression Reduce() 
        {
            return Block( [], Expressions );
        }

        protected override Expression VisitChildren( ExpressionVisitor visitor )
        {
            var newTarget = visitor.Visit( Expressions );

            return newTarget == Expressions
                ? this
                : new ContainerExpression( newTarget, VariableVisitor );
        }
    }


}
