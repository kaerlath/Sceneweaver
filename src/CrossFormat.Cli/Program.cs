using CrossFormat.Core;

if (args.Length < 2)
{
    Console.WriteLine("Sceneweaver converter\nUsage: <input.json> <output-directory> [--write] [--allow-omissions] [--overwrite]\nWithout --write, prints the conversion report only. Always writes a canonical project alongside both exports.");
    return 2;
}
try
{
    var scene = SceneCodec.Import(ProjectFiles.Read(args[0]));
    var directory = Path.GetFullPath(args[1]);
    var plan = ProjectFiles.PlanDualSave(scene, Path.Combine(directory, "scene.cross.json"), Path.Combine(directory, "scene.intoner.json"), Path.Combine(directory, "scene.stagehand.json"));
    Console.WriteLine($"{scene.Name}: {scene.Objects.Count} objects");
    foreach (var issue in plan.Issues) Console.WriteLine($"{(issue.Omitted ? "OMITTED" : "NOTE")} {issue.ObjectName}: {issue.Message}");
    foreach (var target in plan.Targets) Console.WriteLine($"{(File.Exists(target.Path) ? "Replace" : "Create")}: {target.Path}");
    if (!args.Contains("--write")) return 0;
    if (plan.Issues.Any(i => i.Omitted) && !args.Contains("--allow-omissions")) throw new InvalidOperationException("Review the omissions and pass --allow-omissions to write supported objects.");
    if (plan.Targets.Any(t => File.Exists(t.Path)) && !args.Contains("--overwrite")) throw new InvalidOperationException("An output already exists. Use a new directory or pass --overwrite.");
    ProjectFiles.Commit(plan);
    Console.WriteLine("Saved canonical project and both exports.");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
