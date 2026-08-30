using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

// Finds variables an expression references but does not itself bind.
//
// A state-machine body that is closed can be compiled in isolation and embedded as a
// constant delegate. One that reads an enclosing variable cannot: compiled on its own it
// would lose the variable, so it has to be compiled as part of the enclosing expression,
// where the compiler can share the variable through its own closure mechanism.
//
// Scopes are not tracked. The question is only whether isolated compilation loses a
// variable, and a declaration anywhere inside the expression binds it.

internal sealed class FreeVariableScanner : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _bound = [];
    private readonly HashSet<ParameterExpression> _referenced = [];
    private readonly HashSet<ParameterExpression> _assigned = [];

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

    /// <summary>
    /// The variables referenced by the given expressions that none of them declares.
    /// <paramref name="assigned"/> receives the subset that is also written.
    /// </summary>
    public static HashSet<ParameterExpression> Find(
        IReadOnlyList<ParameterExpression> declared,
        IReadOnlyList<Expression> expressions,
        out HashSet<ParameterExpression> assigned )
    {
        var scanner = new FreeVariableScanner();

        scanner.Bind( declared );

        for ( var index = 0; index < expressions.Count; index++ )
        {
            scanner.Visit( expressions[index] );
        }

        var free = new HashSet<ParameterExpression>();

        foreach ( var variable in scanner._referenced )
        {
            if ( !scanner._bound.Contains( variable ) )
                free.Add( variable );
        }

        assigned = [];

        foreach ( var variable in scanner._assigned )
        {
            if ( free.Contains( variable ) )
                assigned.Add( variable );
        }

        return free;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        _referenced.Add( node );
        return node;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.Left is ParameterExpression target && IsAssignment( node.NodeType ) )
            _assigned.Add( target );

        return base.VisitBinary( node );
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        if ( node.Operand is ParameterExpression target && IsAssignment( node.NodeType ) )
            _assigned.Add( target );

        return base.VisitUnary( node );
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

    private static bool IsAssignment( ExpressionType nodeType )
    {
        return nodeType is ExpressionType.Assign
            or ExpressionType.AddAssign or ExpressionType.AddAssignChecked
            or ExpressionType.SubtractAssign or ExpressionType.SubtractAssignChecked
            or ExpressionType.MultiplyAssign or ExpressionType.MultiplyAssignChecked
            or ExpressionType.DivideAssign or ExpressionType.ModuloAssign
            or ExpressionType.PowerAssign
            or ExpressionType.AndAssign or ExpressionType.OrAssign or ExpressionType.ExclusiveOrAssign
            or ExpressionType.LeftShiftAssign or ExpressionType.RightShiftAssign
            or ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
            or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign;
    }
}
