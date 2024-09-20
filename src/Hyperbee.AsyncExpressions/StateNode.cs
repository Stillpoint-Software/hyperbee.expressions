using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class StateNode
{
    public int BlockId { get; }
    public LabelTarget Label { get; set; }
    public List<Expression> Expressions { get; } = [];
    public StateNode Final { get; set; }

    // Condition-specific fields
    public StateNode IfTrue { get; set; }
    public StateNode IfFalse { get; set; }

    // Switch-specific fields
    public List<StateNode> Cases { get; set; }

    // For Async/Await fields
    public int? ContinuationId { get; set; }
    public StateNode Await { get; set; }

    // Goto-specific fields
    public StateNode Continue { get; set; }
    public StateNode Break { get; set; }
    public StateNode Goto { get; set; }

    // TryCatch-specific fields
    public StateNode Try { get; set; }
    public List<StateNode> Catches { get; set; }
    public StateNode Finally { get; set; }
    public StateNode Fault { get; set; }

    public bool IsTerminal
    {
        get
        {
            return Final == null &&
                   IfTrue == null &&
                   IfFalse == null &&
                   Cases == null &&
                   Catches == null &&
                   Await == null;
        }
    }

    public StateNode( int blockId )
    {
        BlockId = blockId;
        Label = Expression.Label( $"block_{BlockId}" );
    }
}
