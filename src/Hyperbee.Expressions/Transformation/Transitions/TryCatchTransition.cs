using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    public Expression TryStateVariable { get; set; }
    public Expression ExceptionVariable { get; set; }

    public StateContext.Scope StateScope { get; init; }
    public List<StateContext.Scope> Scopes { get; init; }

    internal override NodeExpression FallThroughNode => TryNode;

    protected override Transition VisitChildren( ExpressionVisitor visitor )
    {
        return Update(
            CatchBlocks.Select( c => new CatchBlockDefinition( c.Handler, visitor.Visit( c.UpdateBody ), c.CatchState ) ).ToList(),
            visitor.Visit( TryStateVariable ),
            visitor.Visit( ExceptionVariable )
        );
    }

    internal TryCatchTransition Update(
        List<CatchBlockDefinition> catchBlocks,
        Expression tryStateVariable,
        Expression exceptionVariable )
    {
        if ( catchBlocks == CatchBlocks && tryStateVariable == TryStateVariable && exceptionVariable == ExceptionVariable )
            return this;

        return new TryCatchTransition
        {
            TryNode = TryNode,
            FinallyNode = FinallyNode,
            CatchBlocks = catchBlocks,
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            StateScope = StateScope,
            Scopes = Scopes
        };
    }

    protected override List<Expression> ReduceTransition( NodeExpression node )
    {
        return [GetExpression()];

        Expression GetExpression()
        {
            var body = new List<Expression>
            {
                JumpTableBuilder.Build(
                    StateScope,
                    Scopes,
                    node.StateMachineSource.StateIdField
                )
            };

            body.AddRange( StateScope.Nodes );

            MapCatchBlock( node.StateOrder, out var catches, out var switchCases );

            return Block(
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
            );
        }
    }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        references.Add( TryNode.NodeLabel );

        if ( FinallyNode != null )
            references.Add( FinallyNode.NodeLabel );

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            if ( CatchBlocks[index].UpdateBody is not NodeExpression nodeExpression )
                continue;

            CatchBlocks[index].UpdateBody = OptimizeGotos( nodeExpression );
            references.Add( nodeExpression.NodeLabel );
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

            catches[index] = catchBlock.Reduce( ExceptionVariable, TryStateVariable );

            switchCases[index] = SwitchCase(
                (catchBlock.UpdateBody is NodeExpression nodeExpression)
                    ? GotoOrFallThrough( order, nodeExpression )
                    : Block( typeof( void ), catchBlock.UpdateBody ),
                Constant( catchBlock.CatchState ) );
        }

        if ( !includeFinal )
            return;

        catches[^1] = Catch(
            typeof( Exception ),
            Block(
                typeof( void ),
                Assign( TryStateVariable, Constant( catches.Length ) )
            )
        );

        switchCases[^1] = SwitchCase(
            Goto( FinallyNode.NodeLabel ),
            Constant( catches.Length )
        );
    }

    public void AddCatchBlock( CatchBlock handler, Expression updateBody, int catchState )
    {
        CatchBlocks.Add( new CatchBlockDefinition( handler, updateBody, catchState ) );
    }

    internal class CatchBlockDefinition( CatchBlock handler, Expression updateBody, int catchState )
    {
        public CatchBlock Reduce( Expression exceptionVariable, Expression tryStateVariable )
        {
            if ( Handler.Variable == null )
            {
                return Catch(
                    Handler.Test,
                    Block(
                        typeof( void ),
                        Assign( tryStateVariable, Constant( CatchState ) )
                    ) );
            }

            return Catch(
                Handler.Test,
                Block(
                    typeof( void ),
                    Assign( exceptionVariable, Constant( Handler.Variable ) ),
                    Assign( tryStateVariable, Constant( CatchState ) )
                ) );
        }

        public CatchBlock Handler { get; init; } = handler;
        public Expression UpdateBody { get; internal set; } = updateBody;
        public int CatchState { get; init; } = catchState;

        public void Deconstruct( out CatchBlock handler, out Expression updateBody, out int catchState )
        {
            handler = Handler;
            updateBody = UpdateBody;
            catchState = CatchState;
        }
    }
}
