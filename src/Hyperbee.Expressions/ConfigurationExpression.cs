using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Hyperbee.Expressions;

public class ConfigurationExpression<T> : Expression, IDependencyInjectionExpression
{
    private IConfiguration _configuration;
    private readonly string _key;

    public ConfigurationExpression( string key )
    {
        _key = key;
    }

    public ConfigurationExpression( IConfiguration configuration, string key )
    {
        _configuration = configuration;
        _key = key;
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _configuration ??= serviceProvider?.GetService( typeof( IConfiguration ) ) as IConfiguration;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( T );
    public override bool CanReduce => true;

    private static readonly MethodInfo GetValueMethodInfo = typeof( ConfigurationBinder )
        .GetMethod( "GetValue", [typeof( IConfiguration ), typeof( string )] )!
        .MakeGenericMethod( typeof( T ) );

    public override Expression Reduce()
    {
        if ( _configuration == null )
        {
            throw new InvalidOperationException( "Configuration is not available." );
        }

        return Call(
            null,
            GetValueMethodInfo,
            [Constant( _configuration ), Constant( _key )] );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return this;
    }
}
public static partial class ExpressionExtensions
{
    public static ConfigurationExpression<T> ConfigurationValue<T>( string key )
    {
        return new ConfigurationExpression<T>( key );
    }

    public static ConfigurationExpression<T> ConfigurationValue<T>( IConfiguration configuration, string key )
    {
        return new ConfigurationExpression<T>( configuration, key );
    }
}
