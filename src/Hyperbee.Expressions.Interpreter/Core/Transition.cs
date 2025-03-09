using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class Transition
{
    public Expression CommonAncestor { get; }
    public List<Expression> Children { get; }

    public LabelTarget TargetLabel { get; }
    public Exception Exception { get; set; }

    private int _currentChildIndex;

    public Transition( Expression commonAncestor = null, List<Expression> children = null, LabelTarget targetLabel = null, Exception exception = null )
    {
        CommonAncestor = commonAncestor;
        Children = children ?? [];
        TargetLabel = targetLabel;
        Exception = exception;

        _currentChildIndex = 0;
    }

    public void Reset() => _currentChildIndex = 0;

    public Expression GetNextChild()
    {
        if ( _currentChildIndex >= Children.Count )
            throw new InvalidOperationException( "No more child nodes." );

        return Children[_currentChildIndex++];
    }
}
