using System.Diagnostics;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "State = {Label?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class StateNode
{
    public int StateId { get; }
    public LabelTarget Label { get; set; }
    public List<Expression> Expressions { get; } = new (8);
    public Transition Transition { get; set; }

    public StateNode( int stateId )
    {
        StateId = stateId;
        Label = Expression.Label( $"ST_{StateId:0000}" );
        Expressions.Add( Expression.Label( Label ) );
    }
 
    public void Deconstruct( out List<Expression> expressions, out Transition transition )
    {
        expressions = Expressions;
        transition = Transition;
    }
}
