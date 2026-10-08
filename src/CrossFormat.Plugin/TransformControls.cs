using System.Numerics;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private int transformTool; // Select, move, rotate, scale.
    private bool localAxes = true, snapMove, snapRotate, sceneDrawn, gizmoEditing, revealSelection;
    private float moveIncrement = 1, rotateIncrement = 15;
    private Guid gizmoObject;

    private void DrawTransformToolbar()
    {
        sceneDrawn = true;
        ImGui.BeginDisabled(gizmoEditing);
        ImGui.SetNextItemWidth(150);
        ImGui.Combo("Tool", ref transformTool, "Select\0Move\0Rotate\0Scale\0");
        ImGui.SameLine();
        if (ImGui.Button(localAxes ? "Axes: Local" : "Axes: World")) localAxes = !localAxes;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Local follows the selected object's rotation. World follows the game's axes.");
        if (transformTool == 1)
        {
            ImGui.Checkbox("Snap movement", ref snapMove); ImGui.SameLine(); ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("Distance", ref moveIncrement)) moveIncrement = float.IsFinite(moveIncrement) ? Math.Clamp(moveIncrement, .001f, 10000) : 1;
        }
        if (transformTool == 2)
        {
            ImGui.Checkbox("Snap rotation", ref snapRotate); ImGui.SameLine(); ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("Degrees", ref rotateIncrement)) rotateIncrement = float.IsFinite(rotateIncrement) ? Math.Clamp(rotateIncrement, .1f, 180) : 15;
        }
        if (transformTool == 3) ImGui.TextWrapped("Scale uses object axes. All group handles scale uniformly.");
        if (transformTool != 0 && !live.Enabled) ImGui.TextWrapped("Show the scene in game to use the manipulation handles.");
        ImGui.EndDisabled();
        ImGui.Separator();
    }

    private void TransformMenu(string label, Vector3 value, Action<Vector3> apply, bool uniform = false)
    {
        if (!ImGui.BeginPopupContextItem("Transform " + label)) return;
        if (ImGui.MenuItem("Copy " + label)) ImGui.SetClipboardText(TransformEditing.Copy(value));
        if (ImGui.MenuItem("Paste " + label)) Run(() =>
        {
            var pasted = TransformEditing.Paste(ImGui.GetClipboardText(), uniform);
            Edit(() => apply(pasted));
        });
        ImGui.TextDisabled("Values are relative to the parent group / stage.");
        ImGui.EndPopup();
    }

    private unsafe void DrawTransformGizmo()
    {
        var item = scene.Objects.FirstOrDefault(o => o.Id == selected);
        if (!open || !sceneDrawn || !live.Enabled || !clientState.IsLoggedIn || worldPicking || transformTool == 0
            || item == null || item.Locked || item.Kind is AssetKind.Unknown or AssetKind.Furniture
            || !SceneHierarchy.Visible(scene, item)
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
        { gizmoEditing = false; return; }
        if (gizmoObject != item.Id) gizmoEditing = false;
        var manager = CameraManager.Instance();
        if (manager == null || manager->CurrentCamera == null || manager->CurrentCamera->RenderCamera == null) { gizmoEditing = false; return; }
        var camera = manager->CurrentCamera->RenderCamera;
        if (camera->FarPlane <= camera->NearPlane || camera->NearPlane <= 0) return;
        // Camera clip correction follows Stagehand 0.5.5 OverlayService (AGPL; see THIRD_PARTY.md).
        Matrix4x4 view = camera->ViewMatrix, projection = camera->ProjectionMatrix;
        float far = camera->FarPlane, near = camera->NearPlane;
        projection.M43 = -(far / (far - near) * near);
        projection.M33 = -((far + near) / (far - near)); view.M44 = 1;

        var pose = TransformMath.ToWorld(scene, item);
        // Zero scale cannot be decomposed reliably by the manipulator.
        if (MathF.Abs(pose.Scale.X) < .000001f || MathF.Abs(pose.Scale.Y) < .000001f || MathF.Abs(pose.Scale.Z) < .000001f) return;
        var matrix = Matrix4x4.CreateScale(pose.Scale) * Matrix4x4.CreateFromQuaternion(TransformMath.Rotation(pose.Rotation)) * Matrix4x4.CreateTranslation(pose.Position);
        var viewport = ImGui.GetMainViewport();
        bool overUi = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
        ImGuizmo.BeginFrame(); ImGuizmo.SetDrawlist(ImGui.GetBackgroundDrawList());
        ImGuizmo.SetRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);
        ImGuizmo.SetOrthographic(camera->IsOrtho);
        ImGuizmo.SetID(0x53574541);
        ImGuizmo.Enable(gizmoEditing || !overUi);
        var operation = transformTool == 1 ? ImGuizmoOperation.Translate : transformTool == 2 ? ImGuizmoOperation.Rotate : ImGuizmoOperation.Scale;
        // ImGuizmo scale is local. Group axis changes are converted to uniform scale below.
        var mode = localAxes || transformTool == 3 ? ImGuizmoMode.Local : ImGuizmoMode.World;
        Vector3 snap = new(transformTool == 1 && snapMove ? moveIncrement : transformTool == 2 && snapRotate ? rotateIncrement : 0);
        bool changed = ImGuizmo.Manipulate(ref view.M11, ref projection.M11, operation, mode, ref matrix.M11, null, ref snap.X);
        bool usingGizmo = ImGuizmo.IsUsing();
        if (usingGizmo || (!overUi && ImGuizmo.IsOver())) ImGui.SetNextFrameWantCaptureMouse(true);
        if (changed && Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var position))
        {
            // Preserve components the active tool is not editing; decomposition can change equivalent rotations or scale signs.
            if (transformTool != 1) position = pose.Position;
            if (transformTool != 2) rotation = TransformMath.Rotation(pose.Rotation);
            if (transformTool != 3) scale = pose.Scale;
            if (transformTool == 3 && item.Kind == AssetKind.Group)
            {
                var delta = scale - pose.Scale;
                float component = MathF.Abs(delta.X) >= MathF.Abs(delta.Y) && MathF.Abs(delta.X) >= MathF.Abs(delta.Z) ? scale.X : MathF.Abs(delta.Y) >= MathF.Abs(delta.Z) ? scale.Y : scale.Z;
                scale = new(MathF.Max(.001f, component));
            }
            Run(() =>
            {
                // Validate on a detached object before adding an undo record or touching the project.
                var candidate = new SceneObject { ParentId = item.ParentId, Kind = item.Kind };
                TransformEditing.SetWorld(scene, candidate, position, rotation, scale);
                void Apply()
                {
                    if (transformTool == 1) item.Position = candidate.Position;
                    if (transformTool == 2) item.RotationDegrees = candidate.RotationDegrees;
                    if (transformTool == 3) item.Scale = candidate.Scale;
                }
                if (!gizmoEditing) Edit(Apply);
                else { Apply(); scene.Revision++; dirty = true; pending = null; live.MarkChanged(); }
                gizmoObject = item.Id; gizmoEditing = true;
            });
        }
        if (!usingGizmo) gizmoEditing = false;
        ImGuizmo.SetID(-1);
    }
}
