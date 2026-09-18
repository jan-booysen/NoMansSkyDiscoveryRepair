# No Man's Sky - Discovery Log Fixer (.NET 8)

A lightweight C# console application designed to fix the infinite "First Contact" scanning loop bug introduced in the **No Man's Sky Cosmos Update**.

## The Problem
When exploring heavily populated areas or community hubs (like Expedition systems), your game cache records other players' Steam usernames. If a username contains **invalid, unescaped, or special characters**, the game client's local JSON database serialization breaks. 

While your primary game state (inventory, ships, bases) saves perfectly, the `DiscoveryManagerData` string array gets permanently locked in a silent overflow loop. Your local scan memory resets every few seconds—causing planets to stay "Unknown" and making it impossible to permanently record or return to new Paradise planets.

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
