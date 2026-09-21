# No Man's Sky - Discovery Log Fixer (.NET 8)

A lightweight C# console application designed to fix the infinite "First Contact" scanning loop bug introduced in the **No Man's Sky Cosmos Update**.

## The Problem

Following the **No Man's Sky Cosmos Update**, some long-running saves can experience a discovery persistence problem.

Previously discovered planets, fauna, flora, minerals, and other discoveries may temporarily appear correctly and then revert to **Unknown** or **First Contact** after leaving the discovery or returning to it later.

The problem appears to affect the local `DiscoveryManagerData` stored in the save. The exact cause of the problem has **not yet been established**.

Testing performed during development of this tool showed that the problem is not simply caused by the presence of other players' usernames or by special characters in those usernames.

The Repair tool provides a workaround by rebuilding the local discovery record using the player's own discovery entries while preserving the rest of the exported `DiscoveryManagerData` structure.

**This is a workaround, not a confirmed fix for the underlying game bug.**

## How This Fix Works
This tool surgically parses your active `DiscoveryManagerData` structure, maps it strictly against your own account data, and **strips away foreign player entries containing the formatting landmines**. Your active logs will safely reset, allowing you to scan and record planets normally again.

⚠️ **IMPORTANT NOTE:** Running this fix will safely repair your save file architecture, but it will clear out your local history of other players' discoveries. Your own uploaded galactic footprint on the Hello Games servers remains completely safe.

---

## Step-by-Step Instructions

### 1. Create a Backup
Before touching your save files, always create a fallback safety net:
* Navigate to your NMS save folder: `%appdata%\HelloGames\NMS\st_[your_unique_steam_id]\`
* Right-click your active folder, choose **Compress to ZIP file**, and keep it safe on your desktop.

### 2. Export the Clogged Data
1. Download and open the latest version of **Nomnom Save Editor**.
2. Select your most recent save file (**Last Save**).
3. On the left-hand menu under **Save**, click **Edit JSON (Advanced)**.
4. In the new JSON window, look at the left sidebar and click on **DiscoveryManagerData**.
5. Click inside the data field, press `ALT + A` to select everything, copy it, and paste it into a blank text file named `temp.json`.

If you use No Man's Sky Save Editor (NMSE) instead of NomNom:
1. Open your save in NMSE.
2. Go to Edit raw JSON.
3. Find DiscoveryManagerData.
4. Export/copy the contents to temp.json.
5. Run NMSDiscoveryRepair.
6. Import the resulting temp_repaired.json back into the same DiscoveryManagerData node.
7. Save the game.

### 3. Run the Repair Tool
1. Download the `NMS_Discovery_Repair_v1.0.zip` package from the **Releases** section of this repository.
2. Extract the application files into the **same folder** where you saved your `temp.json` file.
3. Run the executable console app and follow the on-screen prompts.
4. The tool will display a list of all Usernames and Steam IDs found in the file. **Select your profile name/Steam ID** and hit `Enter`.
5. The application will instantly output a repaired file named `temp_repaired.json`.

### 4. Inject and Save
1. Open `temp_repaired.json`, select all of its contents, and copy them.
2. Go back to your open Nomnom JSON window, overwrite the existing **DiscoveryManagerData** content completely with your clean data.
3. In Nomnom, go to the top **File** menu and click **Save**.
4. Launch No Man's Sky. When Steam detects the file change, it will prompt you whether to use the Local file or Cloud file—**select Local File** to force Steam to overwrite its broken cloud cache.

Your scanning and exploration logs will now permanently track and hold your discoveries again!




## NMS Discovery Merge

The **NMS Discovery Merge** tool is intended for players who previously used the Discovery Repair tool and want to restore their historical discovery records after Hello Games has fixed the underlying discovery persistence problem.

### ⚠️ IMPORTANT — Do Not Use This During the Current Bug

The Merge tool **does not fix the current No Man's Sky discovery bug**.

If the game is still affected by the discovery persistence problem, adding the historical discovery records back may cause the problem to return.

**Only use the Merge tool after a game update has fixed the underlying discovery problem.**

### What It Does

The Merge tool combines:

- Your historical discovery database (`_backup.json`)
- Your current working discovery database (`new.json`)

It creates:

```text
merged.json
````

The tool is designed to:

* Preserve your current discoveries.
* Restore historical discoveries from the backup.
* Match records by RID where available.
* Use the legacy discovery identity when a RID is unavailable.
* Preserve original discovery timestamps.
* Preserve original discoverer names.
* Preserve existing RIDs.
* Preserve `Available` and `Enqueued` discovery data.
* Avoid creating or inventing RIDs.
* Avoid modifying the original `_backup.json` or `new.json` files.
* Create a backup of the current discovery data before producing the merged file.

### Files

The Merge tool expects the following files in the same directory:

```text
_backup.json
new.json
```

Where:

**`_backup.json`**

Your original historical discovery data saved before using the Repair tool.

**`new.json`**

Your current working discovery data from the repaired save.

The tool produces:

```text
merged.json
```

and creates a backup of the current data:

```text
new_merge_backup.json
```

### Preparing the Files

Both files must contain the `DiscoveryManagerData` exported from your No Man's Sky save.

#### `_backup.json`

This should be your **original historical discovery data**, saved before using the Repair tool.

#### `new.json`

This should be the **current working discovery data** from your repaired save.

To export the current discovery data:

1. Open your save in **NomNom**.
2. Select your latest save (**Last Save**).
3. Go to **Save → Edit JSON (Advanced)**.
4. Select **DiscoveryManagerData**.
5. Click inside the data field and press `ALT + A`.
6. Copy the contents into a file named:

```text
new.json
````

Place `new.json` and `_backup.json` in the same folder as `NMSDiscoveryMerge.exe`.

Then your folder looks like:

```text
NMSDiscoveryMerge
├── NMSDiscoveryMerge.exe
├── _backup.json
└── new.json
````

And the tool produces:

```text
merged.json
new_merge_backup.json
```
### Merge Process

```text
Historical discovery data
        _backup.json
              │
              │
              ├──────────────┐
              │              │
              ▼              ▼
                         new.json
                    Current working data
              │              │
              └──────┬───────┘
                     ▼
              NMS Discovery Merge
                     │
                     ▼
                merged.json
```

The original source files are not modified.

### Recommended Safety Procedure

Before using the merged file:

1. Make a complete backup of your current No Man's Sky save.
2. Keep `_backup.json` somewhere safe.
3. Keep `new.json` somewhere safe.
4. Run the Merge tool.
5. Review the diagnostic output.
6. Only then import `merged.json` into `DiscoveryManagerData`.
7. Save the game.
8. Keep your backup until you have confirmed that your historical discoveries are working correctly.

### Important

The Merge tool is a **data preservation and restoration utility**. It does not repair or alter the No Man's Sky discovery system itself.

The exact cause of the discovery persistence problem is not established by this project. The Merge tool therefore deliberately avoids modifying historical discovery data unnecessarily.
