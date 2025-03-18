using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

public class InterpretScope : LinkedDictionary<ParameterExpression, object>
{
    public InterpretScope() { }
    public InterpretScope( InterpretScope scope ) : base( scope ) { }
}
