using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static class Game26SaveRegression
{
    internal static int Run()
    {
        // Exercise the native version predicate only. TryLoadSave can delete an
        // incompatible real save, so it must never be used by this fixture.
        var type = AccessTools.TypeByName("Peak.Quicksave");
        var parse = AccessTools.Method(AccessTools.TypeByName("UnityEngine.JsonUtility"), "FromJson", new[] { typeof(string), typeof(Type) });
        var loaded = AccessTools.Field(type, "_loadedData");
        var check = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name.StartsWith("<TryLoadSave>g__CheckVersion|"));
        var dataType = loaded.FieldType;
        object previous = loaded.GetValue(null);
        int assertions = 0;
        void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("2.6 native save: " + message);
            assertions++;
        }
        try
        {
            foreach (int version in new[] { 0, 2, 3, 4, 5 })
            {
                object data = parse.Invoke(null, new object[] { "{\"version\":" + version + "}", dataType });
                loaded.SetValue(null, data);
                Assert((bool)check.Invoke(null, null) == (version == 4), "only native format 4 is accepted");
            }
            string id = Guid.NewGuid().ToString("N");
            object checkpoint = parse.Invoke(null, new object[] { "{\"version\":4,\"run\":{\"runId\":\"" + id +
                "\",\"biomeReached\":4,\"runTimer\":678.5}}", dataType });
            object run = AccessTools.Field(dataType, "run").GetValue(checkpoint);
            Assert((string)AccessTools.Field(run.GetType(), "runId").GetValue(run) == id, "native run ID field retained");
            Assert(Convert.ToInt32(AccessTools.Field(run.GetType(), "biomeReached").GetValue(run)) == 4, "native checkpoint field retained");
            Assert(Math.Abs(Convert.ToDouble(AccessTools.Field(run.GetType(), "runTimer").GetValue(run)) - 678.5) < 0.001,
                "native checkpoint time retained");
        }
        finally { loaded.SetValue(null, previous); }
        return assertions;
    }
}
