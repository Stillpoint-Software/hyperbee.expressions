using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal readonly List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public Expression FinallyNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var catches = CatchBlocks
            .Select( catchBlock => catchBlock.Reduce() );

        // TODO: Finally blocks are not allowed to have Goto's in C#
        // var finallyBody = FinallyNode != null
        //     ? Goto( FinallyNode.NodeLabel )
        //     : null;

        // TODO: FallThrough is removing the rest of the body and replacing with Empty()
        return TryCatchFinally(
            TryNode, //GotoOrFallThrough( order, TryNode ),
            FinallyNode, //finallyBody,
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
