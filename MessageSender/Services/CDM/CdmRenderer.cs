using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MessageSender.Services.CDM;

/// <summary>
/// Converts a raw CDM message body (numeric-keyed JSON) into a human-readable
/// representation by resolving field, attribute, struct, and enum names from
/// the loaded CDM definitions.
/// </summary>
public class CdmRenderer
{
    private readonly CdmDefinitionService _service;

    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    public CdmRenderer(CdmDefinitionService service) => _service = service;

    // ── Public helpers ──────────────────────────────────────────────────────

    /// <summary>Returns true when the user-properties JSON contains a cdmmessagetype key.</summary>
    public static bool IsCdmMessage(string userPropertiesJson)
    {
        if (string.IsNullOrWhiteSpace(userPropertiesJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(userPropertiesJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Name.Equals("cdmmessagetype", StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        catch { }
        return false;
    }

    /// <summary>Extracts the numeric CDM message type ID from user-properties JSON.</summary>
    public static int? GetCdmMessageTypeId(string userPropertiesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(userPropertiesJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Name.Equals("cdmmessagetype", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out int id)) return id;
                if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out int sid)) return sid;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Renders user properties JSON into human-readable format.
    /// Converts `cdmmessagetype` to the message command name, and decodes `flags` bitfield.
    /// Handles both numeric values and numeric strings.
    /// </summary>
    public string RenderUserProperties(string userPropertiesJson)
    {
        if (!_service.IsLoaded) return userPropertiesJson;

        try
        {
            var root = JsonNode.Parse(userPropertiesJson) as JsonObject;
            if (root is null) return userPropertiesJson;

            var result = new JsonObject();
            foreach (var kv in root)
            {
                var key = kv.Key;
                var value = kv.Value;

                if (value is null) { result[key] = null; continue; }

                // Render cdmmessagetype
                if (key.Equals("cdmmessagetype", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseIntValue(value, out int msgId))
                    {
                        var cmd = _service.GetMessageCommand(msgId);
                        var cmdName = cmd?.Name ?? msgId.ToString();
                        result[key] = JsonValue.Create($"{cmdName} ({msgId})");
                    }
                    else
                    {
                        result[key] = value.DeepClone();
                    }
                }
                // Render flags as human-readable bit descriptions
                else if (key.Equals("flags", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseIntValue(value, out int flagsValue))
                    {
                        var flagDescriptions = DecodeFlagsValue(flagsValue);
                        var flagsDisplay = string.IsNullOrEmpty(flagDescriptions) ? "0 (no flags set)" : flagDescriptions;
                        result[key] = JsonValue.Create($"{flagsDisplay} (0x{flagsValue:X8})");
                    }
                    else
                    {
                        result[key] = value.DeepClone();
                    }
                }
                // Copy other properties as-is
                else
                {
                    result[key] = value.DeepClone();
                }
            }

            return result.ToJsonString(_writeOptions);
        }
        catch
        {
            return userPropertiesJson;
        }
    }

    /// <summary>
    /// Attempts to parse an int value from either a JsonValue (number or string).
    /// Returns true if the value could be parsed as an integer.
    /// </summary>
    private static bool TryParseIntValue(JsonNode? node, out int result)
    {
        result = 0;
        if (node is null) return false;

        // Try as JsonValue (numeric or string)
        if (node is JsonValue jv)
        {
            // Try direct numeric value
            if (jv.TryGetValue<int>(out result)) return true;

            // Try as string
            if (jv.TryGetValue<string>(out var strValue) && int.TryParse(strValue, out result)) return true;
        }

        return false;
    }

    /// <summary>
    /// Decodes a 32-bit flags value into human-readable flag descriptions.
    /// Based on CDM Message Header specification.
    /// </summary>
    private static string DecodeFlagsValue(int flags)
    {
        var descriptions = new List<string>();

        // Bit 7: Application Level Ack
        if ((flags & 0x80) != 0)
            descriptions.Add("ApplicationLevelAck");

        // Bit 6: Signaling
        if ((flags & 0x40) != 0)
            descriptions.Add("Signaling");

        // Bits 4-5: Priority (0-3)
        var priority = (flags >> 4) & 0x3;
        if (priority > 0)
            descriptions.Add($"Priority{priority}");

        // Bit 3: ForceAlternatePath
        if ((flags & 0x08) != 0)
            descriptions.Add("ForceAlternatePath");

        // Bit 2: DropAfterOneAttempt
        if ((flags & 0x04) != 0)
            descriptions.Add("DropAfterOneAttempt");

        // Bits 0-1: Encoding Type
        var encoding = flags & 0x3;
        var encodingName = encoding switch
        {
            0b00 => "DefaultEncoding",    // Default encoding (Indexed JSON, msgpack)
            0b10 => "IndexedJSONOnly",    // Indexed JSON, no msgpack
            _ => $"Encoding{encoding:b2}"
        };
        descriptions.Add(encodingName);

        return string.Join(", ", descriptions);
    }

    // ── Main render entry point ─────────────────────────────────────────────

    /// <summary>
    /// Renders the raw CDM body JSON into a human-readable JSON string.
    /// Falls back to the original <paramref name="bodyJson"/> on any error or if definitions are not loaded.
    /// </summary>
    public string Render(string bodyJson, string userPropertiesJson)
    {
        if (!_service.IsLoaded) return bodyJson;

        var msgTypeId = GetCdmMessageTypeId(userPropertiesJson);
        if (msgTypeId is null) return bodyJson;

        var msgCommand = _service.GetMessageCommand(msgTypeId.Value);
        if (msgCommand is null) return bodyJson;

        try
        {
            var root = JsonNode.Parse(bodyJson);
            if (root is null) return bodyJson;
            return RenderObjectWithFields(root, msgCommand.Request, cluster: null)
                       .ToJsonString(_writeOptions);
        }
        catch
        {
            return bodyJson;
        }
    }

    // ── Rendering helpers ───────────────────────────────────────────────────

    /// <summary>Renders a JSON object whose keys are numeric field IDs from <paramref name="fields"/>.</summary>
    private JsonNode RenderObjectWithFields(JsonNode node, Dictionary<string, CdmField> fields, CdmCluster? cluster)
    {
        if (node is not JsonObject obj) return node.DeepClone();

        var result = new JsonObject();
        foreach (var kv in obj)
        {
            fields.TryGetValue(kv.Key, out var field);
            var key = field?.Name ?? kv.Key;
            result[key] = kv.Value is null ? null : RenderValue(kv.Value, field, cluster);
        }
        return result;
    }

    /// <summary>Renders a single value given its (optional) field definition and cluster context.</summary>
    private JsonNode? RenderValue(JsonNode value, CdmField? field, CdmCluster? cluster)
    {
        if (field is null) return value.DeepClone();

        // Arrays
        if (field.IsArray && value is JsonArray arr)
        {
            var result = new JsonArray();
            foreach (var item in arr)
                result.Add(item is null ? null : RenderArrayItem(item, field, cluster));
            return result;
        }

        // Struct reference
        if (field.StructID.HasValue && value is JsonObject)
        {
            var structDef = ResolveStruct(field.StructID.Value, cluster);
            if (structDef is not null) return RenderStruct(value, structDef, cluster);
        }

        // Enum: replace numeric value with "Name (value)"
        if (field.EnumID.HasValue && value is JsonValue jv && jv.TryGetValue<int>(out int enumVal))
        {
            var enumDef = ResolveEnum(field.EnumID.Value, cluster);
            var item = enumDef?.Items.Find(i => i.Value == enumVal);
            if (item is not null) return JsonValue.Create($"{item.Name} ({enumVal})");
        }

        return value.DeepClone();
    }

    private JsonNode RenderArrayItem(JsonNode item, CdmField field, CdmCluster? cluster)
    {
        if (field.StructID.HasValue && item is JsonObject)
        {
            var structDef = ResolveStruct(field.StructID.Value, cluster);
            if (structDef is not null) return RenderStruct(item, structDef, cluster);
        }
        return item.DeepClone();
    }

    /// <summary>
    /// Renders a JSON object using a struct definition.
    /// Handles the special case of AttributeDataIB / EventReportIB by first
    /// resolving the Cluster field (key "1") to find the active cluster definition,
    /// then using that cluster to decode the Attributes field (key "2").
    /// </summary>
    private JsonNode RenderStruct(JsonNode node, CdmStruct structDef, CdmCluster? parentCluster)
    {
        if (node is not JsonObject obj) return node.DeepClone();

        // For IB types that carry a cluster context, resolve it from field "1"
        CdmCluster? activeCluster = parentCluster;
        if (structDef.Name is "AttributeDataIB" or "EventReportIB")
        {
            if (obj["1"] is JsonValue cv && cv.TryGetValue<int>(out int clusterId))
                activeCluster = _service.GetCluster(clusterId);
        }

        var result = new JsonObject();
        foreach (var kv in obj)
        {
            structDef.Attributes.TryGetValue(kv.Key, out var field);
            var key = field?.Name ?? kv.Key;
            var value = kv.Value;
            if (value is null) { result[key] = null; continue; }

            if (field is null)
            {
                result[key] = value.DeepClone();
            }
            // Cluster ID field → show "ClusterName (ID)"
            else if (field.Name == "Cluster" && value is JsonValue cval && cval.TryGetValue<int>(out int cid))
            {
                var name = _service.GetCluster(cid)?.Name ?? cid.ToString();
                result[key] = JsonValue.Create($"{name} ({cid})");
            }
            // EventID field in EventReportIB → show "EventName (ID)" using the active cluster's event definitions
            else if (field.Name == "EventID" && activeCluster is not null && value is JsonValue eval && eval.TryGetValue<int>(out int eid))
            {
                var eventName = activeCluster.Events.TryGetValue(eid.ToString(), out var ev) ? ev.Name : eid.ToString();
                result[key] = JsonValue.Create($"{eventName} ({eid})");
            }
            // Variable-typed Attributes field in AttributeDataIB → decode using cluster attributes
            else if (field.Name == "Attributes" && field.Type == "Variable" && activeCluster is not null && value is JsonObject attrObj)
            {
                result[key] = RenderClusterAttributes(attrObj, activeCluster);
            }
            // Array of structs
            else if (field.IsArray && value is JsonArray arr)
            {
                var resultArr = new JsonArray();
                foreach (var item in arr)
                    resultArr.Add(item is null ? null : RenderArrayItem(item, field, activeCluster));
                result[key] = resultArr;
            }
            else
            {
                result[key] = RenderValue(value, field, activeCluster);
            }
        }
        return result;
    }

    /// <summary>
    /// Renders the Attributes tree of a specific cluster.
    /// Top-level keys are attribute IDs from the cluster definition.
    /// </summary>
    private JsonNode RenderClusterAttributes(JsonObject attrObj, CdmCluster cluster)
    {
        var result = new JsonObject();
        foreach (var kv in attrObj)
        {
            cluster.Attributes.TryGetValue(kv.Key, out var field);
            var key = field?.Name ?? kv.Key;
            var value = kv.Value;
            if (value is null) { result[key] = null; continue; }

            result[key] = field is not null
                ? RenderClusterAttributeValue(value, field, cluster)
                : value.DeepClone();
        }
        return result;
    }

    private JsonNode? RenderClusterAttributeValue(JsonNode value, CdmField field, CdmCluster cluster)
    {
        // ClusterID reference → show "ClusterName (ID)"
        if (field.Name == "ClusterID" && value is JsonValue cidVal && cidVal.TryGetValue<int>(out int refClusterId))
        {
            var name = _service.GetCluster(refClusterId)?.Name ?? refClusterId.ToString();
            return JsonValue.Create($"{name} ({refClusterId})");
        }

        // cdmarray: keyed by index, values are structs
        if (field.Type.Equals("cdmarray", StringComparison.OrdinalIgnoreCase) && value is JsonObject cdmArr)
        {
            CdmStruct? structDef = field.StructID.HasValue
                ? (cluster.Structs.TryGetValue(field.StructID.Value.ToString(), out var s) ? s : null)
                : null;

            var result = new JsonObject();
            foreach (var kv in cdmArr)
                result[$"[{kv.Key}]"] = kv.Value is null ? null
                    : (structDef is not null ? RenderClusterStruct(kv.Value, structDef, cluster) : kv.Value.DeepClone());
            return result;
        }

        // struct
        if (field.Type.Equals("struct", StringComparison.OrdinalIgnoreCase) && field.StructID.HasValue && value is JsonObject)
        {
            if (cluster.Structs.TryGetValue(field.StructID.Value.ToString(), out var structDef))
                return RenderClusterStruct(value, structDef, cluster);
        }

        // enum
        if (field.EnumID.HasValue && value is JsonValue jv && jv.TryGetValue<int>(out int ev))
        {
            if (cluster.Enums.TryGetValue(field.EnumID.Value.ToString(), out var enumDef))
            {
                var item = enumDef.Items.Find(i => i.Value == ev);
                if (item is not null) return JsonValue.Create($"{item.Name} ({ev})");
            }
        }

        return value.DeepClone();
    }

    private JsonNode RenderClusterStruct(JsonNode node, CdmStruct structDef, CdmCluster cluster)
    {
        if (node is not JsonObject obj) return node.DeepClone();

        var result = new JsonObject();
        foreach (var kv in obj)
        {
            structDef.Attributes.TryGetValue(kv.Key, out var field);
            var key = field?.Name ?? kv.Key;
            result[key] = kv.Value is null ? null
                : (field is not null ? RenderClusterAttributeValue(kv.Value, field, cluster) : kv.Value.DeepClone());
        }
        return result;
    }

    // ── Resolution helpers ──────────────────────────────────────────────────

    private CdmStruct? ResolveStruct(int id, CdmCluster? cluster)
    {
        var s = _service.GetMessageStruct(id);
        if (s is not null) return s;
        if (cluster?.Structs.TryGetValue(id.ToString(), out var cs) == true) return cs;
        return null;
    }

    private CdmEnumDef? ResolveEnum(int id, CdmCluster? cluster)
    {
        var e = _service.GetMessageEnum(id);
        if (e is not null) return e;
        if (cluster?.Enums.TryGetValue(id.ToString(), out var ce) == true) return ce;
        return null;
    }
}
