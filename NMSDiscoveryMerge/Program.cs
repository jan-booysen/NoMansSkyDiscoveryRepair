using System.Text.Json.Nodes;

const string backupPath = "_backup.json";
const string newPath = "new.json";
const string mergedPath = "merged.json";

Console.WriteLine("NMS Discovery Merge Diagnostic");
Console.WriteLine("==============================");
Console.WriteLine();

if (!File.Exists(backupPath))
{
    Console.WriteLine($"ERROR: {backupPath} not found.");
    return;
}

if (!File.Exists(newPath))
{
    Console.WriteLine($"ERROR: {newPath} not found.");
    return;
}

if (!File.Exists(mergedPath))
{
    Console.WriteLine($"ERROR: {mergedPath} not found.");
    return;
}

try
{
    var backupRecords = LoadRecords(backupPath);
    var newRecords = LoadRecords(newPath);
    var mergedRecords = LoadRecords(mergedPath);

    PrintSummary("BACKUP", backupRecords);
    PrintSummary("NEW", newRecords);
    PrintSummary("MERGED", mergedRecords);

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("DUPLICATE CHECKS");
    Console.WriteLine("========================================");

    CheckDuplicates("BACKUP", backupRecords);
    CheckDuplicates("NEW", newRecords);
    CheckDuplicates("MERGED", mergedRecords);

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("MERGE CONTENT CHECK");
    Console.WriteLine("========================================");

    CheckContainsAll(
        "NEW -> MERGED",
        newRecords,
        mergedRecords);

    CheckContainsAll(
        "BACKUP -> MERGED",
        backupRecords,
        mergedRecords);

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("DRLIZZARD CHECK");
    Console.WriteLine("========================================");

    PrintOwnerStats(
        "BACKUP",
        backupRecords,
        "DrLizzard");

    PrintOwnerStats(
        "NEW",
        newRecords,
        "DrLizzard");

    PrintOwnerStats(
        "MERGED",
        mergedRecords,
        "DrLizzard");

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("PLANET CHECK");
    Console.WriteLine("========================================");

    Console.WriteLine(
        "If you know the UA of the planet that became Unknown,");
    Console.WriteLine(
        "enter it below. Press ENTER to skip.");

    Console.Write("Planet UA: ");

    string? planetUa = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(planetUa))
    {
        CheckPlanet(
            "BACKUP",
            backupRecords,
            planetUa);

        CheckPlanet(
            "NEW",
            newRecords,
            planetUa);

        CheckPlanet(
            "MERGED",
            mergedRecords,
            planetUa);
    }

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("DIAGNOSTIC COMPLETE");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("ERROR:");
    Console.WriteLine(ex.Message);
}


// ============================================================
// LOAD
// ============================================================

static List<JsonObject> LoadRecords(string path)
{
    var root = JsonNode.Parse(
        File.ReadAllText(path))?.AsObject();

    if (root == null)
        throw new Exception($"Invalid JSON: {path}");

    JsonObject? discovery = null;

    if (root["DiscoveryManagerData"]?["DiscoveryData-v1"]
        is JsonObject nested)
    {
        discovery = nested;
    }
    else if (root["DiscoveryData-v1"]
        is JsonObject direct)
    {
        discovery = direct;
    }

    if (discovery == null)
        throw new Exception(
            $"DiscoveryData-v1 not found in {path}");

    if (discovery["Store"]?["Record"]
        is not JsonArray records)
    {
        throw new Exception(
            $"DiscoveryData-v1.Store.Record not found in {path}");
    }

    return records
        .Select(x => x as JsonObject)
        .Where(x => x != null)
        .Cast<JsonObject>()
        .ToList();
}


// ============================================================
// SUMMARY
// ============================================================

static void PrintSummary(
    string name,
    List<JsonObject> records)
{
    int withRid = 0;
    int withoutRid = 0;

    var types =
        new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

    foreach (var record in records)
    {
        string? rid =
            GetString(record, "RID");

        if (string.IsNullOrWhiteSpace(rid))
            withoutRid++;
        else
            withRid++;

        string type =
            GetNestedString(record, "DD", "DT")
            ?? "<missing>";

        Increment(types, type);
    }

    Console.WriteLine();
    Console.WriteLine(name);
    Console.WriteLine($"  Records     : {records.Count}");
    Console.WriteLine($"  With RID    : {withRid}");
    Console.WriteLine($"  Without RID : {withoutRid}");

    Console.WriteLine("  Types:");

    foreach (var item in types.OrderByDescending(x => x.Value))
    {
        Console.WriteLine(
            $"    {item.Key,-15} : {item.Value}");
    }
}


// ============================================================
// DUPLICATES
// ============================================================

static void CheckDuplicates(
    string name,
    List<JsonObject> records)
{
    var ridCounts =
        new Dictionary<string, int>(
            StringComparer.Ordinal);

    var legacyCounts =
        new Dictionary<string, int>(
            StringComparer.Ordinal);

    foreach (var record in records)
    {
        string? rid =
            GetString(record, "RID");

        if (!string.IsNullOrWhiteSpace(rid))
            Increment(ridCounts, rid);

        string? legacy =
            GetLegacyIdentity(record);

        if (legacy != null)
            Increment(legacyCounts, legacy);
    }

    var duplicateRids =
        ridCounts
            .Where(x => x.Value > 1)
            .OrderByDescending(x => x.Value)
            .ToList();

    var duplicateLegacy =
        legacyCounts
            .Where(x => x.Value > 1)
            .OrderByDescending(x => x.Value)
            .ToList();

    Console.WriteLine();
    Console.WriteLine(name);

    Console.WriteLine(
        $"  Duplicate RIDs              : {duplicateRids.Count}");

    foreach (var item in duplicateRids.Take(10))
    {
        Console.WriteLine(
            $"    {item.Value}x {item.Key}");
    }

    Console.WriteLine(
        $"  Duplicate legacy identities : {duplicateLegacy.Count}");

    foreach (var item in duplicateLegacy.Take(10))
    {
        Console.WriteLine(
            $"    {item.Value}x {item.Key}");
    }
}


// ============================================================
// SOURCE -> MERGED CHECK
// ============================================================

static void CheckContainsAll(
    string label,
    List<JsonObject> source,
    List<JsonObject> merged)
{
    var mergedRids =
        new HashSet<string>(
            merged
                .Select(x => GetString(x, "RID"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>(),
            StringComparer.Ordinal);

    var mergedLegacy =
        new HashSet<string>(
            merged
                .Select(GetLegacyIdentity)
                .Where(x => x != null)
                .Cast<string>(),
            StringComparer.Ordinal);

    int missingRid = 0;
    int missingLegacy = 0;

    foreach (var record in source)
    {
        string? rid =
            GetString(record, "RID");

        if (!string.IsNullOrWhiteSpace(rid))
        {
            if (!mergedRids.Contains(rid))
                missingRid++;
        }
        else
        {
            string? legacy =
                GetLegacyIdentity(record);

            if (legacy != null &&
                !mergedLegacy.Contains(legacy))
            {
                missingLegacy++;
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine(label);
    Console.WriteLine($"  Source records         : {source.Count}");
    Console.WriteLine($"  Missing RID identities : {missingRid}");
    Console.WriteLine($"  Missing legacy IDs     : {missingLegacy}");

    if (missingRid == 0 &&
        missingLegacy == 0)
    {
        Console.WriteLine(
            "  RESULT: All source identities are represented.");
    }
    else
    {
        Console.WriteLine(
            "  RESULT: Some source identities are missing.");
    }
}


// ============================================================
// OWNER CHECK
// ============================================================

static void PrintOwnerStats(
    string name,
    List<JsonObject> records,
    string owner)
{
    int total = 0;
    int withRid = 0;
    int withoutRid = 0;

    var types =
        new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

    foreach (var record in records)
    {
        string? usn =
            GetNestedString(record, "OWS", "USN");

        if (!string.Equals(
                usn,
                owner,
                StringComparison.Ordinal))
        {
            continue;
        }

        total++;

        string? rid =
            GetString(record, "RID");

        if (string.IsNullOrWhiteSpace(rid))
            withoutRid++;
        else
            withRid++;

        string type =
            GetNestedString(record, "DD", "DT")
            ?? "<missing>";

        Increment(types, type);
    }

    Console.WriteLine();
    Console.WriteLine($"{name} - {owner}");

    Console.WriteLine(
        $"  Total       : {total}");

    Console.WriteLine(
        $"  With RID    : {withRid}");

    Console.WriteLine(
        $"  Without RID : {withoutRid}");

    foreach (var item in types.OrderByDescending(x => x.Value))
    {
        Console.WriteLine(
            $"    {item.Key,-15} : {item.Value}");
    }
}


// ============================================================
// PLANET CHECK
// ============================================================

static void CheckPlanet(
    string name,
    List<JsonObject> records,
    string ua)
{
    Console.WriteLine();
    Console.WriteLine(
        $"{name} - Planet UA {ua}");

    int found = 0;

    foreach (var record in records)
    {
        string? type =
            GetNestedString(record, "DD", "DT");

        if (!string.Equals(
                type,
                "Planet",
                StringComparison.Ordinal))
        {
            continue;
        }

        string? recordUa =
            GetNestedString(record, "DD", "UA");

        if (!string.Equals(
                recordUa,
                ua,
                StringComparison.Ordinal))
        {
            continue;
        }

        found++;

        Console.WriteLine();
        Console.WriteLine("  FOUND");

        Console.WriteLine(
            $"  RID : {GetString(record, "RID") ?? "<missing>"}");

        Console.WriteLine(
            $"  USN : {GetNestedString(record, "OWS", "USN") ?? "<missing>"}");

        Console.WriteLine(
            $"  UID : {GetNestedString(record, "OWS", "UID") ?? "<missing>"}");

        Console.WriteLine(
            $"  PTK : {GetNestedString(record, "OWS", "PTK") ?? "<missing>"}");

        Console.WriteLine(
            $"  TS  : {GetNestedString(record, "OWS", "TS") ?? "<missing>"}");

        Console.WriteLine();
        Console.WriteLine("  DD:");

        if (record["DD"] is JsonObject dd)
            Console.WriteLine(
                dd.ToJsonString());
    }

    if (found == 0)
        Console.WriteLine("  NOT FOUND");
    else
        Console.WriteLine($"  Matches: {found}");
}


// ============================================================
// LEGACY IDENTITY
// ============================================================

static string? GetLegacyIdentity(
    JsonObject record)
{
    if (record["DD"] is not JsonObject dd)
        return null;

    JsonNode? dt = dd["DT"];
    JsonNode? ua = dd["UA"];

    if (dt == null ||
        ua == null)
    {
        return null;
    }

    string identity =
        dt.ToJsonString() +
        "|" +
        ua.ToJsonString();

    // Most records have VP.
    // SpaceStation and some other records do not.
    if (dd["VP"] != null)
    {
        identity +=
            "|" +
            dd["VP"]!.ToJsonString();
    }

    return identity;
}


// ============================================================
// GET STRING
// ============================================================

static string? GetString(
    JsonObject obj,
    string property)
{
    JsonNode? node = obj[property];

    if (node == null)
        return null;

    if (node is JsonValue value &&
        value.TryGetValue<string>(out string? text))
    {
        return text;
    }

    return node.ToJsonString();
}

// ============================================================
// GET NESTED STRING
// ============================================================

static string? GetNestedString(
    JsonObject obj,
    string parent,
    string property)
{
    if (obj[parent] is not JsonObject child)
        return null;

    return GetString(child, property);
}

// ============================================================
// INCREMENT
// ============================================================

static void Increment(
    Dictionary<string, int> dictionary,
    string key)
{
    if (dictionary.TryGetValue(
            key,
            out int value))
    {
        dictionary[key] = value + 1;
    }
    else
    {
        dictionary[key] = 1;
    }
}