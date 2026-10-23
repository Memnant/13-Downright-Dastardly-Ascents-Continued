using dda;
using Mono.Cecil;
using System.Diagnostics;
using System.Text.Json;

try
{
    Run(args);
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("FAILED_COMPATIBILITY_GUARD: " + error);
    return 1;
}

static void Run(string[] args)
{
    string root = Path.GetFullPath(args[0]);
    string gameRoot = Path.GetFullPath(args[1]);
    string managed = Path.Combine(gameRoot, "PEAK_Data", "Managed");
    string core = Path.Combine(gameRoot, "BepInEx", "core");
    string plugin = Path.Combine(root, "bin", "Release", "netstandard2.1", "13dda.dll");
    string fixture = Path.Combine(root, "artifacts", "compatibility-fixtures-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(fixture);
    var cases = new List<string>();
    var watch = Stopwatch.StartNew();
    var baseline = RuntimeCompatibility.Check("3.0.0", plugin, managed, core);
    watch.Stop();
    Require(!baseline.UnverifiedBuild && baseline.PatchDeclarations == 103 && baseline.CheckedMembers > 100, "verified baseline passes full preflight");
    Require(!RuntimeCompatibility.Check("3.0.0--logFile", plugin, managed, core).UnverifiedBuild, "audited packaging suffix is recognized");
    Require(RuntimeCompatibility.Check("3.0.1", plugin, managed, core).UnverifiedBuild, "future numeric patch is allowed but unverified");
    ExpectFailure(() => RuntimeCompatibility.Check("3.1.0", "missing", "missing", "missing"), "allows PEAK 3.0", "other series stopped before inspecting files");
    ExpectFailure(() => RuntimeCompatibility.Check("3.00.0", "missing", "missing", "missing"), "allows PEAK 3.0", "similar prefix is rejected");
    Mutation("missing-weather-target", module => {
        module.GetType("WindChillZone").Methods.Single(m => m.Name == "HandleTime").Name = "ChangedHandleTime";
    }, "HandleTime");
    Mutation("changed-status-argument", module => {
        module.GetType("CharacterAfflictions").Methods.Single(m => m.Name == "AddStatus").Parameters[0].Name = "changed_for_test";
    }, "Changed patch argument/field");
    Mutation("missing-weather-field", module => {
        module.GetType("WindChillZone").Fields.Single(f => f.Name == "windForce").Name = "ChangedWindForce";
    }, "windForce");
    Mutation("native-save-format-5", module => {
        module.GetType("Peak.Quicksave/SaveData").Fields.Single(f => f.Name == "k_CurrentVersion").Constant = 5;
    }, "Native save format changed");
    Mutation("changed-checkpoint-field", module => {
        module.GetType("Peak.Quicksave/RunProgress").Fields.Single(f => f.Name == "runTimer").FieldType = module.TypeSystem.Double;
    }, "Native checkpoint field changed");
    Mutation("ambiguous-weather-target", module => {
        var method = new MethodDefinition("HandleTime", MethodAttributes.Private, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("extra", ParameterAttributes.None, module.TypeSystem.Boolean));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        module.GetType("WindChillZone").Methods.Add(method);
    }, "HandleTime");
    string newBuild = CopyMutation("changed-build-id", module => module.Mvid = Guid.NewGuid());
    Require(RuntimeCompatibility.Check("3.0.0", plugin, managed, core, newBuild).UnverifiedBuild, "same version but new assembly build is unverified");

    var report = new { Status = "PASSED_COMPATIBILITY_GUARD", Assertions = cases.Count, baseline.PatchDeclarations,
        baseline.CheckedMembers, BaselineMilliseconds = watch.Elapsed.TotalMilliseconds, Cases = cases,
        SyntheticFutureVersions = true, RealFutureBuildTested = false, Fixture = fixture };
    string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(root, "artifacts", "compatibility-guard-tests.json"), json);
    Console.WriteLine(json);

    void Require(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        cases.Add(description);
    }
    void ExpectFailure(Action action, string reason, string description)
    {
        try { action(); }
        catch (NotSupportedException error) when (error.Message.Contains(reason, StringComparison.Ordinal)) { cases.Add(description); return; }
        throw new Exception("Expected compatibility rejection: " + description);
    }
    string CopyMutation(string name, Action<ModuleDefinition> change)
    {
        string path = Path.Combine(fixture, name + ".dll");
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        resolver.AddSearchDirectory(core);
        using var copy = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "Assembly-CSharp.dll"),
            new ReaderParameters { AssemblyResolver = resolver });
        change(copy.MainModule);
        copy.Write(path);
        return path;
    }
    void Mutation(string name, Action<ModuleDefinition> change, string reason)
    {
        string path = CopyMutation(name, change);
        ExpectFailure(() => RuntimeCompatibility.Check("3.0.1", plugin, managed, core, path), reason, name);
    }
}
