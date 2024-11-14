using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Hyperbee.Expressions.Collections;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override NodeExpression FallThroughNode => TryNode;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        references.Add( TryNode.NodeLabel );

        if ( FinallyNode != null )
            references.Add( FinallyNode.NodeLabel );

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            if ( CatchBlocks[index].UpdateBody is not NodeExpression nodeExpression )
                continue;

            CatchBlocks[index].UpdateBody = OptimizeTransition( nodeExpression );
            references.Add( nodeExpression.NodeLabel );
        }
    }

    public Expression TryStateVariable { get; set; }
    public Expression ExceptionVariable { get; set; }

    public StateContext.Scope StateScope { get; init; }
    public PooledArray<StateContext.Scope> Scopes { get; init; }

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

    internal override Expression Reduce( int order, NodeExpression expression, StateMachineSource resolverSource )
    {
        var expressions = new List<Expression>
        {
            JumpTableBuilder.Build(
                StateScope,
                Scopes,
                resolverSource.StateIdField
            )
        };

        expressions.AddRange( StateScope.Nodes );

        MapCatchBlock( order, out var catches, out var switchCases );

        return Block(
            TryCatch(
                expressions.Count == 1
                    ? expressions[0]
                    : Block( expressions ),
                catches
            ),
            Switch( // Handle error
                TryStateVariable,
                Empty(),
                switchCases
            )
        );
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
