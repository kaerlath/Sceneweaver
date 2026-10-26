using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
namespace CrossFormat.Plugin;
public sealed partial class Plugin
{
    private bool showReplacePrompt;
    private void NewScene()
    {
        ReplaceScene(new());
        inputPath=projectPath=intonerPath=stagehandPath=""; knownHashes.Clear();
        status="New scene created. Add objects or insert a saved scene, then choose new save destinations.";
    }
    private void DrawReplacePrompt()
    {
        if(showReplacePrompt) { ImGui.OpenPopup("Replace unsaved scene?"); showReplacePrompt=false; }
        if(!ImGui.BeginPopupModal("Replace unsaved scene?",ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted("Your current scene has unsaved changes.");
        ImGui.TextUnformatted("Keep editing to save them first, or discard them to continue.");
        if(ImGui.Button("Discard changes and continue"))
        {
            var action=pendingDestructive; pendingDestructive=null;
            ImGui.CloseCurrentPopup(); if(action!=null) Run(action);
        }
        ImGui.SameLine();
        if(ImGui.Button("Keep editing")) { pendingDestructive=null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
    private void InsertSceneFile(string path)
    {
        var imported=SceneCodec.Import(ProjectFiles.Read(path));
        var position=objectTable.LocalPlayer is { } player?LiveScene.LocalPosition(scene,player.Position):System.Numerics.Vector3.Zero;
        var insertion=SceneMerge.Insert(scene,imported,position);
        Edit(()=> { scene=insertion.Project; selected=insertion.GroupId; focusScene=true; });
        status=$"Inserted '{imported.Name}' as one group with {insertion.ObjectCount} entries. Select the group to move, rotate or resize the entire assembly. Undo removes this insertion.";
    }
}
