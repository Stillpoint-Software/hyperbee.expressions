using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;


// Temp hack to make sure all nodes are reduced before analyzing
internal sealed class LoweringVisitor : ExpressionVisitor
{
    protected override Expression VisitExtension( Expression node ) => Visit( node.ReduceAndCheck() );
}


internal sealed class AnalyzerVisitor : ExpressionVisitor
{
    private readonly List<Expression> _currentPath = new( 8 );

    private readonly Dictionary<LabelTarget, List<Expression>> _labelPaths = new();
    private readonly Dictionary<GotoExpression, List<Expression>> _gotoPaths = new();

    public Dictionary<GotoExpression, Navigation> Navigation { get; } = new();
    public Expression Lowered { get; private set; }

    private Dictionary<Expression, Expression> _extensions;  // not needed if always reduced?

    public void Analyze( Expression root, Dictionary<Expression, Expression> extensions )
    {
        _gotoPaths.Clear();
        _labelPaths.Clear();
        Navigation.Clear();

        _extensions = extensions;

        var reduced = new LoweringVisitor().Visit( root );  // TOOD: fix
        Lowered = Visit( reduced );
        ResolveNavigationPaths();
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
            return null;

        //if ( node.NodeType == ExpressionType.Extension )
        //    return base.Visit( node );
       
        _currentPath.Add( node );
        var result = base.Visit( node );
        _currentPath.RemoveAt( _currentPath.Count - 1 );

        return result;
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        _labelPaths[node.Target] = [.. _currentPath];
        return base.VisitLabel( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        _gotoPaths[node] = [.. _currentPath];
        return base.VisitGoto( node );
    }

    //protected override Expression VisitExtension( Expression node )
    //{
    //    if ( _extensions.TryGetValue( node, out var reduced ) )
    //    {
    //        return reduced;
    //    }

    //    reduced = node.ReduceAndCheck();

    //    _currentPath.Add( reduced );
    //    var loweredAndReduced = base.Visit( reduced );
    //    _currentPath.RemoveAt( _currentPath.Count - 1 );

    //    _extensions[node] = loweredAndReduced;
    //    return loweredAndReduced;
    //}

    protected override Expression VisitLoop( LoopExpression node )
    {
        if ( node.BreakLabel == null && node.ContinueLabel == null )
        {
            return base.VisitLoop( node );
        }

        var currentPath = _currentPath.ToList();

        if ( node.BreakLabel != null )
            _labelPaths[node.BreakLabel] = currentPath;

        if ( node.ContinueLabel != null )
            _labelPaths[node.ContinueLabel] = currentPath;

        return base.VisitLoop( node );
    }

    private void ResolveNavigationPaths()
    {
        foreach ( var (gotoExpr, gotoPath) in _gotoPaths )
        {
            if ( !_labelPaths.TryGetValue( gotoExpr.Target, out var labelPath ) )
            {
                throw new InvalidOperationException( $"Label target {gotoExpr.Target.Name} not found." );
            }

            Navigation[gotoExpr] = CreateNavigationExpression( gotoPath, labelPath, gotoExpr.Target );
        }
    }

    private static Navigation CreateNavigationExpression( List<Expression> gotoPath, List<Expression> labelPath, LabelTarget targetLabel )
    {
        var minLength = Math.Min( gotoPath.Count, labelPath.Count );
        var ancestorIndex = 0;

        while ( ancestorIndex < minLength && gotoPath[ancestorIndex] == labelPath[ancestorIndex] )
        {
            ancestorIndex++;
        }

        if ( ancestorIndex == 0 )
            throw new InvalidOperationException( "Could not determine a common ancestor." );

        var commonAncestorExpr = labelPath[ancestorIndex - 1];
        var steps = labelPath.Skip( ancestorIndex ).ToList();

        return new Navigation( commonAncestorExpr, steps, targetLabel );
    }
}

internal sealed class Navigation
{
    public Expression CommonAncestor { get; }
    public List<Expression> Steps { get; }
    public LabelTarget TargetLabel { get; }
    public Exception Exception { get; }

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

