using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class AnalyzerVisitor : ExpressionVisitor
{
    private readonly List<Expression> _currentPath = new( 8 );

    private readonly Dictionary<LabelTarget, List<Expression>> _labelPaths = new();
    private readonly Dictionary<GotoExpression, List<Expression>> _gotoPaths = new();

    //private readonly Dictionary<Expression, Expression> _replacements = new();

    public Dictionary<GotoExpression, Transition> Transitions { get; } = new();
    public Expression Reduced { get; private set; }

    public void Analyze( Expression root )
    {
        _gotoPaths.Clear();
        _labelPaths.Clear();

        Transitions.Clear();

        Reduced = Visit( root );

        ResolveTransitions();
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
            return null;

        _currentPath.Add( node );
        var result = base.Visit( node );

        if ( !ReferenceEquals( node, result ) )
        {
            FixExpressionPaths( node, result, _gotoPaths );
            FixExpressionPaths( node, result, _labelPaths );
        }

        _currentPath.RemoveAt( _currentPath.Count - 1 );
        return result;

        static void FixExpressionPaths<T>( Expression original, Expression replacement, Dictionary<T, List<Expression>> paths )
        {
            foreach ( var expressions in paths.Values )
            {
                for ( var i = 0; i < expressions.Count; i++ )
                {
                    if ( ReferenceEquals( expressions[i], original ) )
                    {
                        expressions[i] = replacement;
                    }
                }
            }
        }
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

    protected override Expression VisitExtension( Expression node )
    {
        return Visit( node.ReduceAndCheck() )!;
    }

    private void ResolveTransitions()
    {
        //FixUpdatedExpressions( _replacements, _gotoPaths );
        //FixUpdatedExpressions( _replacements, _labelPaths );

        foreach ( var (gotoExpr, gotoPath) in _gotoPaths )
        {
            if ( !_labelPaths.TryGetValue( gotoExpr.Target, out var labelPath ) )
            {
                throw new InvalidOperationException( $"Label target {gotoExpr.Target.Name} not found." );
            }

            Transitions[gotoExpr] = CreateTransition( gotoPath, labelPath, gotoExpr.Target );
        }

        // return;

        //static void FixUpdatedExpressions<T>( Dictionary<Expression, Expression> replacements, Dictionary<T, List<Expression>> paths )
        //{
        //    foreach ( var expressions in paths.Values )
        //    {
        //        for ( var i = 0; i < expressions.Count; i++ )
        //        {
        //            if ( replacements.TryGetValue( expressions[i], out var replace ) )
        //            {
        //                expressions[i] = replace;
        //            }
        //        }
        //    }
        //}
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
