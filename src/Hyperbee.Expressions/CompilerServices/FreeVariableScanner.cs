using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

// Determines whether a lambda references variables that it does not itself bind.
//
// A state-machine body that is closed can be compiled in isolation and embedded as a
// constant delegate. One that reads an enclosing variable cannot: compiled on its own it
// would lose the variable, so it has to be compiled as part of the enclosing expression,
// where the compiler can share the variable through its own closure mechanism.
//
// Scopes are not tracked. The question is only whether isolated compilation loses a
// variable, and a declaration anywhere inside the lambda binds it.

internal sealed class FreeVariableScanner : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _bound = [];
    private readonly HashSet<ParameterExpression> _referenced = [];

    public static bool HasFreeVariables( LambdaExpression lambda )
    {
        var scanner = new FreeVariableScanner();

        scanner.Visit( lambda );

        foreach ( var variable in scanner._referenced )
        {
            if ( !scanner._bound.Contains( variable ) )
                return true;
        }

        return false;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        _referenced.Add( node );
        return node;
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        Bind( node.Parameters );
        return base.VisitLambda( node );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        Bind( node.Variables );
        return base.VisitBlock( node );
    }

    protected override CatchBlock VisitCatchBlock( CatchBlock node )
    {
        if ( node.Variable != null )
            _bound.Add( node.Variable );

        return base.VisitCatchBlock( node );
    }

    protected override Expression VisitExtension( Expression node )
    {
        switch ( node )
        {
            case AsyncBlockExpression asyncBlock:
                Bind( asyncBlock.Variables );
                break;

            case EnumerableBlockExpression enumerableBlock:
                Bind( enumerableBlock.Variables );
                break;
        }

        return base.VisitExtension( node );
    }

    private void Bind( IReadOnlyList<ParameterExpression> variables )
    {
        for ( var index = 0; index < variables.Count; index++ )
        {
            _bound.Add( variables[index] );
        }
    }
}
