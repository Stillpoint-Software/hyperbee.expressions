using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public class StateNode
{
    public int StateId { get; }
    public LabelTarget Label { get; set; }
    public List<Expression> Expressions { get; } = [];
    public Transition Transition { get; set; }
    public HashSet<ParameterExpression> Variables { get; } = [];

    public StateNode( int stateId )
    {
        StateId = stateId;
        Label = Expression.Label( $"ST_{StateId:0000}" );
        Expressions.Add( Expression.Label( Label ) );
    }

    public void Deconstruct( out IReadOnlyCollection<ParameterExpression> variables, out List<Expression> expressions, out Transition transition )
    {
        variables = Variables;
        expressions = Expressions;
        transition = Transition;
    }
}
