using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

internal sealed class StateResult
{
    public Expression Variable { get; set; }
    public Expression Value { get; set; }

    public void Deconstruct( out Expression variable, out Expression value )
    {
        variable = Variable;
        value = Value;
    }
}
