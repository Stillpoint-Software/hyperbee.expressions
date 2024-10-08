using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal readonly List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var catches = CatchBlocks
            .Select( catchBlock => catchBlock.Reduce() );

        var finallyBody = FinallyNode != null
            ? Goto( FinallyNode.NodeLabel )
            : null;

        return TryCatchFinally(
            GotoOrFallThrough( order, TryNode ),
            finallyBody,
            [.. catches]
        );
    }

    internal override NodeExpression FallThroughNode => TryNode;

    public void AddCatchBlock( Type test, NodeExpression body )
    {
        CatchBlocks.Add( new CatchBlockDefinition( test, body ) );
    }

    internal record CatchBlockDefinition( Type Test, NodeExpression Body )
    {
        public CatchBlock Reduce() => Catch( Test, Goto( Body.NodeLabel ) );
    }
}
