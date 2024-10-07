using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    private readonly List<CatchBlockDefinition> _catchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var catches = _catchBlocks
            .Select( catchBlock => catchBlock.Reduce() );

        var finallyBody = FinallyNode != null
            ? Goto( FinallyNode.NodeLabel )
            : null;

        return TryCatchFinally(
            //Expression.Goto( TryNode.NodeLabel )
            GotoOrFallThrough( order, TryNode ), //BF
            finallyBody,
            [.. catches]
        );
    }

    internal override NodeExpression FallThroughNode => TryNode;

    public void AddCatchBlock( Type test, NodeExpression body )
    {
        _catchBlocks.Add( new CatchBlockDefinition( test, body ) );
    }

    private record CatchBlockDefinition( Type Test, NodeExpression Body )
    {
        public CatchBlock Reduce() => Catch( Test, Goto( Body.NodeLabel ) );
    }
}
