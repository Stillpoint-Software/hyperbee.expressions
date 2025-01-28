using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions;

public class StringFormatExpression : Expression
{
    public Expression FormatProvider { get; }
    public Expression Format { get; }
    public IReadOnlyList<Expression> Arguments { get; }

    private static readonly MethodInfo StringFormatMethod;

    static StringFormatExpression()
    {
        StringFormatMethod = typeof( string ).GetMethod( "Format", [typeof( IFormatProvider ), typeof( string ), typeof( object[] )] );
    }

    internal StringFormatExpression( Expression formatProvider, Expression format, Expression[] arguments )
    {
        ArgumentNullException.ThrowIfNull( format, nameof( format ) );
        ArgumentNullException.ThrowIfNull( arguments, nameof( arguments ) );

        if ( format.Type != typeof( string ) )
            throw new ArgumentException( "Format expression must be of type string.", nameof( format ) );

        if ( formatProvider != null && !typeof( IFormatProvider ).IsAssignableFrom( formatProvider.Type ) )
            throw new ArgumentException( "Format provider must implement IFormatProvider.", nameof( formatProvider ) );

        FormatProvider = formatProvider ?? Constant( null, typeof( IFormatProvider ) );
        Format = format;
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
        return new StringFormatExpression( null, format, [argument] );
    }

    public static StringFormatExpression StringFormat( Expression format, Expression[] arguments )
    {
        return new StringFormatExpression( null, format, arguments );
    }

    public static StringFormatExpression StringFormat( Expression formatProvider, Expression format, Expression[] arguments )
    {
        return new StringFormatExpression( formatProvider, format, arguments );
    }
}

