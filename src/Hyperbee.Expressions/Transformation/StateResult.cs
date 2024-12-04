using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public sealed class StateResult
{
    public Expression Variable { get; set; } // Left
    public Expression Value { get; set; } // Right

    public void Deconstruct( out Expression variable, out Expression value )
    {
        variable = Variable;
        value = Value;
    }
}
