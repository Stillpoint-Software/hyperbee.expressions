using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal interface IStateNode //BF ME
{
    public int StateId { get; }
    public int GroupId { get; }
    public int ScopeId { get; }

    public int StateOrder { get; set; }

    public NodeResult Result { get; } //BF ME - NodeResult to StateResult ??

    public LabelTarget NodeLabel { get; }
    public List<Expression> Expressions { get; }

    public Transition Transition { get; }

    public Expression GetExpression( StateMachineContext context );

    public Expression AsExpression() => this as Expression;
}
