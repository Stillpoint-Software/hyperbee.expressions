using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace ILComparisonTool;

internal static class DiagDump
{
    public static void Run()
    {
        Expression<Func<int, int, int>> expr = (a, b) => a + b;
        var del = expr.Compile();
        var method = del.Method;

        Console.WriteLine($"del.Method type: {method.GetType().FullName}");
        Console.WriteLine($"del.Method is DynamicMethod: {method is DynamicMethod}");
        Console.WriteLine();

        // Dump all fields on RTDynamicMethod (or whatever type del.Method is)
        Console.WriteLine($"--- Fields on {method.GetType().Name} ---");
        foreach (var f in method.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Console.WriteLine($"  {f.FieldType.Name} {f.Name} = {TryGet(f, method)}");

        // In .NET 9, del.Method IS the DynamicMethod directly
        var dm = method as DynamicMethod;
        if (dm != null)
        {
            // Get resolver (field is _resolver in .NET 9)
            var resolverField = typeof(DynamicMethod).GetField("_resolver", BindingFlags.NonPublic | BindingFlags.Instance);
            var resolver = resolverField?.GetValue(dm);
            if (resolver != null)
            {
                Console.WriteLine();
                Console.WriteLine($"--- Fields on {resolver.GetType().Name} (hierarchy) ---");
                var t = resolver.GetType();
                while (t != null && t != typeof(object))
                {
                    Console.WriteLine($"  Type: {t.Name}");
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        Console.WriteLine($"    {f.FieldType.Name} {f.Name} = {TryGet(f, resolver)}");
                    t = t.BaseType;
                }
            }
            else
            {
                Console.WriteLine("  _resolver is null");
            }

            // Also dump ILGenerator hierarchy
            var ilgField = typeof(DynamicMethod).GetField("_ilGenerator", BindingFlags.NonPublic | BindingFlags.Instance);
            var ilg = ilgField?.GetValue(dm);
            if (ilg != null)
            {
                Console.WriteLine();
                Console.WriteLine($"--- Fields on ILGenerator hierarchy ---");
                var t = ilg.GetType();
                while (t != null && t != typeof(object))
                {
                    Console.WriteLine($"  Type: {t.Name}");
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        Console.WriteLine($"    {f.FieldType.Name} {f.Name} = {TryGet(f, ilg)}");
                    t = t.BaseType;
                }
            }
        }
        else
        {
            Console.WriteLine("  del.Method is not a DynamicMethod — unexpected");
        }
    }

    static string TryGet(FieldInfo f, object obj)
    {
        try
        {
            var val = f.GetValue(obj);
            if (val == null) return "null";
            if (val is byte[] bytes) return $"byte[{bytes.Length}]";
            if (val is Array arr) return $"{val.GetType().Name}[{arr.Length}]";
            return val.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"<error: {ex.Message}>";
        }
    }
}
