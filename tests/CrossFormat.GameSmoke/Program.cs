using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using CrossFormat.Assets;
using Lumina;
using Lumina.Data.Files;

if (args.Length != 3) { Console.WriteLine("Usage: <stage-or-layout.json> <game/sqpack directory> <output directory>"); return 2; }
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var scene = SceneCodec.Import(ProjectFiles.Read(args[0]));
Directory.CreateDirectory(args[2]);
var plan = ProjectFiles.PlanDualSave(scene, Path.Combine(args[2], "scene.cross.json"), Path.Combine(args[2], "scene.intoner.json"), Path.Combine(args[2], "scene.stagehand.json"));
ProjectFiles.Commit(plan);
var roundTrip = SceneCodec.Import(ProjectFiles.Read(plan.Targets[2].Path));
if (roundTrip.Objects.Count != scene.Objects.Count) throw new Exception("Unexpected same-format omissions.");
for (int i = 0; i < scene.Objects.Count; i++)
{
    var a = scene.Objects[i]; var b = roundTrip.Objects[i];
    if (a.Position != b.Position || a.RotationDegrees != b.RotationDegrees || a.Scale != b.Scale || a.AssetPath != b.AssetPath || a.Color != b.Color) throw new Exception("Real scene round-trip mismatch.");
}
Console.WriteLine($"Real scene: {scene.Objects.Count} objects. Same-format transform, path and color round-trip passed. {plan.Issues.Count(i=>i.Omitted)} omissions across exports.");
using var game = new GameData(args[1], new LuminaOptions());
ColorPreviewSmoke.Run(game, args[2]);
ModPreviewSmoke.Run(game, args[2]);
var asset = scene.Objects.First(o => o.AssetPath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase));
var file = game.GetFile<MdlFile>(asset.AssetPath) ?? throw new Exception("Real game model was not found.");
var mesh = ModelGeometry.Decode(file);
if (mesh.Error != null || mesh.Indices.Length == 0) throw new Exception(mesh.Error ?? "Empty geometry.");
if (mesh.Vertices.Any(v => !float.IsFinite(v.X) || v.Length() > 1.0001f)) throw new Exception("Model auto-fit escaped its unit bounds.");
if (mesh.Size == Vector3.Zero) throw new Exception("Original model dimensions were lost.");
Console.WriteLine($"Auto-fit passed. Original dimensions: {mesh.Size}.");
var rotation = Matrix4x4.CreateRotationY(.65f) * Matrix4x4.CreateRotationX(-.35f);
var vertices = mesh.Vertices.Select(v => Vector3.Transform(v, rotation)).ToArray();
var triangles = Enumerable.Range(0, mesh.Indices.Length / 3).OrderBy(n => (vertices[mesh.Indices[n*3]].Z + vertices[mesh.Indices[n*3+1]].Z + vertices[mesh.Indices[n*3+2]].Z)/3);
var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"400\" viewBox=\"0 0 400 400\"><rect width=\"400\" height=\"400\" fill=\"#202024\"/>");
using var bitmap = new System.Drawing.Bitmap(400,400);
using var graphics = System.Drawing.Graphics.FromImage(bitmap);
graphics.Clear(System.Drawing.Color.FromArgb(32,32,36));
graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
string Point(Vector3 v) => $"{200+v.X*172:0.###},{200-v.Y*172:0.###}";
foreach (var n in triangles)
{
    var a = vertices[mesh.Indices[n*3]]; var b = vertices[mesh.Indices[n*3+1]]; var c = vertices[mesh.Indices[n*3+2]];
    var normal = Vector3.Cross(b-a,c-a); if (normal.LengthSquared()<1e-12f) continue;
    float shade = .3f+.65f*MathF.Abs(Vector3.Dot(Vector3.Normalize(normal),Vector3.Normalize(new Vector3(-1,2,-3))));
    svg.Append($"<polygon points=\"{Point(a)} {Point(b)} {Point(c)}\" fill=\"rgb({(int)(shade*.8f*255)},{(int)(shade*.9f*255)},{(int)(shade*255)})\"/>");
    using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((int)(shade*.8f*255),(int)(shade*.9f*255),(int)(shade*255)));
    System.Drawing.PointF P(Vector3 v) => new(200+v.X*172,200-v.Y*172);
    graphics.FillPolygon(brush,[P(a),P(b),P(c)]);
}
svg.Append("</svg>"); File.WriteAllText(Path.Combine(args[2],"model-preview.svg"),svg.ToString());
bitmap.Save(Path.Combine(args[2],"model-preview.png"),System.Drawing.Imaging.ImageFormat.Png);
Console.WriteLine($"Real model decoded: {mesh.Vertices.Length} vertices, {mesh.Indices.Length/3} preview triangles. SVG written.");
return 0;
