using System.Numerics;
using CrossFormat.Core;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using SceneNode = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;

namespace CrossFormat.Plugin;

/// <summary>Read only on the framework thread. Never retain native pointers in snapshots.</summary>
internal static unsafe class WorldSceneReader
{
    public static List<WorldPick> Read(Vector3 origin)
    {
        var result = new List<WorldPick>(); var seen = new HashSet<nint>(); int visits = 0;
        var world = World.Instance(); if (world == null) return result;
        void Visit(SceneNode* node, int depth)
        {
            if (++visits >= 50000 || node == null || depth > 128 || !seen.Add((nint)node)) return;
            var type = node->GetObjectType();
            if ((type == ObjectType.BgObject || type == ObjectType.VfxObject) && Vector3.DistanceSquared(origin, node->Position) <= 200 * 200)
            {
                string path = ""; Vector4 color = Vector4.One; float opacity = 1;
                if (type == ObjectType.BgObject)
                {
                    var model = (BgObject*)node;
                    if (model->ModelResourceHandle != null && model->ModelResourceHandle->LoadState >= 7)
                    {
                        path = model->ModelResourceHandle->FileName.ToString();
                        opacity = 1 - model->GetTransparency();
                        if (model->StainBuffer != null)
                        {
                            var c = model->StainBuffer->SrgbByteColor;
                            color = new Vector4(c.R, c.G, c.B, c.A) / 255f; color *= color;
                        }
                    }
                }
                else
                {
                    var vfx = (VfxObject*)node;
                    // Same resource chain used by Stagehand 0.5.2 LiveVfxObject/ViewportPickerService.
                    var instance = (byte*)vfx->VfxResourceInstance;
                    if (instance != null)
                    {
                        var resource = *(byte**)(instance + 0x08);
                        if (resource != null)
                        {
                            var handle = *(ResourceHandle**)(resource + 0x18);
                            if (handle != null) path = handle->FileName.ToString();
                        }
                    }
                    color = vfx->Color;
                }
                Vector3 position = node->Position, scale = node->Scale; Quaternion rotation = node->Rotation;
                if (path.Length > 0 && path.Length <= 4096 && Finite(position) && Finite(scale) && float.IsFinite(opacity) && float.IsFinite(color.LengthSquared()) && float.IsFinite(rotation.LengthSquared()) && rotation.LengthSquared() > .0001f)
                    result.Add(new($"{path}|{position.X:R},{position.Y:R},{position.Z:R}", path,
                        type == ObjectType.BgObject ? AssetKind.BgObject : AssetKind.Vfx, position, Quaternion.Normalize(rotation), scale, color, opacity));
            }
            foreach (var child in node->ChildObjects)
            {
                if (visits >= 50000) break;
                Visit(child, depth + 1);
            }
        }
        Visit((SceneNode*)world, 0);
        return result.OrderBy(p => Vector3.DistanceSquared(origin, p.Position)).Take(2000).ToList();
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
