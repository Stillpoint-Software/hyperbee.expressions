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
}
