using Hyperbee.Collections;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> Results { get; init; } = new();

    public bool IsTransitioning => Transition != null;

    private Transition _transition;
    public Transition Transition
    {
        get => _transition;
        set 
        {
            _transition?.Reset();
            _transition = value;
        }
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results )
    {
        scope = Scope;
        results = Results;
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results, out Transition transition )
    {
        scope = Scope;
        results = Results;
        transition = Transition;
    }

    internal InterpretContext Clone()
    {
        var clone = new InterpretContext
        {
            Scope = CloneScope( Scope ),
            Results = new ( Results ),
            Transition = Transition?.Clone()
        };

        return clone;

        static InterpretScope CloneScope( InterpretScope originalScope )
        {
            var newValues = new LinkedDictionary<ParameterExpression, object>();

            foreach ( var node in originalScope.Values.Nodes().Reverse() )
            {
                newValues.Push( node.Name, node.Dictionary );
            }

            var newScope = new InterpretScope
            {
                Depth = originalScope.Depth,
                Values = newValues
            };

            return newScope;
        }
    }
}
