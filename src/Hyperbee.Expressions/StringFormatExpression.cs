
using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class StringFormatExpression : Expression
{
    public Expression Format { get; }
    public IReadOnlyList<Expression> Arguments { get; }

    internal StringFormatExpression( Expression format, params Expression[] arguments )
    {
        ArgumentNullException.ThrowIfNull( format, nameof(format) );
        ArgumentNullException.ThrowIfNull( arguments, nameof(arguments) );

        if ( format.Type != typeof(string) )
            throw new ArgumentException( "Format expression must be of type string.", nameof(format) );

        Format = format;
        Arguments = arguments.ToList();
    }

    public override Type Type => typeof(string);
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( !Arguments.Any() )
            return Format; 

        var argsArrayExpression = NewArrayInit(
            typeof(object),
            Arguments.Select( arg => Convert( arg, typeof(object) ) )
        );

        var formatMethod = typeof(string).GetMethod( "Format", [typeof(string), typeof(object[])] );

        if ( formatMethod == null )
            throw new InvalidOperationException( "string.Format(string, object[]) not found." );
 
        return Call( formatMethod, Format, argsArrayExpression );
    }
}

public static partial class ExpressionExtensions
{
    public static StringFormatExpression StringFormat( Expression format, params Expression[] arguments )
    {
        return new StringFormatExpression( format, arguments );
    }
}

