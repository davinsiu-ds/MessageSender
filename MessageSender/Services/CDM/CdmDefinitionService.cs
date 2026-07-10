using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MessageSender.Services.CDM;

/// <summary>
/// Result of attempting to load CDM definitions.
/// </summary>
public class CdmLoadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ClustersLoaded { get; set; }
}

/// <summary>
/// Loads CDM definition files from a local clone of the cdm-definitions repo
/// and provides lookup access to clusters, message structs, and enums.
/// </summary>
public class CdmDefinitionService
{
    private readonly Dictionary<int, CdmCluster> _clusters = new();
    private CdmMessageDefinitions? _messageStructs;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public bool IsLoaded { get; private set; }
    public string LoadedPath { get; private set; } = string.Empty;

    /// <summary>
    /// Loads (or reloads) all definitions from the given folder path.
    /// The path should point to the root of the cdm-definitions repository.
    /// Returns a CdmLoadResult indicating success/failure and providing diagnostic information.
    /// </summary>
    public CdmLoadResult Load(string definitionsPath)
    {
        _clusters.Clear();
        IsLoaded = false;

        // Validate path exists
        if (string.IsNullOrWhiteSpace(definitionsPath))
            return new CdmLoadResult { Success = false, Message = "Path is empty." };

        if (!Directory.Exists(definitionsPath))
            return new CdmLoadResult { Success = false, Message = $"Directory does not exist: {definitionsPath}" };

        // Validate directory structure
        var msgTypesDir = Path.Combine(definitionsPath, "MsgTypes");
        if (!Directory.Exists(msgTypesDir))
            return new CdmLoadResult { Success = false, Message = $"Required 'MsgTypes' folder not found in: {definitionsPath}" };

        var clustersDir = Path.Combine(definitionsPath, "Clusters");
        if (!Directory.Exists(clustersDir))
            return new CdmLoadResult { Success = false, Message = $"Required 'Clusters' folder not found in: {definitionsPath}" };

        // Load MsgTypes/CDMMessageStructs.json (required)
        var msgStructsPath = Path.Combine(msgTypesDir, "CDMMessageStructs.json");
        if (!File.Exists(msgStructsPath))
            return new CdmLoadResult { Success = false, Message = $"Required file not found: MsgTypes/CDMMessageStructs.json" };

        try
        {
            _messageStructs = JsonSerializer.Deserialize<CdmMessageDefinitions>(
                File.ReadAllText(msgStructsPath), _options);

            if (_messageStructs is null)
                return new CdmLoadResult { Success = false, Message = "CDMMessageStructs.json is empty or invalid." };
        }
        catch (Exception ex)
        {
            return new CdmLoadResult { Success = false, Message = $"Failed to parse CDMMessageStructs.json: {ex.Message}" };
        }

        // Load all cluster JSON files from Clusters/ (at least one required)
        var clusterFiles = Directory.GetFiles(clustersDir, "*.json");
        if (clusterFiles.Length == 0)
            return new CdmLoadResult { Success = false, Message = "No cluster definition files found in Clusters/ folder." };

        int loadedCount = 0;
        int failedCount = 0;

        foreach (var file in clusterFiles)
        {
            try
            {
                var cluster = JsonSerializer.Deserialize<CdmCluster>(
                    File.ReadAllText(file), _options);
                if (cluster is null)
                {
                    failedCount++;
                    continue;
                }

                // The Name field is at the end of each cluster JSON;
                // fall back to the file name when it is absent.
                if (string.IsNullOrEmpty(cluster.Name))
                    cluster.Name = Path.GetFileNameWithoutExtension(file);

                _clusters[cluster.ID] = cluster;
                loadedCount++;
            }
            catch
            {
                failedCount++;
            }
        }

        if (loadedCount == 0)
            return new CdmLoadResult { Success = false, Message = "No valid cluster files could be loaded from Clusters/ folder." };

        IsLoaded = true;
        LoadedPath = definitionsPath;

        var warningMessage = failedCount > 0 
            ? $"Loaded {loadedCount} cluster definitions ({failedCount} files failed to parse)."
            : $"Successfully loaded {loadedCount} cluster definitions.";

        return new CdmLoadResult { Success = true, Message = warningMessage, ClustersLoaded = loadedCount };
    }

    // ── Message-struct lookups ──────────────────────────────────────────────

    public CdmCommand? GetMessageCommand(int messageTypeId) =>
        _messageStructs?.Commands.TryGetValue(messageTypeId.ToString(), out var c) == true ? c : null;

    public CdmStruct? GetMessageStruct(int structId) =>
        _messageStructs?.Structs.TryGetValue(structId.ToString(), out var s) == true ? s : null;

    public CdmEnumDef? GetMessageEnum(int enumId) =>
        _messageStructs?.Enums.TryGetValue(enumId.ToString(), out var e) == true ? e : null;

    // ── Cluster lookups ─────────────────────────────────────────────────────

    public CdmCluster? GetCluster(int clusterId) =>
        _clusters.TryGetValue(clusterId, out var c) ? c : null;
}
