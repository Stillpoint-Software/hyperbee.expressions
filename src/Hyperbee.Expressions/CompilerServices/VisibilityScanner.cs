using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Reports whether an expression references anything that is not publicly visible.
/// </summary>
/// <remarks>
/// An expression tree may name members that source could not: a <c>DynamicMethod</c> is
/// created with visibility checks skipped, so a private method or a constant of a private
/// type compiles and runs. A body emitted into a <c>MethodBuilder</c> gets no such
/// exemption, so a coroutine whose body reaches a non-public member keeps the delegate form.
/// Emitting into the type is an optimization; it must never narrow what compiles.
/// </remarks>
internal sealed class VisibilityScanner : ExpressionVisitor
{
    private bool _found;

    public static bool HasNonPublicReferences( IReadOnlyList<Expression> expressions )
    {
        var scanner = new VisibilityScanner();

        for ( var index = 0; index < expressions.Count && !scanner._found; index++ )
        {
            scanner.Visit( expressions[index] );
        }

        return scanner._found;
    }

    public override Expression Visit( Expression node )
    {
        if ( _found || node == null )
            return node;

        if ( node != null && !IsVisible( node.Type ) )
        {
            _found = true;
            return node;
        }

        return base.Visit( node );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        Check( node.Method );
        return base.VisitMethodCall( node );
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        Check( node.Member );
        return base.VisitMember( node );
    }

    protected override Expression VisitNew( NewExpression node )
    {
        if ( node.Constructor != null )
            Check( node.Constructor );

        return base.VisitNew( node );
    }

    protected override Expression VisitIndex( IndexExpression node )
    {
        if ( node.Indexer != null )
            Check( node.Indexer );

        return base.VisitIndex( node );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.Method != null )
            Check( node.Method );

        return base.VisitBinary( node );
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        if ( node.Method != null )
            Check( node.Method );

        return base.VisitUnary( node );
    }

    private void Check( MemberInfo member )
    {
        if ( _found )
            return;

        _found = member switch
        {
            MethodInfo method => !method.IsPublic || !IsVisible( method.DeclaringType )
                || HasNonPublicGenericArgument( method ),
            ConstructorInfo constructor => !constructor.IsPublic || !IsVisible( constructor.DeclaringType ),
            FieldInfo field => !field.IsPublic || !IsVisible( field.DeclaringType ),
            PropertyInfo property => !IsVisible( property.DeclaringType ),
            _ => !IsVisible( member.DeclaringType )
        };
    }

    private static bool HasNonPublicGenericArgument( MethodInfo method )
    {
        // Asking a non-generic method for its arguments is the common case, and it answers
        // without allocating.

        if ( !method.IsGenericMethod )
            return false;

        var arguments = method.GetGenericArguments();

        for ( var index = 0; index < arguments.Length; index++ )
        {
            if ( !IsVisible( arguments[index] ) )
                return true;
        }

        return false;
    }

    private static bool IsVisible( Type type )
    {
        if ( type == null )
            return true;

        if ( type.IsByRef || type.IsPointer || type.IsArray )
            return IsVisible( type.GetElementType() );

        if ( type.IsGenericParameter )
            return true;

        for ( var declaring = type; declaring != null; declaring = declaring.DeclaringType )
        {
            if ( !( declaring.IsPublic || declaring.IsNestedPublic ) )
                return false;
        }

        if ( !type.IsGenericType )
            return true;

        foreach ( var argument in type.GetGenericArguments() )
        {
            if ( !IsVisible( argument ) )
                return false;
        }

        return true;
    }
}
