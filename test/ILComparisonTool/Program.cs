using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace ILComparisonTool;

internal static class Program
{
    static void Main()
    {
        var outputDir = Path.Combine( AppContext.BaseDirectory, "il-output" );
        Directory.CreateDirectory( outputDir );

        Console.WriteLine( $"Output directory: {outputDir}" );
        Console.WriteLine();

        var expressions = BuildExpressions();

        foreach ( var (name, lambda, delegateType) in expressions )
        {
            Console.WriteLine( $"=== {name} ===" );
            Console.WriteLine();

            var secIl = CompileAndExtract( "SEC", name, () => lambda.Compile() );
            var fecIl = CompileAndExtract( "FEC", name, () => ExpressionCompiler.CompileFast( lambda, ifFastFailedReturnNull: true )! );
            var hecIl = CompileAndExtract( "HEC", name, () => HyperbeeCompiler.Compile( lambda ) );

            // Write individual files
            File.WriteAllText( Path.Combine( outputDir, $"{name}_SEC.il" ), secIl ?? "(failed)" );
            File.WriteAllText( Path.Combine( outputDir, $"{name}_FEC.il" ), fecIl ?? "(failed)" );
            File.WriteAllText( Path.Combine( outputDir, $"{name}_HEC.il" ), hecIl ?? "(failed)" );

            // Write side-by-side comparison
            WriteSideBySide( outputDir, name, secIl, fecIl, hecIl );

            Console.WriteLine();
        }

        // Also save HEC to a PersistedAssemblyBuilder .dll for ILSpy
        SaveHecPersistedAssembly( outputDir, expressions );

        // Write master summary
        WriteMasterSummary( outputDir, expressions );

        Console.WriteLine( $"Done. Files written to: {outputDir}" );
    }

    static string? CompileAndExtract( string compiler, string exprName, Func<Delegate> compile )
    {
        try
        {
            var del = compile();
            if ( del == null )
            {
                Console.WriteLine( $"  {compiler}: compile returned null" );
                return null;
            }

            // DynamicMethod.GetMethodBody() throws in .NET Core.
            // Extract IL bytes via internal reflection on DynamicResolver/ILGenerator.
            var ilBytes = DynamicMethodILExtractor.TryGetILBytes( del );
            if ( ilBytes == null || ilBytes.Length == 0 )
            {
                Console.WriteLine( $"  {compiler}: could not extract IL bytes" );
                return null;
            }

            var formatted = RawILFormatter.Format( ilBytes, del );

            Console.WriteLine( $"  {compiler}: {ilBytes.Length} bytes" );

            return formatted;
        }
        catch ( Exception ex )
        {
            Console.WriteLine( $"  {compiler}: FAILED - {ex.GetType().Name}: {ex.Message}" );
            return null;
        }
    }

    static void WriteSideBySide( string outputDir, string name, string? sec, string? fec, string? hec )
    {
        var sb = new StringBuilder();
        sb.AppendLine( $"IL Comparison: {name}" );
        sb.AppendLine( new string( '=', 80 ) );
        sb.AppendLine();

        sb.AppendLine( "--- SEC (System Expression Compiler) ---" );
        sb.AppendLine( sec ?? "(not available)" );
        sb.AppendLine();

        sb.AppendLine( "--- FEC (Fast Expression Compiler) ---" );
        sb.AppendLine( fec ?? "(not available)" );
        sb.AppendLine();

        sb.AppendLine( "--- HEC (Hyperbee Expression Compiler) ---" );
        sb.AppendLine( hec ?? "(not available)" );
        sb.AppendLine();

        // Quick metrics
        var secLines = CountInstructions( sec );
        var fecLines = CountInstructions( fec );
        var hecLines = CountInstructions( hec );

        sb.AppendLine( "--- Summary ---" );
        sb.AppendLine( $"  SEC: {secLines} instructions" );
        sb.AppendLine( $"  FEC: {fecLines} instructions" );
        sb.AppendLine( $"  HEC: {hecLines} instructions" );
        if ( secLines > 0 && hecLines > 0 )
        {
            var ratio = (double) hecLines / secLines;
            sb.AppendLine( $"  HEC/SEC ratio: {ratio:F2}x" );
        }
        if ( fecLines > 0 && hecLines > 0 )
        {
            var ratio = (double) hecLines / fecLines;
            sb.AppendLine( $"  HEC/FEC ratio: {ratio:F2}x" );
        }

        File.WriteAllText( Path.Combine( outputDir, $"{name}_comparison.txt" ), sb.ToString() );
    }

    static int CountInstructions( string? il )
    {
        if ( il == null ) return 0;
        return il.Split( '\n' ).Count( line => line.Length > 4 && line[4] == ':' );
    }

    static void SaveHecPersistedAssembly( string outputDir, List<(string Name, LambdaExpression Lambda, Type DelegateType)> expressions )
    {
        try
        {
            var pab = new PersistedAssemblyBuilder( new AssemblyName( "HEC_IL_Output" ), typeof( object ).Assembly );
            var mod = pab.DefineDynamicModule( "Module" );
            var tb = mod.DefineType( "CompiledExpressions", TypeAttributes.Public | TypeAttributes.Class );

            foreach ( var (name, lambda, _) in expressions )
            {
                try
                {
                    var paramTypes = lambda.Parameters.Select( p => p.Type ).ToArray();
                    var mb = tb.DefineMethod(
                        name,
                        MethodAttributes.Public | MethodAttributes.Static,
                        lambda.ReturnType,
                        paramTypes );

                    // Name the parameters for readability in ILSpy
                    for ( var i = 0; i < lambda.Parameters.Count; i++ )
                    {
                        mb.DefineParameter( i + 1, ParameterAttributes.None, lambda.Parameters[i].Name );
                    }

                    HyperbeeCompiler.CompileToMethod( lambda, mb );
                    Console.WriteLine( $"  PersistedAssembly: {name} OK" );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"  PersistedAssembly: {name} FAILED - {ex.Message}" );
                }
            }

            tb.CreateType();

            var dllPath = Path.Combine( outputDir, "HEC_IL_Output.dll" );
            using var stream = File.Create( dllPath );
            pab.Save( stream );

            Console.WriteLine( $"  Saved: {dllPath}" );
        }
        catch ( Exception ex )
        {
            Console.WriteLine( $"  PersistedAssemblyBuilder FAILED: {ex.GetType().Name}: {ex.Message}" );
        }
    }

    static void WriteMasterSummary( string outputDir, List<(string Name, LambdaExpression Lambda, Type DelegateType)> expressions )
    {
        var sb = new StringBuilder();
        sb.AppendLine( "IL Comparison Master Summary" );
        sb.AppendLine( new string( '=', 80 ) );
        sb.AppendLine();

        sb.AppendLine( $"{"Expression",-20} {"SEC Inst",10} {"FEC Inst",10} {"HEC Inst",10} {"HEC/SEC",10} {"HEC/FEC",10}" );
        sb.AppendLine( new string( '-', 70 ) );

        foreach ( var (name, _, _) in expressions )
        {
            var secFile = Path.Combine( outputDir, $"{name}_SEC.il" );
            var fecFile = Path.Combine( outputDir, $"{name}_FEC.il" );
            var hecFile = Path.Combine( outputDir, $"{name}_HEC.il" );

            var secCount = File.Exists( secFile ) ? CountInstructions( File.ReadAllText( secFile ) ) : 0;
            var fecCount = File.Exists( fecFile ) ? CountInstructions( File.ReadAllText( fecFile ) ) : 0;
            var hecCount = File.Exists( hecFile ) ? CountInstructions( File.ReadAllText( hecFile ) ) : 0;

            var hecSecRatio = secCount > 0 ? $"{(double) hecCount / secCount:F2}x" : "N/A";
            var hecFecRatio = fecCount > 0 ? $"{(double) hecCount / fecCount:F2}x" : "N/A";

            sb.AppendLine( $"{name,-20} {secCount,10} {fecCount,10} {hecCount,10} {hecSecRatio,10} {hecFecRatio,10}" );
        }

        var summaryPath = Path.Combine( outputDir, "_SUMMARY.txt" );
        File.WriteAllText( summaryPath, sb.ToString() );
    }

    // --- Expression definitions (mirror BenchmarkExpressions) ---

    static List<(string Name, LambdaExpression Lambda, Type DelegateType)> BuildExpressions()
    {
        var list = new List<(string, LambdaExpression, Type)>();

        // Tier 1: Simple — binary op, no closures
        Expression<Func<int, int, int>> simple = ( a, b ) => a + b;
        list.Add( ("Simple", simple, typeof( Func<int, int, int> )) );

        // Tier 2: Closure — captures an outer variable (but as embeddable constant)
        {
            var p = Expression.Parameter( typeof( int ), "x" );
            var c = Expression.Constant( 42 );
            var closure = Expression.Lambda<Func<int, int>>( Expression.Add( p, c ), p );
            list.Add( ("Closure", closure, typeof( Func<int, int> )) );
        }

        // Tier 3: TryCatch — exception handling
        {
            var result = Expression.Variable( typeof( int ), "result" );
            var tryCatch = Expression.Lambda<Func<int>>(
                Expression.Block(
                    new[] { result },
                    Expression.TryCatch(
                        Expression.Assign( result, Expression.Constant( 42 ) ),
                        Expression.Catch( typeof( Exception ), Expression.Assign( result, Expression.Constant( -1 ) ) )
                    ),
                    result
                ) );
            list.Add( ("TryCatch", tryCatch, typeof( Func<int> )) );
        }

        // Tier 4: Complex — conditional + cast + method call
        {
            var obj = Expression.Parameter( typeof( object ), "obj" );
            var complex = Expression.Lambda<Func<object, string>>(
                Expression.Condition(
                    Expression.TypeIs( obj, typeof( string ) ),
                    Expression.Call( Expression.Convert( obj, typeof( string ) ),
                        typeof( string ).GetMethod( "ToUpper", Type.EmptyTypes )! ),
                    Expression.Constant( "(not a string)" )
                ),
                obj );
            list.Add( ("Complex", complex, typeof( Func<object, string> )) );
        }

        // Tier 5: Loop — while loop with break
        {
            var n = Expression.Parameter( typeof( int ), "n" );
            var sum = Expression.Variable( typeof( int ), "sum" );
            var i = Expression.Variable( typeof( int ), "i" );
            var breakLabel = Expression.Label( typeof( int ), "break" );
            var loop = Expression.Lambda<Func<int, int>>(
                Expression.Block(
                    new[] { sum, i },
                    Expression.Assign( sum, Expression.Constant( 0 ) ),
                    Expression.Assign( i, Expression.Constant( 1 ) ),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThanOrEqual( i, n ),
                            Expression.Block(
                                Expression.Assign( sum, Expression.Add( sum, i ) ),
                                Expression.Assign( i, Expression.Add( i, Expression.Constant( 1 ) ) )
                            ),
                            Expression.Break( breakLabel, sum )
                        ),
                        breakLabel
                    )
                ),
                n );
            list.Add( ("Loop", loop, typeof( Func<int, int> )) );
        }

        // Tier 6: Switch — switch with multiple cases
        {
            var val = Expression.Parameter( typeof( int ), "val" );
            var sw = Expression.Lambda<Func<int, string>>(
                Expression.Switch(
                    val,
                    Expression.Constant( "other" ),
                    Expression.SwitchCase( Expression.Constant( "one" ), Expression.Constant( 1 ) ),
                    Expression.SwitchCase( Expression.Constant( "two" ), Expression.Constant( 2 ) ),
                    Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) )
                ),
                val );
            list.Add( ("Switch", sw, typeof( Func<int, string> )) );
        }

        return list;
    }
}
