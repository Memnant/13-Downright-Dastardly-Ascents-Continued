using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using dda;

internal static class AllocationRegression
{
    internal static object Run(string baselineDll, Action<bool, string> check)
    {
        // Compare executable 1.5.1 policy with this source using the same .NET runtime.
        // This measures managed allocations in two routines, not Unity's total heap/RSS.
        var context = new AssemblyLoadContext("continued-baseline", isCollectible: true);
        try
        {
            var baseline = context.LoadFromAssemblyPath(Path.GetFullPath(baselineDll));
            check(baseline.GetName().Version!.ToString() == "1.5.1.0", "allocation baseline is 1.5.1");
            var oldPolicy = CreatePolicy(baseline);
            var newPolicy = CreatePolicy(typeof(RuleZeroPolicy).Assembly);
            var oldEncode = CreateEncode(baseline);
            var snapshot = new DifficultySnapshot(15, 0, true, true);
            Action newEncode = () => snapshot.Encode();
            const int iterations = 100000;
            long oldPolicyBytes = Measure(oldPolicy, iterations), newPolicyBytes = Measure(newPolicy, iterations);
            long oldEncodeBytes = Measure(oldEncode, iterations), newEncodeBytes = Measure(newEncode, iterations);
            check(newPolicyBytes < oldPolicyBytes, "reunion policy allocates less than 1.5.1");
            check(newEncodeBytes < oldEncodeBytes, "unchanged rules encoding allocates less than 1.5.1");
            check(ReferenceEquals(snapshot.Encode(), snapshot.Encode()), "immutable snapshot reuses encoded string");
            return new { Iterations = iterations, Runtime = ".NET 10; no Unity scene",
                ReunionBeforeBytes = oldPolicyBytes, ReunionAfterBytes = newPolicyBytes,
                RulesEncodeBeforeBytes = oldEncodeBytes, RulesEncodeAfterBytes = newEncodeBytes };
        }
        finally { context.Unload(); }
    }
    private static Action CreatePolicy(Assembly assembly)
    {
        var position = assembly.GetType("dda.ScoutPosition")!;
        var array = Array.CreateInstance(position, 2);
        array.SetValue(Activator.CreateInstance(position, 1, 0d, 0d, 0d), 0);
        array.SetValue(Activator.CreateInstance(position, 2, 0d, 151d, 0d), 1);
        var method = assembly.GetType("dda.RuleZeroPolicy")!.GetMethod("ShouldRelease")!;
        var call = Expression.Call(method, Expression.Constant(array, array.GetType()), Expression.Constant(2));
        return Expression.Lambda<Action>(Expression.Block(call, Expression.Empty())).Compile();
    }
    private static Action CreateEncode(Assembly assembly)
    {
        var type = assembly.GetType("dda.DifficultySnapshot")!;
        var instance = Activator.CreateInstance(type, 15, 0, true, true)!;
        var call = Expression.Call(Expression.Constant(instance), type.GetMethod("Encode")!);
        return Expression.Lambda<Action>(Expression.Block(call, Expression.Empty())).Compile();
    }
    private static long Measure(Action action, int iterations)
    {
        for (int i = 0; i < 10000; i++) action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
