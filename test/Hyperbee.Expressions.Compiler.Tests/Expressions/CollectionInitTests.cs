using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class CollectionInitTests
{
    // ================================================================
    // ListInit with List<int>
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ListInit_IntList_ReturnsPopulatedList( CompilerType compilerType )
    {
        // () => new List<int> { 1, 2, 3 }
        var ctor = typeof( List<int> ).GetConstructor( Type.EmptyTypes )!;
        var addMethod = typeof( List<int> ).GetMethod( "Add" )!;

        var lambda = Expression.Lambda<Func<List<int>>>(
            Expression.ListInit(
                Expression.New( ctor ),
                Expression.ElementInit( addMethod, Expression.Constant( 1 ) ),
                Expression.ElementInit( addMethod, Expression.Constant( 2 ) ),
                Expression.ElementInit( addMethod, Expression.Constant( 3 ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.Count );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
        Assert.AreEqual( 3, result[2] );
    }

    // ================================================================
    // ListInit with Dictionary
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ListInit_Dictionary_ReturnsPopulatedDictionary( CompilerType compilerType )
    {
        // () => new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }
        var ctor = typeof( Dictionary<string, int> ).GetConstructor( Type.EmptyTypes )!;
        var addMethod = typeof( Dictionary<string, int> ).GetMethod( "Add" )!;

        var lambda = Expression.Lambda<Func<Dictionary<string, int>>>(
            Expression.ListInit(
                Expression.New( ctor ),
                Expression.ElementInit( addMethod, Expression.Constant( "a" ), Expression.Constant( 1 ) ),
                Expression.ElementInit( addMethod, Expression.Constant( "b" ), Expression.Constant( 2 ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 2, result.Count );
        Assert.AreEqual( 1, result["a"] );
        Assert.AreEqual( 2, result["b"] );
    }

    // ================================================================
    // MemberInit with simple object
    // ================================================================

    public class SimpleDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_SimpleDto_SetsProperties( CompilerType compilerType )
    {
        // () => new SimpleDto { Id = 42, Name = "test" }
        var ctor = typeof( SimpleDto ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<SimpleDto>>(
            Expression.MemberInit(
                Expression.New( ctor ),
                Expression.Bind( typeof( SimpleDto ).GetProperty( "Id" )!, Expression.Constant( 42 ) ),
                Expression.Bind( typeof( SimpleDto ).GetProperty( "Name" )!, Expression.Constant( "test" ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 42, result.Id );
        Assert.AreEqual( "test", result.Name );
    }

    // ================================================================
    // MemberInit with field assignment
    // ================================================================

    public class DtoWithField
    {
        public int Value;
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_FieldAssignment_SetsField( CompilerType compilerType )
    {
        // () => new DtoWithField { Value = 99 }
        var ctor = typeof( DtoWithField ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<DtoWithField>>(
            Expression.MemberInit(
                Expression.New( ctor ),
                Expression.Bind( typeof( DtoWithField ).GetField( "Value" )!, Expression.Constant( 99 ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 99, result.Value );
    }

    // ================================================================
    // MemberInit — nested object initialization
    // ================================================================

    public class AddressDto
    {
        public string? City { get; set; }
        public string? Country { get; set; }
    }

    public class PersonDto
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public AddressDto? Address { get; set; }
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_NestedObject_SetsNestedProperties( CompilerType compilerType )
    {
        // () => new PersonDto { Name = "Alice", Age = 30, Address = new AddressDto { City = "NY", Country = "US" } }
        var personCtor = typeof( PersonDto ).GetConstructor( Type.EmptyTypes )!;
        var addressCtor = typeof( AddressDto ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<PersonDto>>(
            Expression.MemberInit(
                Expression.New( personCtor ),
                Expression.Bind( typeof( PersonDto ).GetProperty( "Name" )!, Expression.Constant( "Alice" ) ),
                Expression.Bind( typeof( PersonDto ).GetProperty( "Age" )!, Expression.Constant( 30 ) ),
                Expression.Bind( typeof( PersonDto ).GetProperty( "Address" )!,
                    Expression.MemberInit(
                        Expression.New( addressCtor ),
                        Expression.Bind( typeof( AddressDto ).GetProperty( "City" )!, Expression.Constant( "NY" ) ),
                        Expression.Bind( typeof( AddressDto ).GetProperty( "Country" )!, Expression.Constant( "US" ) ) ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( "Alice", result.Name );
        Assert.AreEqual( 30, result.Age );
        Assert.IsNotNull( result.Address );
        Assert.AreEqual( "NY", result.Address!.City );
        Assert.AreEqual( "US", result.Address.Country );
    }

    // ================================================================
    // MemberInit — nullable property
    // ================================================================

    public class NullablePropDto
    {
        public int? Value { get; set; }
        public string? Label { get; set; }
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_NullableProperty_SetsNullable( CompilerType compilerType )
    {
        var ctor = typeof( NullablePropDto ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<NullablePropDto>>(
            Expression.MemberInit(
                Expression.New( ctor ),
                Expression.Bind( typeof( NullablePropDto ).GetProperty( "Value" )!, Expression.Constant( 42, typeof( int? ) ) ),
                Expression.Bind( typeof( NullablePropDto ).GetProperty( "Label" )!, Expression.Constant( null, typeof( string ) ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 42, result.Value );
        Assert.IsNull( result.Label );
    }

    // ================================================================
    // ListInit — empty list
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ListInit_EmptyList( CompilerType compilerType )
    {
        var ctor = typeof( List<int> ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<List<int>>>(
            Expression.ListInit( Expression.New( ctor ), Array.Empty<ElementInit>() ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 0, result.Count );
    }

    // ================================================================
    // ListInit — string list
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ListInit_StringList_ReturnsPopulatedList( CompilerType compilerType )
    {
        var ctor = typeof( List<string> ).GetConstructor( Type.EmptyTypes )!;
        var addMethod = typeof( List<string> ).GetMethod( "Add" )!;

        var lambda = Expression.Lambda<Func<List<string>>>(
            Expression.ListInit(
                Expression.New( ctor ),
                Expression.ElementInit( addMethod, Expression.Constant( "hello" ) ),
                Expression.ElementInit( addMethod, Expression.Constant( "world" ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 2, result.Count );
        Assert.AreEqual( "hello", result[0] );
        Assert.AreEqual( "world", result[1] );
    }

    // ================================================================
    // MemberInit — multiple property types
    // ================================================================

    public class MultiTypeDto
    {
        public int IntVal { get; set; }
        public double DoubleVal { get; set; }
        public bool BoolVal { get; set; }
        public string? StrVal { get; set; }
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_MultiplePropertyTypes_SetsAll( CompilerType compilerType )
    {
        var ctor = typeof( MultiTypeDto ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Expression.Lambda<Func<MultiTypeDto>>(
            Expression.MemberInit(
                Expression.New( ctor ),
                Expression.Bind( typeof( MultiTypeDto ).GetProperty( "IntVal" )!, Expression.Constant( 10 ) ),
                Expression.Bind( typeof( MultiTypeDto ).GetProperty( "DoubleVal" )!, Expression.Constant( 3.14 ) ),
                Expression.Bind( typeof( MultiTypeDto ).GetProperty( "BoolVal" )!, Expression.Constant( true ) ),
                Expression.Bind( typeof( MultiTypeDto ).GetProperty( "StrVal" )!, Expression.Constant( "abc" ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 10, result.IntVal );
        Assert.AreEqual( 3.14, result.DoubleVal, 1e-9 );
        Assert.IsTrue( result.BoolVal );
        Assert.AreEqual( "abc", result.StrVal );
    }

    // ================================================================
    // MemberInit — ListBind (populate a list property)
    // ================================================================

    public class DtoWithList
    {
        public List<int> Items { get; } = [];
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_ListBind_PopulatesListProperty( CompilerType compilerType )
    {
        // () => new DtoWithList { Items = { 1, 2, 3 } }  (uses MemberListBinding)
        var ctor = typeof( DtoWithList ).GetConstructor( Type.EmptyTypes )!;
        var addMethod = typeof( List<int> ).GetMethod( "Add" )!;
        var itemsProp = typeof( DtoWithList ).GetProperty( "Items" )!;

        var lambda = Expression.Lambda<Func<DtoWithList>>(
            Expression.MemberInit(
                Expression.New( ctor ),
                Expression.ListBind(
                    itemsProp,
                    Expression.ElementInit( addMethod, Expression.Constant( 1 ) ),
                    Expression.ElementInit( addMethod, Expression.Constant( 2 ) ),
                    Expression.ElementInit( addMethod, Expression.Constant( 3 ) ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.Items.Count );
        Assert.AreEqual( 1, result.Items[0] );
        Assert.AreEqual( 2, result.Items[1] );
        Assert.AreEqual( 3, result.Items[2] );
    }

    // ================================================================
    // MemberInit with constructor that takes arguments
    // ================================================================

    public class DtoWithCtorArgs
    {
        public int X { get; }
        public string? Label { get; set; }

        public DtoWithCtorArgs( int x ) { X = x; }
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void MemberInit_CtorWithArgs_SetsPropertyAfter( CompilerType compilerType )
    {
        var ctor = typeof( DtoWithCtorArgs ).GetConstructor( [typeof( int )] )!;

        var lambda = Expression.Lambda<Func<DtoWithCtorArgs>>(
            Expression.MemberInit(
                Expression.New( ctor, Expression.Constant( 7 ) ),
                Expression.Bind( typeof( DtoWithCtorArgs ).GetProperty( "Label" )!, Expression.Constant( "ok" ) ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 7, result.X );
        Assert.AreEqual( "ok", result.Label );
    }

    // ================================================================
    // ListInit — HashSet (no ordering guarantee, just count)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ListInit_HashSet_NoOrder( CompilerType compilerType )
    {
        // FEC known bug: FEC generates invalid IL for ListInit when the Add method returns a
        // non-void value (HashSet<T>.Add returns bool). See FecKnownIssues.Pattern22.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC generates invalid IL for ListInit with non-void Add method. See FecKnownIssues.Pattern22." );

        var ctor = typeof( HashSet<string> ).GetConstructor( Type.EmptyTypes )!;
        var addMethod = typeof( HashSet<string> ).GetMethod( "Add" )!;

        var lambda = Expression.Lambda<Func<HashSet<string>>>(
            Expression.ListInit(
                Expression.New( ctor ),
                Expression.ElementInit( addMethod, Expression.Constant( "a" ) ),
                Expression.ElementInit( addMethod, Expression.Constant( "b" ) ),
                Expression.ElementInit( addMethod, Expression.Constant( "a" ) ) ) );  // duplicate

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 2, result.Count );  // "a" deduplicated
        Assert.IsTrue( result.Contains( "a" ) );
        Assert.IsTrue( result.Contains( "b" ) );
    }
}
