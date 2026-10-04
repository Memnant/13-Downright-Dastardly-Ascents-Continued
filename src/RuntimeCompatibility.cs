using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace dda;

internal sealed class CompatibilityReport
{
    internal bool UnverifiedBuild;
    internal int PatchDeclarations;
    internal int CheckedMembers;
    internal string GameMvid;
}

internal static class RuntimeCompatibility
{
    private const string VerifiedMvid = "be46e6d8-8207-4c79-aa1e-b196eb5e0358";

    // Metadata only, once at startup, before PatchAll. No game code executes,
    // and all assemblies are opened read-only and disposed before returning.
    internal static CompatibilityReport Check(string version, string pluginPath, string managedDirectory, string coreDirectory, string gameAssemblyOverride = null)
    {
        if (!GameVersionPolicy.Allows(version))
            throw new NotSupportedException("This build allows PEAK 2.6 letter patches; found " + version + ". Other series need a separate compatibility review.");
        using var resolver = new GameResolver();
        resolver.AddSearchDirectory(managedDirectory);
        resolver.AddSearchDirectory(coreDirectory);
        var reader = new ReaderParameters { AssemblyResolver = resolver, InMemory = true };
        using var game = AssemblyDefinition.ReadAssembly(gameAssemblyOverride ?? Path.Combine(managedDirectory, "Assembly-CSharp.dll"), reader);
        resolver.Game = game;
        using var mod = AssemblyDefinition.ReadAssembly(pluginPath, reader);
        var report = new CompatibilityReport {
            GameMvid = game.MainModule.Mvid.ToString(),
            UnverifiedBuild = version != GameVersionPolicy.VerifiedGame || game.MainModule.Mvid.ToString() != VerifiedMvid
        };
        var errors = new List<string>();
        foreach (var reference in mod.MainModule.GetMemberReferences())
        {
            string scope = reference.DeclaringType.Scope.Name;
            if (!(scope == "Assembly-CSharp" || scope.StartsWith("Unity", StringComparison.Ordinal) ||
                scope.StartsWith("Photon", StringComparison.Ordinal) || scope.StartsWith("Zorro", StringComparison.Ordinal) ||
                scope == "BepInEx" || scope == "0Harmony" || scope == "DOTween" || scope == "pworld")) continue;
            report.CheckedMembers++;
            try
            {
                bool resolved = reference is MethodReference method ? method.Resolve() != null :
                    reference is FieldReference field && field.Resolve() != null;
                if (!resolved) errors.Add("Missing member: " + reference.FullName);
            }
            catch (Exception error) { errors.Add("Cannot resolve " + reference.FullName + ": " + error.Message); }
        }
        foreach (var patchType in mod.MainModule.GetTypes())
        foreach (var attribute in patchType.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
        {
            report.PatchDeclarations++;
            try
            {
                if (attribute.ConstructorArguments.Count != 2 ||
                    !(attribute.ConstructorArguments[0].Value is TypeReference targetReference) ||
                    !(attribute.ConstructorArguments[1].Value is string name))
                    throw new NotSupportedException("Unknown Harmony target declaration");
                var targetType = targetReference.Resolve();
                var candidates = targetType.Methods.Where(m => m.Name == name).ToArray();
                if (candidates.Length != 1) throw new MissingMethodException(targetType.FullName, name);
                var target = candidates[0];
                foreach (var patch in patchType.Methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Finalizer"))
                foreach (var argument in patch.Parameters)
                {
                    string key = argument.Name;
                    if (key == "__state" || key == "__exception" || key == "__originalMethod" || key == "__runOriginal" || key == "__args") continue;
                    if (key == "__instance")
                    {
                        if (target.IsStatic) throw new InvalidDataException("Static target no longer has an instance");
                        continue;
                    }
                    TypeReference expected;
                    if (key == "__result") expected = target.ReturnType;
                    else if (key.StartsWith("___", StringComparison.Ordinal)) expected = FindField(targetType, key.Substring(3))?.FieldType;
                    else expected = target.Parameters.FirstOrDefault(p => p.Name == key)?.ParameterType;
                    if (expected == null || Bare(expected) != Bare(argument.ParameterType))
                        throw new InvalidDataException("Changed patch argument/field: " + key);
                }
            }
            catch (Exception error) { errors.Add("Patch " + patchType.FullName + ": " + error.Message); }
        }
        var saveType = game.MainModule.GetType("Peak.Quicksave/SaveData");
        var format = saveType?.Fields.FirstOrDefault(f => f.Name == "k_CurrentVersion");
        if (format == null || !format.HasConstant || !Equals(format.Constant, GameVersionPolicy.NativeSaveVersion))
            errors.Add("Native save format changed; expected format 4. Existing saves will not be converted.");
        CheckField(saveType, "version", "System.Int32", errors);
        CheckField(saveType, "run", "Peak.Quicksave/RunProgress", errors);
        var runType = game.MainModule.GetType("Peak.Quicksave/RunProgress");
        CheckField(runType, "runId", "System.String", errors);
        CheckField(runType, "biomeReached", "Segment", errors);
        CheckField(runType, "runTimer", "System.Single", errors);
        if (report.PatchDeclarations == 0) errors.Add("No Harmony targets found in plugin metadata");
        if (errors.Count != 0)
            throw new NotSupportedException("Continued compatibility check failed (" + errors.Count + "): " + string.Join(" | ", errors.Take(8)));
        return report;
    }

    // Unwrap only ref/out; GetElementType() also strips arrays, changing ref byte[] into byte.
    private static string Bare(TypeReference type) => type is ByReferenceType byReference ? byReference.ElementType.FullName : type.FullName;

    private static FieldDefinition FindField(TypeDefinition type, string name)
    {
        while (type != null)
        {
            var field = type.Fields.FirstOrDefault(f => f.Name == name);
            if (field != null) return field;
            type = type.BaseType?.Resolve();
        }
        return null;
    }

    private static void CheckField(TypeDefinition type, string name, string expectedType, List<string> errors)
    {
        if (type?.Fields.FirstOrDefault(f => f.Name == name)?.FieldType.FullName != expectedType)
            errors.Add("Native checkpoint field changed: " + type?.FullName + "." + name);
    }

    private sealed class GameResolver : DefaultAssemblyResolver
    {
        internal AssemblyDefinition Game;
        public override AssemblyDefinition Resolve(AssemblyNameReference name) => name.Name == "Assembly-CSharp" ? Game : base.Resolve(name);
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) =>
            name.Name == "Assembly-CSharp" ? Game : base.Resolve(name, parameters);
    }
}
