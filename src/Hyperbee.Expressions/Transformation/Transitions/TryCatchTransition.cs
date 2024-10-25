using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal readonly List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override NodeExpression FallThroughNode => TryNode;
    public ParameterExpression TryStateVariable { get; set; }
    public ParameterExpression ExceptionVariable { get; set; }

    public StateScope StateScope { get; init; }
    public List<StateScope> Scopes { get; init; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var expressions = new List<Expression>( StateScope.Nodes.Count + 1 )
        {
            JumpTableBuilder.Build(
                StateScope,
                Scopes,
                resolverSource.StateIdField
            )
        };

        expressions.AddRange( StateScope.Nodes.Select( x => x.Reduce( resolverSource ) ) );

        MapCatchBlock( out var catches, out var switchCases );

        return Block(
            TryCatch(
                Block( expressions ),
                catches
            ),
            Switch( // Handle error
                TryStateVariable,
                Empty(),
                switchCases )
            );
    }

    private void MapCatchBlock( out CatchBlock[] catches, out SwitchCase[] switchCases )
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
                    ? Goto( nodeExpression.NodeLabel )
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

    internal record CatchBlockDefinition( CatchBlock Handler, Expression UpdateBody, int CatchState )
    {
        public CatchBlock Reduce( ParameterExpression exceptionVariable, ParameterExpression tryStateVariable )
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
    }
}
