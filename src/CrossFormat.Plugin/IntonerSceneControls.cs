using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
namespace CrossFormat.Plugin;
public sealed partial class Plugin
{
    private List<IntonerSceneRow> intonerScenes=[];
    private string intonerSceneKey="", intonerSceneStatus="Refresh to browse Intoner's saved layouts and temporary scenes.", intonerSceneFilter="";
    private Task<List<IntonerSceneRow>>? intonerSceneRefresh;
    private Task<SceneProject>? intonerSceneImport;
    private void DrawIntonerScenes()
    {
        ImGui.TextWrapped("Intoner scenes");
        ImGui.TextWrapped("Import saved layouts or temporary scenes as independent projects. Mod files and memory resources are copied when available. Temporary sources can come from any plugin; they are not necessarily PlayerSync scenes. Native room furniture is excluded.");
        if(intonerSceneRefresh?.IsCompleted==true)
        {
            var task=intonerSceneRefresh; intonerSceneRefresh=null;
            try { intonerScenes=task.GetAwaiter().GetResult(); intonerSceneStatus=$"{intonerScenes.Count} layouts and sources found."; }
            catch(Exception e) { intonerScenes.Clear(); intonerSceneStatus=e.GetBaseException().Message; }
        }
        if(intonerSceneImport?.IsCompleted==true)
        {
            var task=intonerSceneImport; intonerSceneImport=null;
            Run(()=> { var imported=task.GetAwaiter().GetResult(); GuardReplace(()=> {
                ReplaceScene(imported); inputPath=projectPath=intonerPath=stagehandPath=""; knownHashes.Clear(); dirty=true;
                status="Intoner scene imported as an independent project. Choose new save destinations.";
            }); });
        }
        bool busy=intonerSceneImport!=null || intonerSceneRefresh!=null;
        ImGui.BeginDisabled(busy || !clientState.IsLoggedIn);
        if(ImGui.Button("Refresh Intoner scenes")) intonerSceneRefresh=framework.RunOnFrameworkThread(()=>IntonerSceneReader.List(pi));
        ImGui.EndDisabled();
        ImGui.TextWrapped(busy?"Reading Intoner scene data and resources...":intonerSceneStatus);
        ImGui.InputTextWithHint("##intoner-scene-filter","Filter scene names",ref intonerSceneFilter,256);
        ImGui.BeginChild("intoner-scenes-list",new(0,220),true);
        foreach(var row in intonerScenes.Where(r=>r.Name.Contains(intonerSceneFilter,StringComparison.OrdinalIgnoreCase)))
            if(ImGui.Selectable($"{(row.Temporary?"Temporary":"Saved")} | {row.Name}##{row.Key}",intonerSceneKey==row.Key)) intonerSceneKey=row.Key;
        ImGui.EndChild();
        var selectedRow=intonerScenes.FirstOrDefault(r=>r.Key==intonerSceneKey);
        if(selectedRow==null) return;
        ImGui.Separator(); ImGui.TextWrapped("Portable mod resources are retained in the Sceneweaver project and Stagehand export. Intoner layout files alone do not embed collections. Unsupported object types remain in the project and are reported during conversion.");
        ImGui.BeginDisabled(busy || !clientState.IsLoggedIn);
        if(ImGui.Button("Import Intoner scene as new project")) intonerSceneImport=ImportIntonerSceneAsync(selectedRow);
        ImGui.EndDisabled();
    }
    private async Task<SceneProject> ImportIntonerSceneAsync(IntonerSceneRow row)
    {
        var snapshot=await framework.RunOnTick(()=>IntonerSceneReader.Capture(pi,row));
        return await Task.Run(()=>IntonerScenes.Import(snapshot));
    }
}
