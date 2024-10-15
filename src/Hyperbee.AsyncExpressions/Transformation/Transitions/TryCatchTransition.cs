using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal readonly List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override NodeExpression FallThroughNode => TryNode;
    public ParameterExpression TryStateVariable { get; set; }
    public ParameterExpression ExceptionVariable { get; set; }

    public NodeScope NodeScope { get; init; }
    public List<NodeScope> Scopes { get; init; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var jumpTableExpression = NodeScope.CreateJumpTable( Scopes, resolverSource.StateIdField );
        var expressions = new List<Expression>( NodeScope.Nodes.Count + 1 ) { jumpTableExpression };
        expressions.AddRange( NodeScope.Nodes.Select( x => x.Reduce( resolverSource ) ) );

        var tryBody = Block(
            expressions
        );

        var includeFinal = FinallyNode != null;
        var size = CatchBlocks.Count + (includeFinal ? 1 : 0);
        var catches = new CatchBlock[size];
        var switchCases = new SwitchCase[size];

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            var catchBlock = CatchBlocks[index];
            catches[index] = catchBlock.Reduce( ExceptionVariable, TryStateVariable );
            switchCases[index] = SwitchCase(
                (catchBlock.UpdateBody is NodeExpression nodeExpression ) 
                    ? Goto( nodeExpression.NodeLabel )
                    : Block( typeof(void), catchBlock.UpdateBody),
                Constant( catchBlock.CatchState ) );
        }

        if ( includeFinal )
        {
            catches[^1] = Catch(
                typeof(Exception),
                Block(
                    typeof(void),
                    Assign( TryStateVariable, Constant( catches.Length ) )
                ) );
            switchCases[^1] = SwitchCase(
                Goto( FinallyNode.NodeLabel ),
                Constant( catches.Length ) );
        }
        
        var handleError = Switch(
            TryStateVariable,
            Empty(),
            switchCases );

        return Block(
            TryCatch(
                tryBody,
                catches
            ),
            handleError );
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
                    typeof(void),
                    Assign( exceptionVariable, Constant( Handler.Variable ) ),
                    Assign( tryStateVariable, Constant( CatchState ) )
                ) );
        }
    }
}
