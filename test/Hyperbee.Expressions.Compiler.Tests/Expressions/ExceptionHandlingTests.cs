using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ExceptionHandlingTests
{
    // ================================================================
    // Basic try/catch
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_NoException_ReturnsBodyResult( CompilerType compilerType )
    {
        // try { 42 } catch (Exception) { -1 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Constant( 42 ),
                Expression.Catch( typeof( Exception ), Expression.Constant( -1 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_WithException_ReturnsCatchResult( CompilerType compilerType )
    {
        // try { throw new InvalidOperationException(); 0 } catch (Exception) { -1 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( InvalidOperationException ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( Exception ), Expression.Constant( -1 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_TypedCatch_MatchesCorrectHandler( CompilerType compilerType )
    {
        // try { throw new InvalidOperationException(); 0 }
        // catch (ArgumentException) { 1 }
        // catch (InvalidOperationException) { 2 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( InvalidOperationException ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( ArgumentException ), Expression.Constant( 1 ) ),
                Expression.Catch( typeof( InvalidOperationException ), Expression.Constant( 2 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_MultipleCatchHandlers_FirstMatchWins( CompilerType compilerType )
    {
        // try { throw new ArgumentNullException(); 0 }
        // catch (ArgumentNullException) { 10 }
        // catch (ArgumentException) { 20 }      -- base class, but should not match
        // catch (Exception) { 30 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( ArgumentNullException ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( ArgumentNullException ), Expression.Constant( 10 ) ),
                Expression.Catch( typeof( ArgumentException ), Expression.Constant( 20 ) ),
                Expression.Catch( typeof( Exception ), Expression.Constant( 30 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_CatchVariable_AccessExceptionObject( CompilerType compilerType )
    {
        // try { throw new InvalidOperationException("test message"); "" }
        // catch (InvalidOperationException ex) { ex.Message }
        var ex = Expression.Parameter( typeof( InvalidOperationException ), "ex" );
        var msgProp = typeof( Exception ).GetProperty( nameof( Exception.Message ) )!;

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( string ),
                    Expression.Throw(
                        Expression.New(
                            typeof( InvalidOperationException ).GetConstructor( new[] { typeof( string ) } )!,
                            Expression.Constant( "test message" ) ) ),
                    Expression.Constant( "" ) ),
                Expression.Catch(
                    ex,
                    Expression.Property( ex, msgProp ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "test message", fn() );
    }

    // ================================================================
    // Try/catch/finally
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatchFinally_FinallyExecutes( CompilerType compilerType )
    {
        // var result = 0;
        // try { result = 1; } catch (Exception) { result = -1; } finally { result = result + 100; }
        // return result;
        var result = Expression.Variable( typeof( int ), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.Assign( result, Expression.Constant( 0 ) ),
                Expression.TryCatchFinally(
                    Expression.Assign( result, Expression.Constant( 1 ) ),
                    Expression.Assign( result, Expression.Add( result, Expression.Constant( 100 ) ) ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 101, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatchFinally_WithException_CatchAndFinallyBothRun( CompilerType compilerType )
    {
        // var result = 0;
        // try { throw new Exception(); } catch (Exception) { result = 10; } finally { result = result + 100; }
        // return result;
        var result = Expression.Variable( typeof( int ), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.Assign( result, Expression.Constant( 0 ) ),
                Expression.TryCatchFinally(
                    Expression.Block(
                        Expression.Throw( Expression.New( typeof( Exception ) ) ),
                        Expression.Assign( result, Expression.Constant( 1 ) )
                    ),
                    Expression.Assign( result, Expression.Add( result, Expression.Constant( 100 ) ) ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Assign( result, Expression.Constant( 10 ) ) )
                ),
                result
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 110, fn() );
    }

    // ================================================================
    // Throw / Rethrow
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Throw_ExceptionPropagates( CompilerType compilerType )
    {
        // () => { throw new InvalidOperationException("boom"); return 0; }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof( int ),
                Expression.Throw( Expression.New(
                    typeof( InvalidOperationException ).GetConstructor( new[] { typeof( string ) } )!,
                    Expression.Constant( "boom" ) ) ),
                Expression.Constant( 0 )
            ) );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn(); }
        catch ( InvalidOperationException ex ) when ( ex.Message == "boom" )
        {
            threw = true;
        }

        Assert.IsTrue( threw, "Expected InvalidOperationException with message 'boom'." );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Throw_InsideTry_CatchHandlesIt( CompilerType compilerType )
    {
        // try { throw new ArgumentException(); 0 } catch (ArgumentException) { 99 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( ArgumentException ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( ArgumentException ), Expression.Constant( 99 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Rethrow_InsideCatch_ExceptionPropagates( CompilerType compilerType )
    {
        // try {
        //   try { throw new InvalidOperationException("inner"); 0 }
        //   catch (InvalidOperationException) { rethrow; 0 }
        // } catch (InvalidOperationException) { 42 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.TryCatch(
                    Expression.Block(
                        typeof( int ),
                        Expression.Throw( Expression.New(
                            typeof( InvalidOperationException ).GetConstructor( new[] { typeof( string ) } )!,
                            Expression.Constant( "inner" ) ) ),
                        Expression.Constant( 0 ) ),
                    Expression.Catch( typeof( InvalidOperationException ),
                        Expression.Block(
                            typeof( int ),
                            Expression.Rethrow( typeof( void ) ),
                            Expression.Constant( 0 ) ) )
                ),
                Expression.Catch( typeof( InvalidOperationException ), Expression.Constant( 42 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Return from try (Goto/Label)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ReturnLabel_FromTryBody( CompilerType compilerType )
    {
        // { try { return 42; } catch (Exception) { return -1; } }
        // Label(return, 0)
        var returnLabel = Expression.Label( typeof( int ), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof( int ),
                Expression.TryCatch(
                    Expression.Return( returnLabel, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ReturnLabel_FromCatchBody( CompilerType compilerType )
    {
        // { try { throw; return 42; } catch (Exception) { return -1; } }
        // Label(return, 0)
        var returnLabel = Expression.Label( typeof( int ), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof( int ),
                Expression.TryCatch(
                    Expression.Block(
                        Expression.Throw( Expression.New( typeof( InvalidOperationException ) ) ),
                        Expression.Return( returnLabel, Expression.Constant( 42 ) )
                    ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn() );
    }

    // ================================================================
    // Void try
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidTryCatch_SideEffectsOnly( CompilerType compilerType )
    {
        // var x = 0;
        // try { x = 1; } catch (Exception) { x = -1; }
        // return x;
        var x = Expression.Variable( typeof( int ), "x" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Constant( 0 ) ),
                Expression.TryCatch(
                    Expression.Assign( x, Expression.Constant( 1 ) ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Assign( x, Expression.Constant( -1 ) ) )
                ),
                x
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn() );
    }

    // ================================================================
    // Nested try
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedTry_InnerCatches_OuterDoesNot( CompilerType compilerType )
    {
        // try {
        //   try { throw new InvalidOperationException(); 0 }
        //   catch (InvalidOperationException) { 10 }
        // } catch (Exception) { 20 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.TryCatch(
                    Expression.Block(
                        typeof( int ),
                        Expression.Throw( Expression.New( typeof( InvalidOperationException ) ) ),
                        Expression.Constant( 0 ) ),
                    Expression.Catch( typeof( InvalidOperationException ), Expression.Constant( 10 ) )
                ),
                Expression.Catch( typeof( Exception ), Expression.Constant( 20 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedTry_TryInsideCatch( CompilerType compilerType )
    {
        // try { throw new Exception(); 0 }
        // catch (Exception) {
        //   try { 100 } catch (Exception) { 200 }
        // }
        // NOTE: CompilerType.Fast excluded -- FEC produces invalid IL for
        // nested try inside catch handler (known FEC limitation).
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( Exception ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( Exception ),
                    Expression.TryCatch(
                        Expression.Constant( 100 ),
                        Expression.Catch( typeof( Exception ), Expression.Constant( 200 ) ) ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 100, fn() );
    }

    // ================================================================
    // Try/catch with value result
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_IntResult_NoException( CompilerType compilerType )
    {
        // try { 42 } catch (Exception) { 0 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Constant( 42 ),
                Expression.Catch( typeof( Exception ), Expression.Constant( 0 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_CatchProvidesAlternateValue( CompilerType compilerType )
    {
        // try { throw new Exception(); 0 } catch (Exception) { 99 }
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    typeof( int ),
                    Expression.Throw( Expression.New( typeof( Exception ) ) ),
                    Expression.Constant( 0 ) ),
                Expression.Catch( typeof( Exception ), Expression.Constant( 99 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // TryCatch with Assign (FEC pattern)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_WithAssign_InBody( CompilerType compilerType )
    {
        // var result = 0;
        // try { result = 42; } catch (Exception) { result = -1; }
        // return result;
        var result = Expression.Variable( typeof( int ), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof( Exception ),
                        Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Goto / Label without try (basic control flow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GotoLabel_SimpleReturn( CompilerType compilerType )
    {
        // { return 42; label: 0 }
        var returnLabel = Expression.Label( typeof( int ), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof( int ),
                Expression.Return( returnLabel, Expression.Constant( 42 ) ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GotoLabel_FallthroughUsesDefault( CompilerType compilerType )
    {
        // The label's default value is used when falling through (no goto executed)
        // { label: 99 }
        var label = Expression.Label( typeof( int ), "myLabel" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof( int ),
                Expression.Label( label, Expression.Constant( 99 ) )
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // TryFinally (no catch)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryFinally_FinallyRuns( CompilerType compilerType )
    {
        // var x = 0;
        // try { x = 1; } finally { x = x + 10; }
        // return x;
        var x = Expression.Variable( typeof( int ), "x" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Constant( 0 ) ),
                Expression.TryFinally(
                    Expression.Assign( x, Expression.Constant( 1 ) ),
                    Expression.Assign( x, Expression.Add( x, Expression.Constant( 10 ) ) )
                ),
                x
            ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn() );
    }
}
