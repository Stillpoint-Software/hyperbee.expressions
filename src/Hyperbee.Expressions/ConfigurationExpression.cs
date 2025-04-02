using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Hyperbee.Expressions;

public class ConfigurationExpression : Expression, IDependencyInjectionExpression
{
    private IConfiguration _configuration;
    private readonly Type _type;

    public ConfigurationExpression( Type type, string key )
    {
        _type = type;
        Key = key;
    }

    public ConfigurationExpression( Type type, IConfiguration configuration, string key )
    {
        _type = type;
        _configuration = configuration;
        Key = key;
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _configuration ??= serviceProvider?.GetService( typeof( IConfiguration ) ) as IConfiguration;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => _type;
    public string Key { get; init; }
    public override bool CanReduce => true;

    private MethodInfo GetValueMethodInfo => typeof( ConfigurationBinder )
        .GetMethod( "GetValue", [typeof( IConfiguration ), typeof( string )] )!
        .MakeGenericMethod( _type );

    public override Expression Reduce()
    {
        if ( _configuration == null )
        {
            throw new InvalidOperationException( "Configuration is not available." );
        }

        return Call(
            null,
            GetValueMethodInfo,
            [Constant( _configuration ), Constant( Key )] );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return this;
    }
}
public static partial class ExpressionExtensions
{
    public static ConfigurationExpression ConfigurationValue( Type type, string key )
    {
        return new ConfigurationExpression( type, key );
    }

    public static ConfigurationExpression ConfigurationValue( Type type, IConfiguration configuration, string key )
    {
        return new ConfigurationExpression( type, configuration, key );
    }

    public static ConfigurationExpression ConfigurationValue<T>( string key )
    {
        return new ConfigurationExpression( typeof( T ), key );
    }

    public static ConfigurationExpression ConfigurationValue<T>( IConfiguration configuration, string key )
    {
        return new ConfigurationExpression( typeof( T ), configuration, key );
    }
}
