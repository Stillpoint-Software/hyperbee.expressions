using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> Results { get; init; } = new();

    public bool IsTransitioning => Transition != null;

    public int TransitionChildIndex;

    public Transition Transition { get; set; }

    public LambdaExpression Reduced { get; set; }

    public Dictionary<GotoExpression, Transition> Transitions { get; set; }

    public InterpretContext() { }

    public InterpretContext( InterpretContext context )
    {
        Scope = new InterpretScope( context.Scope.Values );
        Results = new Stack<object>( context.Results );
        Transition = context.Transition;
        TransitionChildIndex = 0;

        Reduced = context.Reduced;
        Transitions = context.Transitions;
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results )
    {
        scope = Scope;
        results = Results;
    }

    public Expression GetNextChild()
    {
        if ( TransitionChildIndex >= Transition.Children.Count )
            throw new InvalidOperationException( "No more child nodes." );

        return Transition.Children[TransitionChildIndex++];
    }
}
