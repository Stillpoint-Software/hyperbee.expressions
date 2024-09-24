using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class StateNode
{
    public int BlockId { get; }
    public LabelTarget Label { get; set; }
    public List<Expression> Expressions { get; } = [];
    public TransitionNode Transition { get; set; }
    public HashSet<ParameterExpression> Variables { get; } = [];

    public StateNode( int blockId )
    {
        BlockId = blockId;
        Label = Expression.Label( $"block_{BlockId}" );
    }
}
