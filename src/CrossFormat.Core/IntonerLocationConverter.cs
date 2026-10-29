using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Intoner.Objects.Api;
namespace CrossFormat.Core;

// Accept legacy location objects even inside canonical projects saved by older Sceneweaver versions.
public sealed class IntonerLocationConverter : JsonConverter<ObjectLocationData>
{
    public override ObjectLocationData Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var raw=JsonNode.Parse(ref reader)!.AsObject();
        bool legacy=raw.ContainsKey("WorldId"), current=raw.ContainsKey("Worlds");
        if(legacy==current) throw new JsonException("Intoner location must contain either Worlds or legacy WorldId, not both.");
        ObjectWorldSelection? worlds=null;
        if(legacy)
        {
            var id=raw["WorldId"]!.GetValue<ushort>();
            if(id!=0) worlds=new(false,worldIds:[id]);
        }
        else if(raw["Worlds"] is JsonObject w)
            worlds=new(w["AllDataCenters"]!.GetValue<bool>(),
                (w["DataCenterIds"]!.AsArray().Select(n=>n!.GetValue<uint>())).ToImmutableArray(),
                (w["WorldIds"]!.AsArray().Select(n=>n!.GetValue<ushort>())).ToImmutableArray());
        uint Number(string key)=>raw[key]!.GetValue<uint>();
        return new(worlds,Number("TerritoryId"),raw["TerritoryName"]!.GetValue<string>(),Number("DivisionId"),Number("WardId"),Number("HouseId"),Number("RoomId"));
    }
    public override void Write(Utf8JsonWriter writer,ObjectLocationData value,JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WritePropertyName("Worlds"); JsonSerializer.Serialize(writer,value.Worlds,options);
        writer.WriteNumber("TerritoryId",value.TerritoryId); writer.WriteString("TerritoryName",value.TerritoryName);
        writer.WriteNumber("DivisionId",value.DivisionId); writer.WriteNumber("WardId",value.WardId);
        writer.WriteNumber("HouseId",value.HouseId); writer.WriteNumber("RoomId",value.RoomId); writer.WriteEndObject();
    }
    public static JsonNode Upgrade(JsonNode value)
    {
        var options=new JsonSerializerOptions(ProjectJson.Options); options.Converters.Insert(0,new IntonerLocationConverter());
        return JsonSerializer.SerializeToNode(value.Deserialize<ObjectLocationData>(options),options)!;
    }
}
