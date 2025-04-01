using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Hyperbee.Expressions;

public class InjectExpression<T> : Expression, IDependencyInjectionExpression
{
    private IServiceProvider _serviceProvider;
    private readonly string _key;
    private readonly Expression _defaultValue;

    public InjectExpression( IServiceProvider serviceProvider, string key, Expression defaultValue )
    {
        _serviceProvider = serviceProvider;
        _key = key;
        _defaultValue = defaultValue;
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _serviceProvider ??= serviceProvider;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( T );
    public override bool CanReduce => true;

    private static readonly MethodInfo GetServiceMethodInfo = typeof( ServiceProviderServiceExtensions )
        .GetMethod( "GetService", [typeof( IServiceProvider )] )!
        .MakeGenericMethod( typeof( T ) );

    private static readonly MethodInfo GetKeyedServiceMethodInfo = typeof( ServiceProviderKeyedServiceExtensions )
        .GetMethod( "GetKeyedService", [typeof( IServiceProvider ), typeof( string )] )!
        .MakeGenericMethod( typeof( T ) );

    public override Expression Reduce()
    {
        if ( _serviceProvider == null )
        {
            throw new InvalidOperationException( "Service provider is not available." );
        }

        var serviceValue = Variable( typeof( T ), "serviceValue" );

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
                                Throw( New( ExceptionHelper.ConstructorInfo, Constant( "Service is not available." ) ), typeof( T ) );

        return Block(
            [serviceValue],
            Assign( serviceValue, callExpression ),
            Condition(
                NotEqual( serviceValue, Constant( null, typeof( T ) ) ),
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
    public static InjectExpression<T> Inject<T>( IServiceProvider serviceProvider, string key = null, Expression defaultValue = null )
    {
        return new InjectExpression<T>( serviceProvider, key, defaultValue );
    }

    public static InjectExpression<T> Inject<T>( string key = null, Expression defaultValue = null )
    {
        return new InjectExpression<T>( null, key, defaultValue );
    }
}
