using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

public class InterpretScope
{
    public int Depth { get; internal set; }
    public LinkedDictionary<string, ParameterExpression> Variables { get; internal set; } = new();
    public LinkedDictionary<ParameterExpression, object> Values { get; internal set; } = new();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void EnterScope()
    {
        Depth++;
        Variables.Push();
        Values.Push();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitScope()
    {
        Depth--;
        Variables.Pop();
        Values.Pop();
    }
}
