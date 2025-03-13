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

    public Dictionary<GotoExpression, Transition> Transitions { get; } = new();
    public Expression Reduced { get; private set; }

    public void Analyze( Expression root )
    {
        _gotoPaths.Clear();
        _labelPaths.Clear();

        Transitions.Clear();

        var reduced = new LoweringVisitor().Visit( root );  // TODO: fix

        Reduced = Visit( reduced );
        ResolveTransitions();
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
            return null;

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

    private void ResolveTransitions()
    {
        foreach ( var (gotoExpr, gotoPath) in _gotoPaths )
        {
            if ( !_labelPaths.TryGetValue( gotoExpr.Target, out var labelPath ) )
            {
                throw new InvalidOperationException( $"Label target {gotoExpr.Target.Name} not found." );
            }

            Transitions[gotoExpr] = CreateTransition( gotoPath, labelPath, gotoExpr.Target );
        }
    }

    private static Transition CreateTransition( List<Expression> gotoPath, List<Expression> labelPath, LabelTarget targetLabel )
    {
        var minLength = Math.Min( gotoPath.Count, labelPath.Count );
        var ancestorIndex = 0;

        while ( ancestorIndex < minLength && gotoPath[ancestorIndex] == labelPath[ancestorIndex] )
        {
            ancestorIndex++;
        }

        if ( ancestorIndex == 0 )
            throw new InvalidOperationException( "Could not determine a common ancestor." );

        var commonAncestor = labelPath[ancestorIndex - 1];
        var children = labelPath.Skip( ancestorIndex ).ToList();

        return new Transition( commonAncestor, children, targetLabel );
    }
}
