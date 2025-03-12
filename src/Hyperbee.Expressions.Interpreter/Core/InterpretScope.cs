using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

public class InterpretScope
{
    public LinkedDictionary<ParameterExpression, object> Values { get; internal set; }

    public InterpretScope( LinkedDictionary<ParameterExpression, object> values = null )
    {
        Values = values == null 
            ? new LinkedDictionary<ParameterExpression, object>()
            : new LinkedDictionary<ParameterExpression, object>( values );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void EnterScope()
    {
        Values.Push();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitScope()
    {
        Values.Pop();
    }
}
