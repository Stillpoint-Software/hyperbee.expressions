
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions;

public class StringFormatExpression : Expression
{
    public Expression Format { get; }
    public IReadOnlyList<Expression> Arguments { get; }

    public Expression FormatProvider { get; }

    private static readonly MethodInfo StringFormatMethod;

    static StringFormatExpression()
    {
        StringFormatMethod = typeof( string ).GetMethod( "Format", [typeof( IFormatProvider ), typeof( string ), typeof( object[] )] );
    }

    internal StringFormatExpression( Expression format, Expression formatProvider, Expression[] arguments )
    {
        ArgumentNullException.ThrowIfNull( format, nameof( format ) );
        ArgumentNullException.ThrowIfNull( arguments, nameof( arguments ) );

        if ( format.Type != typeof( string ) )
            throw new ArgumentException( "Format expression must be of type string.", nameof( format ) );

        if ( formatProvider != null && !typeof( IFormatProvider ).IsAssignableFrom( formatProvider.Type ) )
            throw new ArgumentException( "Format provider must implement IFormatProvider.", nameof( formatProvider ) );

        Format = format;
        FormatProvider = formatProvider ?? Constant( null, typeof( IFormatProvider ) );
        Arguments = arguments.ToList();
    }

    public override Type Type => typeof( string );
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( !Arguments.Any() )
            return Format;

        var argsArrayExpression = NewArrayInit(
            typeof( object ),
            Arguments.Select( arg => Convert( arg, typeof( object ) ) )
        );

        if ( StringFormatMethod == null )
            throw new InvalidOperationException( "string Format method not found." );

        return Call( StringFormatMethod, FormatProvider, Format, argsArrayExpression );
    }
}

public static partial class ExpressionExtensions
{
    public static StringFormatExpression StringFormat( Expression format, Expression argument )
    {
        return new StringFormatExpression( format, null, [argument] );
    }

    public static StringFormatExpression StringFormat( Expression format, Expression[] arguments )
    {
        return new StringFormatExpression( format, null, arguments );
    }

    public static StringFormatExpression StringFormat( Expression format, Expression formatProvider, Expression[] arguments )
    {
        return new StringFormatExpression( format, formatProvider, arguments );
    }
}

