using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Hyperbee.Expressions;

public class InjectExpression : Expression, IDependencyInjectionExpression
{
    private IServiceProvider _serviceProvider;
    private readonly string _key;
    private readonly Expression _defaultValue;
    private readonly Type _type;

    public InjectExpression( Type type, IServiceProvider serviceProvider, string key, Expression defaultValue )
    {
        _type = type;
        _serviceProvider = serviceProvider;
        _key = key;
        _defaultValue = defaultValue;
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _serviceProvider ??= serviceProvider;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => _type;
    public override bool CanReduce => true;

    private MethodInfo GetServiceMethodInfo => typeof( ServiceProviderServiceExtensions )
        .GetMethod( "GetService", [typeof( IServiceProvider )] )!
        .MakeGenericMethod( _type );

    private MethodInfo GetKeyedServiceMethodInfo => typeof( ServiceProviderKeyedServiceExtensions )
        .GetMethod( "GetKeyedService", [typeof( IServiceProvider ), typeof( string )] )!
        .MakeGenericMethod( _type );

    public override Expression Reduce()
    {
        if ( _serviceProvider == null )
        {
            throw new InvalidOperationException( "Service provider is not available." );
        }

        var serviceValue = Variable( _type, "serviceValue" );

        var callExpression = _key == null
            ? Call(
                null,
                GetServiceMethodInfo,
                Constant( _serviceProvider )
            )
            : Call(
                null,
                GetKeyedServiceMethodInfo,
                [Constant( _serviceProvider ), Constant( _key )]
            );

        var defaultExpression = _defaultValue ??
                                Throw( New( ExceptionHelper.ConstructorInfo, Constant( "Service is not available." ) ), _type );

        return Block(
            [serviceValue],
            Assign( serviceValue, callExpression ),
            Condition(
                NotEqual( serviceValue, Constant( null, _type ) ),
                serviceValue,
                defaultExpression
            )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return this;
    }
}

internal static class ExceptionHelper
{
    internal static readonly ConstructorInfo ConstructorInfo = typeof( InvalidOperationException )
        .GetConstructor( [typeof( string )] );
}

public static partial class ExpressionExtensions
{
    public static InjectExpression Inject( Type type, IServiceProvider serviceProvider, string key = null, Expression defaultValue = null )
    {
        return new InjectExpression( type, serviceProvider, key, defaultValue );
    }

    public static InjectExpression Inject( Type type, string key = null, Expression defaultValue = null )
    {
        return new InjectExpression( type, null, key, defaultValue );
    }

    public static InjectExpression Inject<T>( IServiceProvider serviceProvider, string key = null, Expression defaultValue = null )
    {
        return new InjectExpression( typeof( T ), serviceProvider, key, defaultValue );
    }

    public static InjectExpression Inject<T>( string key = null, Expression defaultValue = null )
    {
        return new InjectExpression( typeof( T ), null, key, defaultValue );
    }
}
