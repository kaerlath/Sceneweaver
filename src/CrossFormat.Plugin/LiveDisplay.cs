using System.Diagnostics;
using System.Numerics;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrossFormat.Plugin;

internal sealed class StagehandLiveBackend : ILiveStageBackend
{
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly ICallGateSubscriber<string, string, Vector3, Quaternion, float, string, bool> upsert;
    private readonly ICallGateSubscriber<string, bool, bool> show;
    private readonly ICallGateSubscriber<string, bool> destroy;
    private readonly ICallGateSubscriber<object> location;
    public StagehandLiveBackend(IDalamudPluginInterface pi, IClientState client, IObjectTable objects)
    {
        this.client = client; this.objects = objects;
        // Primitive IPC signatures verified against Stagehand.Api 1.2 and its installed generated consumer.
        upsert = pi.GetIpcSubscriber<string, string, Vector3, Quaternion, float, string, bool>("Stagehand.TryCreateOrUpdateTemporaryStageWithTransform");
        show = pi.GetIpcSubscriber<string, bool, bool>("Stagehand.TrySetTemporaryStageVisible");
        destroy = pi.GetIpcSubscriber<string, bool>("Stagehand.TryDestroyTemporaryStage");
        location = pi.GetIpcSubscriber<object>("Stagehand.GetLocation");
    }
    public bool Available => upsert.HasFunction && show.HasFunction && destroy.HasFunction && location.HasFunction;
    // Dalamud converts this return value to an object/JSON snapshot containing world, territory, ward, division, house and room.
    public string? Location => client.IsLoggedIn && objects.LocalPlayer != null ? location.InvokeFunc()?.ToString() : null;
    public bool Upsert(string id, LiveSceneSnapshot scene) => upsert.InvokeFunc(scene.Definition, id, scene.Translation, scene.Rotation, scene.Scale, "Sceneweaver live editor");
    public bool Show(string id) => show.InvokeFunc(id, true);
    public bool Destroy(string id) => destroy.InvokeFunc(id);
}

public sealed partial class Plugin
{
    private readonly StagehandLiveBackend liveBackend;
    private readonly LiveSceneSession live;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly Stopwatch liveClock = Stopwatch.StartNew();

    private void TickLive(IFramework _) => live.Tick(() => LiveScene.Build(scene), liveClock.Elapsed);
    private void OnTerritoryChanged(uint _) => live.Stop("Live display stopped after changing areas.");
    private Vector3 NewObjectPosition() => live.Enabled && objectTable.LocalPlayer is { } player ? LiveScene.LocalPosition(scene, player.Position) : Vector3.Zero;
    private Vector3 PlayerPosition() => objectTable.LocalPlayer?.Position ?? throw new InvalidOperationException("Log into the game to place an object near your character.");
    private void RequireLiveBackend()
    {
        if (!liveBackend.Available) throw new InvalidOperationException("Stagehand must be installed and enabled for Sceneweaver's in-game display.");
    }
    private void ShowHere()
    {
        RequireLiveBackend();
        var position = PlayerPosition();
        var anchor = scene.Objects.FirstOrDefault(o => o.Id == selected) ?? scene.Objects.FirstOrDefault(o => o.Visible);
        Edit(() => scene.StageTranslation = LiveScene.TranslationAt(scene, anchor, position));
        live.Start(() => LiveScene.Build(scene), liveClock.Elapsed);
    }
    private void AddSceneAsset(SceneObject asset, bool placeInGame)
    {
        Vector3? localPosition = null;
        if (placeInGame)
        {
            if (asset.Kind is AssetKind.Furniture or AssetKind.Unknown) throw new InvalidOperationException("This object type cannot be displayed through Stagehand. It can still be edited and saved in Sceneweaver.");
            RequireLiveBackend(); localPosition = LiveScene.LocalPosition(scene, PlayerPosition());
        }
        Edit(() =>
        {
            var copy = System.Text.Json.JsonSerializer.Deserialize<SceneObject>(System.Text.Json.JsonSerializer.Serialize(asset, ProjectJson.Options), ProjectJson.Options)!;
            copy.Id = Guid.NewGuid(); copy.StagehandId = ""; copy.Position = localPosition ?? Vector3.Zero;
            scene.Objects.Add(copy); selected = copy.Id;
        });
        if (placeInGame && !live.Enabled) live.Start(() => LiveScene.Build(scene), liveClock.Elapsed);
        status = placeInGame ? "Added at your character's position. Adjust its transform in Scene." : "Added to the editor scene.";
    }
    private void DrawLiveControls()
    {
        if (!live.Enabled)
        {
            if (ImGui.Button("Show scene here")) Run(ShowHere);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Moves the scene's saved stage placement so the selected object's origin is at your character. Live display uses Stagehand.");
            ImGui.SameLine(); if (ImGui.Button("Show saved placement")) Run(() => { RequireLiveBackend(); live.Start(() => LiveScene.Build(scene), liveClock.Elapsed); });
        }
        else if (ImGui.Button("Hide in game")) live.Stop();
        ImGui.SameLine(); ImGui.TextWrapped(live.Status);
        if (live.CleanupPending && ImGui.Button("Retry hiding scene")) live.Stop();
        if (live.Notes.Length > 0 && ImGui.TreeNode("Live display details"))
        {
            foreach (var note in live.Notes) ImGui.TextWrapped(note);
            ImGui.TreePop();
        }
        ImGui.Separator();
    }
}
