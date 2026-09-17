using System.Security.Cryptography;
using System.Text;

namespace CrossFormat.Core;

public sealed record SaveTarget(string Path, string Content, string? ExpectedHash = null);
public sealed record SavePlan(IReadOnlyList<SaveTarget> Targets, IReadOnlyList<ConversionIssue> Issues);

public static class ProjectFiles
{
    public const long MaxInputBytes = 256 * 1024 * 1024;
    public static string Read(string path)
    {
        if (new FileInfo(path).Length > MaxInputBytes) throw new InvalidDataException("File exceeds the 256 MB project import limit.");
        return File.ReadAllText(path);
    }
    public static string Hash(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "missing";

    public static SavePlan PlanDualSave(SceneProject s, string canonicalPath, string intonerPath, string stagehandPath)
    {
        var i = SceneCodec.Export(s, SceneFormat.Intoner);
        var h = SceneCodec.Export(s, SceneFormat.Stagehand);
        var targets = new[] { new SaveTarget(canonicalPath, ProjectJson.Save(s)), new SaveTarget(intonerPath, i.Json), new SaveTarget(stagehandPath, h.Json) };
        var paths = targets.Select(t => Path.GetFullPath(t.Path)).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length) throw new InvalidDataException("Choose three distinct save paths.");
        return new(targets.Select(t => t with { ExpectedHash = Hash(t.Path) }).ToArray(), i.Issues.Concat(h.Issues).ToArray());
    }

    // Files are staged before any replacement. Rollback covers ordinary I/O failures;
    // no filesystem can promise a single atomic commit across arbitrary directories.
    public static void Commit(SavePlan plan, Action<int>? beforeReplace = null)
    {
        var targets = plan.Targets.Select(t => t with { Path = Path.GetFullPath(t.Path) }).ToArray();
        if (targets.Any(t => Encoding.UTF8.GetByteCount(t.Content) > MaxInputBytes)) throw new InvalidDataException("Output exceeds the 256 MB project limit. Split large mod libraries between projects.");
        if (targets.Select(t => t.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length) throw new InvalidDataException("Duplicate destination path.");
        var token = Guid.NewGuid().ToString("N");
        var staged = new List<(SaveTarget Target, string Temp, string Backup, bool Existed)>();
        var committed = new List<(SaveTarget Target, string Backup, bool Existed)>();
        try
        {
            foreach (var t in targets)
            {
                if (t.ExpectedHash is not null && Hash(t.Path) != t.ExpectedHash) throw new IOException($"File changed since review: {t.Path}");
                Directory.CreateDirectory(Path.GetDirectoryName(t.Path)!);
                var temp = t.Path + "." + token + ".tmp";
                var backup = t.Path + "." + token + ".bak";
                staged.Add((t, temp, backup, File.Exists(t.Path)));
                using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var bytes = Encoding.UTF8.GetBytes(t.Content);
                stream.Write(bytes); stream.Flush(true);
            }
            for (int n = 0; n < staged.Count; n++)
            {
                var entry = staged[n];
                beforeReplace?.Invoke(n);
                if (entry.Target.ExpectedHash is not null && Hash(entry.Target.Path) != entry.Target.ExpectedHash) throw new IOException($"File changed during save: {entry.Target.Path}");
                if (entry.Existed) File.Replace(entry.Temp, entry.Target.Path, entry.Backup);
                else File.Move(entry.Temp, entry.Target.Path);
                committed.Add((entry.Target, entry.Backup, entry.Existed));
            }
        }
        catch (Exception original)
        {
            List<Exception> rollbackErrors = [];
            foreach (var entry in committed.AsEnumerable().Reverse())
            {
                try
                {
                    if (entry.Existed) File.Move(entry.Backup, entry.Target.Path, true);
                    else File.Delete(entry.Target.Path);
                }
                catch (Exception e) { rollbackErrors.Add(new IOException($"Recovery backup: {entry.Backup}", e)); }
            }
            if (rollbackErrors.Count > 0) throw new AggregateException("Save failed; some backups need manual recovery.", new[] { original }.Concat(rollbackErrors));
            throw;
        }
        finally
        {
            foreach (var entry in staged) { try { File.Delete(entry.Temp); } catch (IOException) { /* Original error has priority. */ } }
        }
        // Backups intentionally survive successful saves for recovery after external edits.
    }
}
