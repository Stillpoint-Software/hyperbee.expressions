using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public sealed class NodeResult
{
    public Expression Variable { get; set; } // Left-side
    public Expression Value { get; set; } // Right-side

    public void Deconstruct( out Expression variable, out Expression value )
    {
        variable = Variable;
        value = Value;
    }
}
