using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// The cost of a delegate shape, with no compiler involved: the same <c>a + b</c> body
/// reached as an open static delegate and as one closed over a leading argument.
/// </summary>
/// <remarks>
/// Delegate.Invoke always passes a target in the first slot. A delegate over a static method
/// with nothing bound has no target to put there, so the runtime inserts a thunk that shifts
/// every argument down one before the call. Binding a leading parameter removes the thunk.
///
/// The System compiler always emits a leading Closure parameter and binds it, whether the
/// body needs one or not, which is why its IL reads ldarg.1 / ldarg.2 for a two-parameter
/// lambda. HEC emits ldarg.0 / ldarg.1 and binds nothing unless the body needs a constants
/// array. This measures what that costs.
/// </remarks>
[Config( typeof( BenchmarkConfig.Config ) )]
public class DelegateShapeBenchmarks
{
    private const int Count = 1000;

    private Func<int, int, int> _open = null!;
    private Func<int, int, int> _closed = null!;

    [GlobalSetup]
    public void Setup()
    {
        _open = CreateOpen();
        _closed = CreateClosed();
    }

    // static int F( int a, int b ) => a + b;  -- nothing bound

    private static Func<int, int, int> CreateOpen()
    {
        var method = new DynamicMethod(
            string.Empty,
            typeof( int ),
            [typeof( int ), typeof( int )],
            typeof( DelegateShapeBenchmarks ),
            skipVisibility: true );

        var il = method.GetILGenerator();

        il.Emit( OpCodes.Ldarg_0 );
        il.Emit( OpCodes.Ldarg_1 );
        il.Emit( OpCodes.Add );
        il.Emit( OpCodes.Ret );

        return method.CreateDelegate<Func<int, int, int>>();
    }

    // static int F( object closure, int a, int b ) => a + b;  -- closure bound as the target

    private static Func<int, int, int> CreateClosed()
    {
        var method = new DynamicMethod(
            string.Empty,
            typeof( int ),
            [typeof( object ), typeof( int ), typeof( int )],
            typeof( DelegateShapeBenchmarks ),
            skipVisibility: true );

        var il = method.GetILGenerator();

        il.Emit( OpCodes.Ldarg_1 );
        il.Emit( OpCodes.Ldarg_2 );
        il.Emit( OpCodes.Add );
        il.Emit( OpCodes.Ret );

        return (Func<int, int, int>) method.CreateDelegate( typeof( Func<int, int, int> ), new object() );
    }

    private static int Run( Func<int, int, int> compiled )
    {
        var total = 0;

        for ( var index = 0; index < Count; index++ )
            total += compiled( index, 4 );

        return total;
    }

    [Benchmark( Description = "a+b x1000 | open static delegate" )]
    public int Open() => Run( _open );

    [Benchmark( Description = "a+b x1000 | closed over leading arg" )]
    public int Closed() => Run( _closed );
}
