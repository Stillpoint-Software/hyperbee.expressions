using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class TryCatchTransition : Transition
{
    internal List<CatchBlockDefinition> CatchBlocks = [];
    public StateNode TryNode { get; set; }
    public StateNode FinallyNode { get; set; }

    // Where a resume that lands in this region while disposing should go: this try's
    // finally, or the nearest enclosing one when this try has none.

    public StateNode DisposeNode { get; set; }

    public Expression TryStateVariable { get; set; }
    public Expression ExceptionVariable { get; set; }

    public StateContext.Scope StateScope { get; init; }
    public List<StateContext.Scope> Scopes { get; init; }

    internal override StateNode FallThroughNode => TryNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.AddRange( Expressions() );
        return;

        List<Expression> Expressions()
        {
            var body = new List<Expression>
            {
                JumpTableBuilder.Build(
                    StateScope,
                    Scopes,
                    context.StateMachineInfo.StateField
                )
            };

            body.AddRange( StateScope.GetExpressions( context ) );

            MapCatchBlock( context.StateNode.StateOrder, out var catches, out var switchCases );

            // The dispatch variables are state-machine fields, so they survive across
            // states and across iterations. Reset them on every entry into the region.
            //
            // The scope label marks the region entry point. Resuming into the region
            // targets it, so that expressions preceding the try are not re-run.

            // Resuming here while disposing means the caller abandoned the sequence somewhere
            // in this region. Run the pending finally rather than the body -- but only for a
            // state this region owns. A resume bound for a nested region arrives here first,
            // because the outer table routes a whole subtree to its entry, and that region's
            // finally has to run before this one's.

            var disposeCheck = BuildDisposeCheck( context );

            return [
                Label( StateScope.InitialLabel ),
                Assign( TryStateVariable, Constant( 0 ) ),
                Assign( ExceptionVariable, Constant( null, ExceptionVariable.Type ) ),
                disposeCheck,
                TryCatch(
                    body.Count == 1
                        ? body[0]
                        : Block( body ),
                    catches
                ),
                Switch( // Handle error
                    TryStateVariable,
                    switchCases
                )
            ];
        }
    }

    private Expression BuildDisposeCheck( StateMachineContext context )
    {
        if ( DisposeNode == null || context.StateMachineInfo is not EnumerableStateMachineInfo enumerableInfo )
            return Empty();

        var jumpCases = StateScope.JumpCases;

        if ( jumpCases.Count == 0 )
            return Empty();

        var cases = new SwitchCase[jumpCases.Count];

        for ( var index = 0; index < jumpCases.Count; index++ )
        {
            cases[index] = SwitchCase( Goto( DisposeNode.NodeLabel ), Constant( jumpCases[index].StateId ) );
        }

        return IfThen(
            enumerableInfo.DisposingField,
            Switch( enumerableInfo.StateField, cases ) );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        references.Add( TryNode.NodeLabel );

        if ( FinallyNode != null )
            references.Add( FinallyNode.NodeLabel );

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            references.Add( CatchBlocks[index].UpdateBody.NodeLabel );
        }
    }

    private void MapCatchBlock( int order, out CatchBlock[] catches, out SwitchCase[] switchCases )
    {
        var includeFinal = FinallyNode != null;
        var size = CatchBlocks.Count + (includeFinal ? 1 : 0);

        catches = new CatchBlock[size];
        switchCases = new SwitchCase[size];

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            var catchBlock = CatchBlocks[index];

            catches[index] = catchBlock.Reduce( TryStateVariable );

            switchCases[index] = SwitchCase(
                GotoOrFallThrough( order, catchBlock.UpdateBody ),
                Constant( catchBlock.CatchState ) );
        }

        if ( !includeFinal )
            return;

        // No catch block handled the exception. Capture it so that it can be re-thrown
        // once the finally block has run, then dispatch to the finally state.

        var unhandled = Parameter( typeof( Exception ), "__unhandled<>" );

        catches[^1] = Catch(
            unhandled,
            Block(
                typeof( void ),
                Assign( ExceptionVariable, Call( ReflectionHelper.ExceptionDispatchInfoCapture, unhandled ) ),
                Assign( TryStateVariable, Constant( catches.Length ) )
            )
        );

        switchCases[^1] = SwitchCase(
            Goto( FinallyNode.NodeLabel ),
            Constant( catches.Length )
        );
    }

    public void AddCatchBlock( CatchBlock handler, ParameterExpression variable, Expression filter, StateNode updateBody, int catchState )
    {
        CatchBlocks.Add( new CatchBlockDefinition( handler, variable, filter, updateBody, catchState ) );
    }

    internal class CatchBlockDefinition( CatchBlock handler, ParameterExpression variable, Expression filter, StateNode updateBody, int catchState )
    {
        public CatchBlock Handler { get; init; } = handler;

        // The hoisted catch variable, and the resolved filter, both null when the source
        // handler had none.

        public ParameterExpression Variable { get; init; } = variable;
        public Expression Filter { get; init; } = filter;

        public StateNode UpdateBody { get; internal set; } = updateBody;
        public int CatchState { get; init; } = catchState;

        // Lowering moves the handler body into its own state, outside of the try, so all
        // the generated catch block does is record which handler matched. The exception
        // itself is copied to the hoisted catch variable so that the handler state can
        // still reference it.

        public CatchBlock Reduce( Expression tryStateVariable )
        {
            var setState = Assign( tryStateVariable, Constant( CatchState ) );

            if ( Variable == null )
                return MakeCatchBlock( Handler.Test, null, Block( typeof( void ), setState ), Filter );

            var caught = Parameter( Handler.Test, $"__caught<{CatchState}>" );

            return MakeCatchBlock(
                Handler.Test,
                caught,
                Block(
                    typeof( void ),
                    Assign( Variable, caught ),
                    setState
                ),
                Filter == null ? null : ParameterReplacer.Replace( Filter, Variable, caught )
            );
        }
    }

    // Rebinds the hoisted catch variable to the generated catch parameter. A filter runs
    // before the catch body, so it cannot read the hoisted variable.

    private sealed class ParameterReplacer( ParameterExpression target, ParameterExpression replacement ) : ExpressionVisitor
    {
        public static Expression Replace( Expression node, ParameterExpression target, ParameterExpression replacement )
        {
            return new ParameterReplacer( target, replacement ).Visit( node );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return node == target ? replacement : node;
        }
    }
}
