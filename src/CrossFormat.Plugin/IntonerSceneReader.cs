using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Dalamud.Plugin;
namespace CrossFormat.Plugin;

internal sealed record IntonerSceneRow(string Key,string Name,bool Temporary,Guid? LayoutId);
internal static class IntonerSceneReader
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static JsonNode Json(object value)=>JsonSerializer.SerializeToNode(value,value.GetType(),ProjectJson.Options)!;
    private static object Field(object value,string name)=>value.GetType().GetField(name,Flags)?.GetValue(value) ?? throw new InvalidOperationException("Intoner collection adapter unavailable: "+name);
    private static object Query(IDalamudPluginInterface pi,string endpoint)=>pi.GetIpcSubscriber<object>("Intoner.Api."+endpoint).InvokeFunc();
    public static List<IntonerSceneRow> List(IDalamudPluginInterface pi)
    {
        var loaded=Json(Query(pi,"Scene.GetLoadedLayouts")).AsArray();
        var saved=Json(Query(pi,"Layouts.GetAll"))["Layouts"]!.AsArray();
        return saved.Select(l=>new IntonerSceneRow(l!["Id"]!.GetValue<string>(),l["Name"]!.GetValue<string>(),false,l["Id"]!.GetValue<Guid>()))
            .Concat(loaded.Where(l=>l!["Type"]!.ToString()=="Temporary" || l["Type"]!.ToString()=="2")
            .Select(l=>new IntonerSceneRow(l!["SourceKey"]!.GetValue<string>(),l["Name"]!.GetValue<string>(),true,null))).ToList();
    }
    public static async Task<IntonerSceneSnapshot> Capture(IDalamudPluginInterface pi,IntonerSceneRow row)
    {
        object dto; JsonObject root; JsonArray objects; string collections="[]";
        if(row.Temporary)
        {
            dto=Query(pi,"Scene.GetLoadedLayouts");
            var layout=Json(dto).AsArray().SingleOrDefault(l=>l!["SourceKey"]?.GetValue<string>()==row.Key) ?? throw new InvalidOperationException("Intoner source is no longer loaded. Refresh the browser.");
            objects=layout["Objects"]!.AsArray();
            root=Document(row.Name,objects.Select(o=>(JsonNode)new JsonObject { ["Object"]=o!.DeepClone(), ["FolderPath"]="", ["Locked"]=false }).ToArray(),new(),new());
            if(objects.Any(o=>!string.IsNullOrEmpty(o?["CollectionId"]?.GetValue<string>()))) collections=TemporaryCollections(pi,row.Key);
        }
        else
        {
            dto=pi.GetIpcSubscriber<Guid,object>("Intoner.Api.Layouts.Get").InvokeFunc(row.LayoutId!.Value) ?? throw new InvalidOperationException("Intoner layout was removed. Refresh the browser.");
            var layout=Json(dto); var content=layout["Content"]!;
            objects=content["Objects"]!.AsArray();
            var colors=content["FolderColors"] as JsonObject;
            var folders=new JsonArray(content["Folders"]!.AsArray().Select(f=>(JsonNode)new JsonObject { ["Path"]=f!.GetValue<string>(),["Color"]=colors?[f.GetValue<string>()]?.DeepClone() }).ToArray());
            root=Document(row.Name,objects.Select(o=>o!.DeepClone()).ToArray(),folders,(JsonArray?)content["Groups"]?.DeepClone() ?? new());
            if(objects.Any(o=>!string.IsNullOrEmpty(o?["Object"]?["CollectionId"]?.GetValue<string>())))
            {
                // Use the installed API's request type to retain new fields and avoid assembly identity conflicts.
                Type requestType=dto.GetType().Assembly.GetType("Intoner.Objects.Api.TemporarySourceBuildRequest") ?? throw new InvalidOperationException("Intoner sharing API is unavailable.");
                var request=new JsonObject { ["SourceId"]="Sceneweaver.Copy", ["SessionId"]=Guid.NewGuid(),["Name"]=row.Name,["Revision"]=1,
                    ["Objects"]=new JsonArray(objects.Select(o=>o!["Object"]!.DeepClone()).ToArray()) };
                var typed=JsonSerializer.Deserialize(request.ToJsonString(),requestType,ProjectJson.Options)!;
                var method=typeof(IDalamudPluginInterface).GetMethods().Single(m=>m.Name=="GetIpcSubscriber" && m.GetGenericArguments().Length==3);
                var subscriber=method.MakeGenericMethod(requestType,typeof(CancellationToken),typeof(Task<byte[]>)).Invoke(pi,["Intoner.Api.Sharing.BuildSource"])!;
                var contract=subscriber.GetType().GetInterfaces().Single(t=>t.IsGenericType && t.Name.StartsWith("ICallGateSubscriber`3"));
                var task=(Task<byte[]>)contract.GetMethod("InvokeFunc")!.Invoke(subscriber,[typed,CancellationToken.None])!;
                var built=JsonNode.Parse(await task)!;
                if(built["Source"]==null) throw new InvalidOperationException("Intoner could not resolve this layout's mods: "+built["Message"]);
                collections=built["Source"]!["Collections"]!.ToJsonString();
                // Builder generates collection IDs; keep folder/group metadata while adopting its object payloads.
                var generated=built["Source"]!["Objects"]!.AsArray().ToDictionary(o=>o!["Id"]!.GetValue<string>());
                foreach(var wrapper in root["Objects"]!.AsArray())
                {
                    var id=wrapper!["Object"]!["Id"]!.GetValue<string>();
                    if(!generated.TryGetValue(id,out var value)) throw new InvalidOperationException("Intoner sharing output omitted an object.");
                    // BuildSource wire JSON uses numeric enums. Normalize through the installed WorldObject type.
                    var type=dto.GetType().Assembly.GetType("Intoner.Objects.Api.WorldObject")!;
                    wrapper["Object"]=Json(JsonSerializer.Deserialize(value!.ToJsonString(),type,ProjectJson.Options)!);
                }
            }
        }
        return new(row.Key,row.Name,row.Temporary?"temporary scene":"saved layout",root.ToJsonString(),collections);
    }
    private static JsonObject Document(string name,JsonNode[] objects,JsonArray folders,JsonArray groups)
    {
        var now=DateTime.UtcNow;
        return new() { ["DocumentKind"]="object-layout",["FormatVersion"]=3,["Id"]=Guid.NewGuid(),["Name"]=name,["Revision"]=1,
            ["CreatedAtUtc"]=now,["UpdatedAtUtc"]=now,["ExportedAtUtc"]=now,["Folders"]=folders,["Groups"]=groups,["Objects"]=new JsonArray(objects) };
    }
    private static string TemporaryCollections(IDalamudPluginInterface pi,string key)
    {
        var exposed=pi.InstalledPlugins.Single(p=>p.InternalName=="Intoner" && p.IsLoaded);
        var local=Field(exposed,"<plugin>P"); var instance=Field(local,"instance");
        var session=Field(Field(instance,"_host"),"_sessionHost"); var scope=Field(session,"_scope");
        var provider=(IServiceProvider)scope.GetType().GetProperty("ServiceProvider")!.GetValue(scope)!;
        var type=instance.GetType().Assembly.GetType("Intoner.Objects.Runtime.ITemporarySourceStore") ?? throw new InvalidOperationException("Intoner temporary collections API changed.");
        var store=provider.GetService(type) ?? throw new InvalidOperationException("Intoner session is not ready.");
        var sources=(IEnumerable)type.GetMethod("GetSources")!.Invoke(store,null)!;
        foreach(var source in sources)
        {
            if(source.GetType().GetProperty("SourceKey")!.GetValue(source)?.ToString()!=key) continue;
            var collections=Json(source.GetType().GetProperty("RuntimeCollections")!.GetValue(source)!).AsArray();
            long total=0;
            foreach(var collection in collections)
                foreach(var redirect in collection!["Redirects"]!.AsArray())
                {
                    var replacement=redirect!["Replacement"]!;
                    if(replacement["Kind"]!.ToString() is not ("Memory" or "3")) continue;
                    var memoryType=instance.GetType().Assembly.GetType("Intoner.Objects.Resources.IObjectMemoryResourceService") ?? throw new InvalidOperationException("Intoner memory resource interface changed.");
                    var memory=provider.GetService(memoryType) ?? throw new InvalidOperationException("Intoner memory resources are unavailable.");
                    object?[] args=[replacement["Path"]!.GetValue<string>(),null];
                    if(!(bool)memoryType.GetMethod("TryGetResource")!.Invoke(memory,args)!) throw new InvalidOperationException("An Intoner memory resource disappeared during capture.");
                    var resource=args[1]!; var data=(byte[])resource.GetType().GetProperty("Data")!.GetValue(resource)!;
                    total+=data.Length;
                    if(data.Length>ModImport.MaxResourceBytes || total>128L*1024*1024) throw new InvalidOperationException("Intoner memory resources exceed capture limits.");
                    replacement["Data"]=Convert.ToBase64String(data);
                }
            return collections.ToJsonString();
        }
        throw new InvalidOperationException("Intoner source disappeared while copying its collections.");
    }
}
