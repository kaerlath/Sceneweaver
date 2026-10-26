using System.Collections;
using System.Numerics;
using System.Reflection;
using CrossFormat.Core;
using Dalamud.Plugin;

namespace CrossFormat.Plugin;

// Read-only compatibility adapter. No hooks, native pointers, downloads, or sync mutations.
// Resolve objects afresh on each click; never retain foreign plugin instances across frames.
internal static class ReceivedStageReader
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Field(object value, string name)
    {
        for (Type? t = value.GetType(); t != null; t = t.BaseType)
            if (t.GetField(name, Flags) is { } f) return f.GetValue(value) ?? throw Missing(name);
        throw Missing(name);
    }
    private static object Property(object value, string name) => value.GetType().GetProperty(name, Flags)?.GetValue(value) ?? throw Missing(name);
    private static InvalidOperationException Missing(string name) => new($"Received stages are unavailable: the plugin interface changed or is not ready ({name}). Refresh after PlayerSync has loaded your stages.");
    private static object Instance(IDalamudPluginInterface pi, string name)
    {
        var exposed = pi.InstalledPlugins.FirstOrDefault(p => p.InternalName == name && p.IsLoaded) ?? throw new InvalidOperationException($"Enable {name} before browsing received stages.");
        return Field(Field(exposed, "<plugin>P"), "instance");
    }
    private static (IServiceProvider Provider, Type HostedType) Host(object plugin)
    {
        var host = Field(plugin, "_host");
        var contract = host.GetType().GetInterfaces().Single(t => t.FullName == "Microsoft.Extensions.Hosting.IHost");
        var provider = (IServiceProvider)(contract.GetProperty("Services")!.GetValue(host) ?? throw Missing("Services"));
        return (provider, contract.Assembly.GetType("Microsoft.Extensions.Hosting.IHostedService") ?? throw Missing("IHostedService"));
    }
    private static object[] Hosted(IServiceProvider provider, Type type) =>
        ((IEnumerable)(provider.GetService(typeof(IEnumerable<>).MakeGenericType(type)) ?? throw Missing("Hosted services"))).Cast<object>().ToArray();

    public static List<ReceivedStageSnapshot> Read(IDalamudPluginInterface pi)
    {
        var playerHost = Host(Instance(pi, "MareSempiterne"));
        var services = Hosted(playerHost.Provider, playerHost.HostedType);
        var display = services.FirstOrDefault(s => s.GetType().FullName == "PlayerSync.PlayerData.Services.StageDisplayService");
        if (display == null)
        {
            var runtime = services.FirstOrDefault(s => s.GetType().FullName == "MareSynchronos.MarePlugin") ?? throw Missing("PlayerSync runtime");
            var scope = Field(runtime, "_runtimeServiceScope");
            var scopeInterface = scope.GetType().GetInterfaces().Single(t => t.FullName == "Microsoft.Extensions.DependencyInjection.IServiceScope");
            var provider = (IServiceProvider)scopeInterface.GetProperty("ServiceProvider")!.GetValue(scope)!;
            var serviceType = runtime.GetType().Assembly.GetType("PlayerSync.PlayerData.Services.IStageDisplayService") ?? throw Missing("Stage display service");
            display = provider.GetService(serviceType) ?? Hosted(provider, playerHost.HostedType).FirstOrDefault(s => s.GetType().FullName == "PlayerSync.PlayerData.Services.StageDisplayService") ?? throw Missing("Stage display service");
        }
        var stageHost = Host(Instance(pi, "Stagehand"));
        var ipc = Hosted(stageHost.Provider, stageHost.HostedType).SingleOrDefault(s => s.GetType().FullName == "Stagehand.Services.IpcApiService") ?? throw Missing("Stagehand stage registry");
        // ConcurrentDictionary.ToArray takes a stable structural snapshot.
        var registry = Field(ipc, "_temporaryStages");
        var entries = (IEnumerable)(registry.GetType().GetMethod("ToArray", Type.EmptyTypes)?.Invoke(registry, null) ?? throw Missing("Stage registry snapshot"));
        var definitions = entries.Cast<object>().ToDictionary(e => (string)Property(e, "Key"), e => Property(e, "Value"));
        var active = (IEnumerable)(display.GetType().GetMethod("GetActiveStages")?.Invoke(display, null) ?? throw Missing("Active stages"));
        var result = new List<ReceivedStageSnapshot>();
        long snapshotChars = 0;
        foreach (var item in active)
        {
            var stage = item;
            // Support either a values collection or a keyed collection without guessing the key.
            if (stage.GetType().IsGenericType && stage.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) stage = Property(stage, "Value");
            if ((bool)Property(stage, "IsHidden") || Property(stage, "State").ToString() != "Loaded") continue;
            string unique = (string)Property(stage, "UniqueId");
            if (!definitions.TryGetValue("temp://temp/" + unique, out var temporary)) throw Missing("PlayerSync / Stagehand stage ID match");
            var info = Property(stage, "StageFullInfo"); var customize = Property(info, "Customize");
            var definition = Property(temporary, "Definition");
            string json = (string)(definition.GetType().GetMethod("ToDefinitionString", Type.EmptyTypes)?.Invoke(definition, null) ?? throw Missing("Stage serialization"));
            snapshotChars += json.Length;
            if (snapshotChars > 64L * 1024 * 1024) throw new InvalidOperationException("Received definitions are too large to browse together. Hide some stages first.");
            result.Add(new((string)Property(info, "SID"), (string)Property(customize, "DisplayName"), (string)Property(customize, "Author"), json,
                (Vector3)Property(temporary, "Translation"), (Quaternion)Property(temporary, "Rotation"), (float)Property(temporary, "UniformScale")));
            if (result.Count > 100) throw new InvalidOperationException("Too many received stages. Hide some stages before taking a snapshot.");
        }
        return result;
    }
}
