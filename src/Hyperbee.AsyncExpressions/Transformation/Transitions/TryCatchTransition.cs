using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    private readonly List<CatchBlockDefinition> _catchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        var catches = _catchBlocks
            .Select( catchBlock => catchBlock.Reduce() );

        var finallyBody = FinallyNode != null
            ? Expression.Goto( FinallyNode.NodeLabel )
            : null;

        return Expression.TryCatchFinally(
            //Expression.Goto( TryNode.NodeLabel )
            GotoOrFallThrough( order, TryNode ), //BF
            finallyBody,
            [.. catches]
        );
    }

    internal override NodeExpression LogicalNextNode => TryNode;

    public void AddCatchBlock( Type test, NodeExpression body )
    {
        _catchBlocks.Add( new CatchBlockDefinition( test, body ) );
    }

    private record CatchBlockDefinition( Type Test, NodeExpression Body )
    {
        public CatchBlock Reduce() => Expression.Catch( Test, Expression.Goto( Body.NodeLabel ) );
    }
}
