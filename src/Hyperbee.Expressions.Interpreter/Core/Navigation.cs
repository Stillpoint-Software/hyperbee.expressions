using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class Navigation
{
    public Expression CommonAncestor { get; }
    public List<Expression> Steps { get; }
    public LabelTarget TargetLabel { get; }
    public Exception Exception { get; set; }

    private int _currentStepIndex;

    public Navigation( Expression commonAncestor = null, List<Expression> steps = null, LabelTarget targetLabel = null, Exception exception = null )
    {
        CommonAncestor = commonAncestor;
        Steps = steps ?? [];
        TargetLabel = targetLabel;
        Exception = exception;
        _currentStepIndex = 0;
    }

    public void Reset() => _currentStepIndex = 0;

    public Expression GetNextStep()
    {
        if ( _currentStepIndex >= Steps.Count )
            throw new InvalidOperationException( "No more steps available." );

        return Steps[_currentStepIndex++];
    }
}
