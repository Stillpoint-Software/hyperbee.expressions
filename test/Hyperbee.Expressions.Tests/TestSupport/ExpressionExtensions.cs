using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests.TestSupport;

public static class ExpressionExtensions
{
    public static string GetDebugView( this Expression expression )
    {
        var debugViewProperty = typeof( Expression ).GetProperty( "DebugView", BindingFlags.Instance | BindingFlags.NonPublic );
        return debugViewProperty?.GetValue( expression ) as string ?? string.Empty;
    }
}
