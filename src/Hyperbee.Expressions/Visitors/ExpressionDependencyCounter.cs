using System.Linq.Expressions;

namespace Hyperbee.Expressions.Visitors;

internal class ExpressionDependencyCounter : ExpressionVisitor
{
    private readonly Dictionary<Expression, int> _countDictionary = new();
    private readonly Func<Expression, bool> _matchPredicate;
    private int _counter;

    public ExpressionDependencyCounter( Func<Expression, bool> matchPredicate )
    {
        _matchPredicate = matchPredicate ?? throw new ArgumentNullException( nameof( matchPredicate ) );
    }

    public int GetDependencyCount( Expression expression )
    {
        if ( expression == null )
            throw new ArgumentNullException( nameof( expression ) );

        if ( _countDictionary.TryGetValue( expression, out var count ) )
            return count;

        _counter = 0;
        Visit( expression );

        return _countDictionary[expression];
    }

    public void Clear()
    {
        _countDictionary.Clear();
        _counter = 0;
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null || _countDictionary.ContainsKey( node ) )
            return node;

        var parentCount = _counter;
        _counter = 0;

        var result = base.Visit( node );

        var childCount = _counter;
        _counter += parentCount;

        _countDictionary[node] = childCount;

        return result;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( _matchPredicate( node ) )
        {
            _counter++;
        }

        return base.VisitExtension( node );
    }
}
