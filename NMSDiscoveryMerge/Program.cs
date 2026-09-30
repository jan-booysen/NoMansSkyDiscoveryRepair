using System.Text.Json.Nodes;

const string backupPath = "_backup.json";
const string newPath = "new.json";
const string outputPath = "merged.json";

if (!File.Exists(backupPath) || !File.Exists(newPath))
{
    Console.WriteLine($"ERROR: Required files not found. Need '{backupPath}' and '{newPath}'.");
    return 1;
}

try
{
    var backupRoot = JsonNode.Parse(File.ReadAllText(backupPath))?.AsObject()
        ?? throw new Exception("Invalid backup JSON.");
    var newRoot = JsonNode.Parse(File.ReadAllText(newPath))?.AsObject()
        ?? throw new Exception("Invalid new JSON.");

    var backupDiscovery = GetDiscovery(backupRoot);
    var newDiscovery = GetDiscovery(newRoot);
    var backupRecords = GetRecords(backupDiscovery);
    var newRecords = GetRecords(newDiscovery);

    Console.WriteLine("NMS Discovery Merge");
    Console.WriteLine("==================");
    Console.WriteLine($"Backup records : {backupRecords.Count}");
    Console.WriteLine($"New records    : {newRecords.Count}");
    Console.WriteLine();

    var newByRid = new Dictionary<string, List<(int Index, JsonObject Record)>>();
    var newByLegacy = new Dictionary<string, List<(int Index, JsonObject Record)>>();

    for (int i = 0; i < newRecords.Count; i++)
    {
        if (newRecords[i] is not JsonObject r) continue;

        var rid = GetString(r, "RID");
        if (!string.IsNullOrWhiteSpace(rid))
            Add(newByRid, rid, (i, r));

        var legacy = LegacyIdentity(r);
        if (legacy != null)
            Add(newByLegacy, legacy, (i, r));
    }

    var replacements = new Dictionary<int, JsonObject>();
    var additions = new List<JsonObject>();

    int matchedRid = 0;
    int matchedLegacy = 0;
    int replacedMissingRid = 0;
    int restored = 0;

    foreach (var node in backupRecords)
    {
        if (node is not JsonObject backup) continue;

        JsonObject? match = null;
        int index = -1;

        var rid = GetString(backup, "RID");

        if (!string.IsNullOrWhiteSpace(rid) &&
            newByRid.TryGetValue(rid, out var ridMatches) &&
            ridMatches.Count == 1)
        {
            index = ridMatches[0].Index;
            match = ridMatches[0].Record;
            matchedRid++;
        }
        else
        {
            var legacy = LegacyIdentity(backup);

            if (legacy != null &&
                newByLegacy.TryGetValue(legacy, out var legacyMatches) &&
                legacyMatches.Count == 1)
            {
                index = legacyMatches[0].Index;
                match = legacyMatches[0].Record;
                matchedLegacy++;
            }
        }

        if (match == null)
        {
            additions.Add(backup.DeepClone().AsObject());
            restored++;
        }
        else
        {
            var newRid = GetString(match, "RID");

            // Only replace NEW when the backup has a RID and the
            // matching NEW record does not. This preserves the more
            // complete historical representation.
            if (!string.IsNullOrWhiteSpace(rid) &&
                string.IsNullOrWhiteSpace(newRid))
            {
                replacements[index] = backup.DeepClone().AsObject();
                replacedMissingRid++;
            }
        }
    }

    Console.WriteLine("Analysis");
    Console.WriteLine("--------");
    Console.WriteLine($"Matched by RID                  : {matchedRid}");
    Console.WriteLine($"Matched by legacy identity      : {matchedLegacy}");
    Console.WriteLine($"NEW records replaced (no RID)   : {replacedMissingRid}");
    Console.WriteLine($"Backup records restored          : {restored}");
    Console.WriteLine($"Final record count               : {newRecords.Count + additions.Count}");
    Console.WriteLine();

    Console.WriteLine("Type MERGE to create merged.json:");
    if (!string.Equals(Console.ReadLine(), "MERGE", StringComparison.Ordinal))
    {
        Console.WriteLine("Merge cancelled.");
        return 0;
    }

    var mergedRoot = newRoot.DeepClone().AsObject();
    var mergedDiscovery = GetDiscovery(mergedRoot);
    var mergedRecords = GetRecords(mergedDiscovery);

    foreach (var pair in replacements)
        mergedRecords[pair.Key] = pair.Value.DeepClone();

    foreach (var record in additions)
        mergedRecords.Add(record.DeepClone());

    mergedDiscovery["ReserveStore"] = mergedRecords.Count;

    string newBackup = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(newPath))!,
        Path.GetFileNameWithoutExtension(newPath) + "_merge_backup" +
        Path.GetExtension(newPath));

    if (!File.Exists(newBackup))
        File.Copy(newPath, newBackup);
    else
        Console.WriteLine($"Existing backup preserved: {newBackup}");

    File.WriteAllText(outputPath, mergedRoot.ToJsonString(new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true
    }));

    Console.WriteLine();
    Console.WriteLine("MERGE COMPLETE");
    Console.WriteLine($"Output : {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Records: {mergedRecords.Count}");
    Console.WriteLine($"Backup : {Path.GetFullPath(newBackup)}");
    Console.WriteLine();
    Console.WriteLine("Original _backup.json and new.json were not modified.");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    return 1;
}

static JsonObject GetDiscovery(JsonObject root)
{
    if (root["DiscoveryManagerData"]?["DiscoveryData-v1"] is JsonObject nested)
        return nested;
    if (root["DiscoveryData-v1"] is JsonObject direct)
        return direct;
    throw new Exception("DiscoveryData-v1 not found.");
}

static JsonArray GetRecords(JsonObject discovery)
{
    if (discovery["Store"]?["Record"] is JsonArray records)
        return records;
    throw new Exception("DiscoveryData-v1.Store.Record not found.");
}

static string? GetString(JsonObject obj, string property)
{
    var node = obj[property];
    if (node == null) return null;
    return node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToJsonString();
}

static string? LegacyIdentity(JsonObject record)
{
    if (record["DD"] is not JsonObject dd) return null;
    if (dd["DT"] == null || dd["UA"] == null) return null;

    var id = dd["DT"]!.ToJsonString() + "|" + dd["UA"]!.ToJsonString();

    // VP is part of the identity when present. SpaceStation records
    // can legitimately have no VP.
    if (dd["VP"] != null)
        id += "|" + dd["VP"]!.ToJsonString();

    return id;
}

static void Add<T>(Dictionary<string, List<T>> dict, string key, T value)
{
    if (!dict.TryGetValue(key, out var list))
        dict[key] = list = new List<T>();
    list.Add(value);
}
