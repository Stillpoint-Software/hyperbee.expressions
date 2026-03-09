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

    // ================================================================
    // TryFault — fault block runs only on exception
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryFault_FaultRunsOnException( CompilerType compilerType )
    {
        // var ran = false;
        // try { throw new InvalidOperationException(); }
        // fault { ran = true; }
        // return ran; — unreachable but we catch outside
        var ran = Expression.Variable( typeof( bool ), "ran" );
        var exParam = Expression.Parameter( typeof( Exception ), "ex" );
        var outer = Expression.Variable( typeof( bool ), "outer" );

        var tryFault = Expression.TryFault(
            Expression.Throw( Expression.New( typeof( InvalidOperationException ) ), typeof( void ) ),
            Expression.Block( typeof( void ), Expression.Assign( ran, Expression.Constant( true ) ) ) );

        var body = Expression.Block(
            new[] { ran, outer },
            Expression.Assign( ran, Expression.Constant( false ) ),
            Expression.Assign( outer, Expression.Constant( false ) ),
            Expression.TryCatch(
                tryFault,
                Expression.Catch(
                    exParam,
                    Expression.Block( typeof( void ), Expression.Assign( outer, Expression.Constant( true ) ) ) ) ),
            Expression.And( ran, outer ) );

        var lambda = Expression.Lambda<Func<bool>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn() );  // fault ran AND outer catch ran
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryFault_FaultDoesNotRunWithoutException( CompilerType compilerType )
    {
        // var ran = false;
        // try { /* no throw */ } fault { ran = true; }
        // return ran;  // should be false
        var ran = Expression.Variable( typeof( bool ), "ran" );

        var body = Expression.Block(
            new[] { ran },
            Expression.Assign( ran, Expression.Constant( false ) ),
            Expression.TryFault(
                Expression.Constant( 42 ),  // no throw
                Expression.Assign( ran, Expression.Constant( true ) ) ),
            ran );

        var lambda = Expression.Lambda<Func<bool>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn() );  // fault should NOT have run
    }

    // ================================================================
    // Exception variable access in catch
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_ExceptionVariable_AccessMessage( CompilerType compilerType )
    {
        // try { throw new Exception("hello"); }
        // catch (Exception ex) { return ex.Message; }
        var ex = Expression.Parameter( typeof( Exception ), "ex" );
        var msgProp = typeof( Exception ).GetProperty( "Message" )!;

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Throw(
                    Expression.New(
                        typeof( Exception ).GetConstructor( [typeof( string )] )!,
                        Expression.Constant( "hello" ) ),
                    typeof( string ) ),
                Expression.Catch(
                    ex,
                    Expression.Property( ex, msgProp ) ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_ExceptionVariable_AccessType( CompilerType compilerType )
    {
        // try { throw new ArgumentNullException(); }
        // catch (Exception ex) { return ex.GetType().Name; }
        var ex = Expression.Parameter( typeof( Exception ), "ex" );
        var getTypeMethod = typeof( object ).GetMethod( "GetType" )!;
        var nameProp = typeof( Type ).GetProperty( "Name" )!;

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Throw(
                    Expression.New( typeof( ArgumentNullException ) ),
                    typeof( string ) ),
                Expression.Catch(
                    ex,
                    Expression.Property(
                        Expression.Call( ex, getTypeMethod ),
                        nameProp ) ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "ArgumentNullException", fn() );
    }

    // ================================================================
    // Filter edge cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_FilterFalse_FallsToNextHandler( CompilerType compilerType )
    {
        // try { throw new Exception(); }
        // catch (InvalidOperationException) when (false) { return "wrong"; }
        // catch (Exception) { return "right"; }
        var ex1 = Expression.Parameter( typeof( Exception ), "ex1" );
        var ex2 = Expression.Parameter( typeof( Exception ), "ex2" );

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Throw( Expression.New( typeof( Exception ) ), typeof( string ) ),
                Expression.Catch(
                    ex1,
                    Expression.Constant( "wrong" ),
                    Expression.Constant( false ) ),
                Expression.Catch(
                    ex2,
                    Expression.Constant( "right" ) ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "right", fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_MultipleFilterHandlers_FirstMatchWins( CompilerType compilerType )
    {
        // try { throw new ArgumentException(); }
        // catch (ArgumentException ex) when (true) { return "first"; }
        // catch (Exception ex) { return "second"; }
        var ex1 = Expression.Parameter( typeof( ArgumentException ), "ex1" );
        var ex2 = Expression.Parameter( typeof( Exception ), "ex2" );

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Throw( Expression.New( typeof( ArgumentException ) ), typeof( string ) ),
                Expression.Catch(
                    ex1,
                    Expression.Constant( "first" ),
                    Expression.Constant( true ) ),
                Expression.Catch(
                    ex2,
                    Expression.Constant( "second" ) ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "first", fn() );
    }

    // ================================================================
    // Nested try — inner finally runs before outer
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_NestedTry_InnerFinallyRunsFirst( CompilerType compilerType )
    {
        // var log = "";
        // try {
        //   try { throw; } finally { log += "inner"; }
        // } catch { log += "catch"; }
        // return log;
        var log = Expression.Variable( typeof( string ), "log" );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );
        var concatMethod = typeof( string ).GetMethod( "Concat", [typeof( string ), typeof( string )] )!;

        var innerTry = Expression.TryFinally(
            Expression.Throw( Expression.New( typeof( Exception ) ), typeof( void ) ),
            Expression.Assign( log, Expression.Call( null, concatMethod, log, Expression.Constant( "inner;" ) ) ) );

        var outerTry = Expression.TryCatch(
            innerTry,
            Expression.Catch(
                ex,
                Expression.Block(
                    typeof( void ),
                    Expression.Assign( log, Expression.Call( null, concatMethod, log, Expression.Constant( "catch" ) ) ) ) ) );

        var body = Expression.Block(
            new[] { log },
            Expression.Assign( log, Expression.Constant( "" ) ),
            outerTry,
            log );

        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "inner;catch", fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_NestedTry_BothFinallyRun( CompilerType compilerType )
    {
        // var x = 0;
        // try { try { x = 1; } finally { x += 10; } }
        // finally { x += 100; }
        // return x;  // expects 111
        var x = Expression.Variable( typeof( int ), "x" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.TryFinally(
                Expression.TryFinally(
                    Expression.Assign( x, Expression.Constant( 1 ) ),
                    Expression.Assign( x, Expression.Add( x, Expression.Constant( 10 ) ) ) ),
                Expression.Assign( x, Expression.Add( x, Expression.Constant( 100 ) ) ) ),
            x );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 111, fn() );
    }

    // ================================================================
    // Void body catch — side effect only
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_VoidBody_Catch_SideEffect( CompilerType compilerType )
    {
        // var result = "ok";
        // try { throw new Exception(); } catch { result = "caught"; }
        // return result;
        var result = Expression.Variable( typeof( string ), "result" );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );

        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( "ok" ) ),
            Expression.TryCatch(
                Expression.Throw( Expression.New( typeof( Exception ) ), typeof( void ) ),
                Expression.Catch(
                    ex,
                    Expression.Block(
                        typeof( void ),
                        Expression.Assign( result, Expression.Constant( "caught" ) ) ) ) ),
            result );

        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "caught", fn() );
    }

    // ================================================================
    // Catch by base type — derived exception caught by base handler
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_DerivedExceptionCaughtByBase( CompilerType compilerType )
    {
        // try { throw new ArgumentNullException(); }
        // catch (ArgumentException) { return "caught"; }
        var ex = Expression.Parameter( typeof( ArgumentException ), "ex" );

        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Throw( Expression.New( typeof( ArgumentNullException ) ), typeof( string ) ),
                Expression.Catch( ex, Expression.Constant( "caught" ) ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "caught", fn() );
    }

    // ================================================================
    // TryCatchFinally — all three blocks
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatchFinally_AllThreeBlocks_ExceptionPath( CompilerType compilerType )
    {
        // var x = 0;
        // try { throw; x = 1; }
        // catch (Exception) { x = 2; }
        // finally { x += 10; }
        // return x; // expects 12
        var x = Expression.Variable( typeof( int ), "x" );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.TryCatchFinally(
                Expression.Block(
                    Expression.Throw( Expression.New( typeof( Exception ) ), typeof( void ) ),
                    Expression.Assign( x, Expression.Constant( 1 ) ) ),
                Expression.Assign( x, Expression.Add( x, Expression.Constant( 10 ) ) ),
                Expression.Catch( ex, Expression.Assign( x, Expression.Constant( 2 ) ) ) ),
            x );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 12, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatchFinally_AllThreeBlocks_SuccessPath( CompilerType compilerType )
    {
        // var x = 0;
        // try { x = 1; }
        // catch (Exception) { x = 2; }
        // finally { x += 10; }
        // return x; // expects 11
        var x = Expression.Variable( typeof( int ), "x" );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.TryCatchFinally(
                Expression.Assign( x, Expression.Constant( 1 ) ),
                Expression.Assign( x, Expression.Add( x, Expression.Constant( 10 ) ) ),
                Expression.Catch( ex, Expression.Assign( x, Expression.Constant( 2 ) ) ) ),
            x );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn() );
    }

    // ================================================================
    // Rethrow inside catch
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_Rethrow_PropagatesOriginalException( CompilerType compilerType )
    {
        // try {
        //   try { throw new InvalidOperationException("original"); }
        //   catch (Exception) { rethrow; }
        // } catch (Exception ex) { return ex.Message; }
        var result = Expression.Variable( typeof( string ), "result" );
        var ex1 = Expression.Parameter( typeof( Exception ), "ex1" );
        var ex2 = Expression.Parameter( typeof( Exception ), "ex2" );
        var msgProp = typeof( Exception ).GetProperty( "Message" )!;

        var lambda = Expression.Lambda<Func<string>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.TryCatch(
                        Expression.Throw(
                            Expression.New(
                                typeof( InvalidOperationException ).GetConstructor( [typeof( string )] )!,
                                Expression.Constant( "original" ) ),
                            typeof( void ) ),
                        Expression.Catch( ex1, Expression.Rethrow( typeof( void ) ) ) ),
                    Expression.Catch(
                        ex2,
                        Expression.Block(
                            typeof( void ),
                            Expression.Assign( result, Expression.Property( ex2, msgProp ) ) ) ) ),
                result ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "original", fn() );
    }

    // ================================================================
    // TryCatch — int result from multiple paths
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_IntResult_FromMultiplePaths( CompilerType compilerType )
    {
        var result = Expression.Variable( typeof( int ), "result" );
        var body = Expression.Block(
            new[] { result },
            Expression.TryCatch(
                Expression.Assign( result, Expression.Constant( 1 ) ),
                Expression.Catch( typeof( Exception ), Expression.Assign( result, Expression.Constant( 2 ) ) ) ),
            result );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn() );
    }

    // ================================================================
    // TryCatch — derived exception caught by base
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_ArgumentNullException_CaughtByArgumentException( CompilerType compilerType )
    {
        var ex = Expression.Parameter( typeof( ArgumentException ), "ex" );
        var result = Expression.Variable( typeof( string ), "result" );
        var msgProp = typeof( ArgumentException ).GetProperty( "Message" )!;
        var body = Expression.Block(
            new[] { result },
            Expression.TryCatch(
                Expression.Block( typeof( void ),
                    Expression.Throw( Expression.New(
                        typeof( ArgumentNullException ).GetConstructor( [typeof( string )] )!,
                        Expression.Constant( "param" ) ) ) ),
                Expression.Catch( ex,
                    Expression.Block( typeof( void ),
                        Expression.Assign( result, Expression.Property( ex, msgProp ) ) ) ) ),
            result );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        var msg = fn();
        Assert.IsNotNull( msg );
        Assert.IsTrue( msg.Length > 0 );
    }

    // ================================================================
    // TryCatch — exception filter always true
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_ExceptionFilter_AlwaysTrue_Catches( CompilerType compilerType )
    {
        var result = Expression.Variable( typeof( int ), "result" );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );
        var body = Expression.Block(
            new[] { result },
            Expression.TryCatch(
                Expression.Block( typeof( void ),
                    Expression.Throw( Expression.New( typeof( InvalidOperationException ) ) ) ),
                Expression.MakeCatchBlock(
                    typeof( Exception ), ex,
                    Expression.Block( typeof( void ),
                        Expression.Assign( result, Expression.Constant( 42 ) ) ),
                    Expression.Constant( true ) ) ),
            result );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // TryCatch — void body with finally side effect
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_FinallyAlwaysExecutes_EvenWithoutException( CompilerType compilerType )
    {
        var flag = Expression.Variable( typeof( int ), "flag" );
        var body = Expression.Block(
            new[] { flag },
            Expression.TryFinally(
                Expression.Assign( flag, Expression.Constant( 1 ) ),
                Expression.Assign( flag, Expression.Constant( 2 ) ) ),
            flag );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    // ================================================================
    // TryCatch — multiple handlers, second matches
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_MultipleHandlers_SecondMatches( CompilerType compilerType )
    {
        var result = Expression.Variable( typeof( int ), "result" );
        var body = Expression.Block(
            new[] { result },
            Expression.TryCatch(
                Expression.Block( typeof( void ),
                    Expression.Throw( Expression.New( typeof( ArgumentException ) ) ) ),
                Expression.Catch( typeof( InvalidOperationException ),
                    Expression.Block( typeof( void ),
                        Expression.Assign( result, Expression.Constant( 1 ) ) ) ),
                Expression.Catch( typeof( ArgumentException ),
                    Expression.Block( typeof( void ),
                        Expression.Assign( result, Expression.Constant( 2 ) ) ) ) ),
            result );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    // ================================================================
    // TryCatch — throw with message, catch and read message
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_ThrowNewException_CatchReadsMessage( CompilerType compilerType )
    {
        var ex = Expression.Parameter( typeof( InvalidOperationException ), "ex" );
        var msgProp = typeof( Exception ).GetProperty( "Message" )!;
        var ctor = typeof( InvalidOperationException ).GetConstructor( [typeof( string )] )!;
        var body = Expression.TryCatch(
            Expression.Block( typeof( string ),
                Expression.Throw( Expression.New( ctor, Expression.Constant( "boom" ) ) ),
                Expression.Constant( "" ) ),
            Expression.Catch( ex, Expression.Property( ex, msgProp ) ) );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "boom", fn() );
    }

    // ================================================================
    // TryCatch — nested try; outer finally always runs
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_NestedTry_OuterFinallyRunsAfterInner( CompilerType compilerType )
    {
        var log = Expression.Variable( typeof( int ), "log" );
        var body = Expression.Block(
            new[] { log },
            Expression.TryFinally(
                Expression.TryFinally(
                    Expression.Assign( log, Expression.Constant( 1 ) ),
                    Expression.Assign( log, Expression.Add( log, Expression.Constant( 10 ) ) ) ),
                Expression.Assign( log, Expression.Add( log, Expression.Constant( 100 ) ) ) ),
            log );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 111, fn() );  // 1 + 10 + 100
    }

    // ================================================================
    // TryCatch — string result on success vs exception
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TryCatch_StringResult_SuccessAndFailurePaths( CompilerType compilerType )
    {
        var flag = Expression.Parameter( typeof( bool ), "flag" );
        var result = Expression.Variable( typeof( string ), "result" );
        var ctor = typeof( Exception ).GetConstructor( [typeof( string )] )!;
        var body = Expression.Block(
            new[] { result },
            Expression.TryCatch(
                Expression.Assign( result,
                    Expression.Condition( flag,
                        Expression.Constant( "ok" ),
                        Expression.Block( typeof( string ),
                            Expression.Throw( Expression.New( ctor, Expression.Constant( "err" ) ) ),
                            Expression.Constant( "" ) ) ) ),
                Expression.Catch( typeof( Exception ),
                    Expression.Assign( result, Expression.Constant( "caught" ) ) ) ),
            result );
        var lambda = Expression.Lambda<Func<bool, string>>( body, flag );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "ok", fn( true ) );
        Assert.AreEqual( "caught", fn( false ) );
    }
}
