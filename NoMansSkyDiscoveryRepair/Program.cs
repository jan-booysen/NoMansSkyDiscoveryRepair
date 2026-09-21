using System.Text.Json;
using System.Text.Json.Nodes;

Console.WriteLine("==============================================");
Console.WriteLine(" NMS Discovery Repair");
Console.WriteLine("==============================================");
Console.WriteLine();


string inputFileName = "temp.json";
string folderPath = Environment.CurrentDirectory;
string input = Path.Combine(folderPath, inputFileName);

string defaultOutput = Path.Combine(
    folderPath,
    Path.GetFileNameWithoutExtension(input) + "_repaired.json");


if (!File.Exists(input))
{
    Console.WriteLine($"ERROR: File not found: {Path.GetFullPath(input)}");
    return 1;
}

JsonNode? root;
try
{
    root = JsonNode.Parse(await File.ReadAllTextAsync(input));
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR parsing JSON: {ex.Message}");
    return 1;
}

if (root is not JsonObject rootObject)
{
    Console.WriteLine("ERROR: JSON root is not an object.");
    return 1;
}

JsonObject? discoveryData =
    rootObject["DiscoveryData-v1"] as JsonObject
    ?? rootObject["DiscoveryManagerData"]?["DiscoveryData-v1"] as JsonObject;

if (discoveryData is null)
{
    Console.WriteLine("ERROR: DiscoveryData-v1 not found.");
    return 1;
}

if (discoveryData["Store"] is not JsonObject store ||
    store["Record"] is not JsonArray records)
{
    Console.WriteLine("ERROR: DiscoveryData-v1.Store.Record not found.");
    return 1;
}

Console.WriteLine($"Records found: {records.Count:N0}");
Console.WriteLine();

var owners = new Dictionary<string, OwnerStats>(StringComparer.Ordinal);

foreach (JsonNode? node in records)
{
    if (node is not JsonObject record) continue;

    JsonObject? ows = record["OWS"] as JsonObject;
    string uid = ows?["UID"]?.GetValue<string>() ?? "";
    string usn = ows?["USN"]?.GetValue<string>() ?? "(no name)";
    string type = (record["DD"] as JsonObject)?["DT"]?.GetValue<string>() ?? "(unknown)";

    string key = string.IsNullOrEmpty(uid) ? $"NAME:{usn}" : $"UID:{uid}";

    if (!owners.TryGetValue(key, out OwnerStats? stats))
    {
        stats = new OwnerStats(uid, usn);
        owners[key] = stats;
    }

    stats.Total++;
    stats.Types.TryGetValue(type, out int count);
    stats.Types[type] = count + 1;
}

Console.WriteLine("Discovery owners");
Console.WriteLine("----------------------------------------------");

var ownerList = owners.Values.OrderByDescending(x => x.Total).ToList();

for (int i = 0; i < ownerList.Count; i++)
{
    var o = ownerList[i];
    Console.WriteLine($"{i + 1,3}. {o.Name,-28} {o.Total,6:N0}  UID: {o.Uid}");
}

if (ownerList.Count == 0)
{
    Console.WriteLine("No usable discovery owners found.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("Select the owner whose discoveries should be KEPT.");
Console.WriteLine("Identity uses UID when available; display name is only shown for reference.");
Console.WriteLine();

int selection = AskNumber("Owner number", 1, ownerList.Count);
OwnerStats selected = ownerList[selection - 1];

Console.WriteLine();
Console.WriteLine($"Selected: {selected.Name}");
Console.WriteLine($"UID     : {(string.IsNullOrEmpty(selected.Uid) ? "(none)" : selected.Uid)}");
Console.WriteLine($"Records : {selected.Total:N0}");
Console.WriteLine();

Console.WriteLine("Preserved: Available, Enqueued, ReserveManaged, and all other JSON data.");
Console.WriteLine($"Changed : Store.Record {records.Count:N0} -> {selected.Total:N0}");
Console.WriteLine($"Changed : ReserveStore -> {selected.Total:N0}");
Console.WriteLine();


if (File.Exists(defaultOutput))
{
    Console.Write("Output exists. Overwrite it? [y/N]: ");
    if (!string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
        return 0;
}

Console.WriteLine();
Console.Write("Type REPAIR to continue: ");
if (!string.Equals(Console.ReadLine(), "REPAIR", StringComparison.Ordinal))
{
    Console.WriteLine("Cancelled. Nothing was written.");
    return 0;
}

string fullInput = Path.GetFullPath(input);
string directory = Path.GetDirectoryName(fullInput)!;
string stem = Path.GetFileNameWithoutExtension(input);
string ext = Path.GetExtension(input);
string backup = Path.Combine(directory, $"{stem}_backup{ext}");

try
{
    File.Copy(fullInput, backup, overwrite: false);
    Console.WriteLine($"Backup created: {backup}");
}
catch (IOException)
{
    Console.WriteLine($"Backup already exists: {backup}");
    Console.Write("Continue without replacing it? [y/N]: ");
    if (!string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
        return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR creating backup: {ex.Message}");
    return 1;
}

var filtered = new JsonArray();

foreach (JsonNode? node in records)
{
    if (node is not JsonObject record) continue;

    JsonObject? ows = record["OWS"] as JsonObject;
    string uid = ows?["UID"]?.GetValue<string>() ?? "";
    string usn = ows?["USN"]?.GetValue<string>() ?? "";

    bool keep = !string.IsNullOrEmpty(selected.Uid)
        ? string.Equals(uid, selected.Uid, StringComparison.Ordinal)
        : string.Equals(usn, selected.Name, StringComparison.Ordinal);

    if (keep)
        filtered.Add(node.DeepClone());
}

if (filtered.Count != selected.Total)
{
    Console.WriteLine($"ERROR: Expected {selected.Total} records but selected {filtered.Count}.");
    return 1;
}

store["Record"] = filtered;
discoveryData["ReserveStore"] = filtered.Count;
discoveryData["ReserveManaged"] = 3250;

var options = new JsonSerializerOptions { WriteIndented = true };

try
{
    await File.WriteAllTextAsync(defaultOutput, root.ToJsonString(options));
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR writing repaired JSON: {ex.Message}");
    return 1;
}

Console.WriteLine();
Console.WriteLine("==============================================");
Console.WriteLine(" REPAIR COMPLETE");
Console.WriteLine("==============================================");
Console.WriteLine($"Original records : {records.Count:N0}");
Console.WriteLine($"Kept records     : {filtered.Count:N0}");
Console.WriteLine($"Removed records  : {records.Count - filtered.Count:N0}");
Console.WriteLine($"Backup            : {backup}");
Console.WriteLine($"Repaired JSON     : {Path.GetFullPath(defaultOutput)}");
Console.WriteLine();
Console.WriteLine("Import the repaired JSON with your save editor.");
Console.WriteLine("Keep your original No Man's Sky save backed up until verified.");
return 0;

static string Ask(string prompt, string defaultValue)
{
    Console.Write($"{prompt} [{defaultValue}]: ");
    string? value = Console.ReadLine();
    return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}

static int AskNumber(string prompt, int min, int max)
{
    while (true)
    {
        Console.Write($"{prompt} [{min}-{max}]: ");
        if (int.TryParse(Console.ReadLine(), out int value) && value >= min && value <= max)
            return value;
        Console.WriteLine("Invalid selection.");
    }
}

sealed class OwnerStats(string uid, string name)
{
    public string Uid { get; } = uid;
    public string Name { get; } = name;
    public int Total { get; set; }
    public Dictionary<string, int> Types { get; } = new(StringComparer.OrdinalIgnoreCase);
}
