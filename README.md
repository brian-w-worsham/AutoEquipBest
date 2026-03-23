# Auto Equip Best — Bannerlord Mod

Automatically equip the best armor and weapons from your inventory with one click.

## Features

- **Auto-equip button** in the inventory screen (top-right corner)
- **Hotkey: Ctrl+E** as a keyboard shortcut while in inventory
- Scores and equips the best item for each slot:
  - **Armor:** Head, Body, Legs, Gloves, Cape
  - **Horse & Harness**
  - **Weapons:** Picks the top 4 weapons with diversity (prefers different weapon classes)
- Respects item difficulty/skill requirements
- Returns previously equipped items to your inventory

## Prerequisites

- **Mount & Blade II: Bannerlord** (Steam version)
- **.NET Framework 4.7.2 targeting pack** (comes with Visual Studio)
- **Visual Studio 2022** or the **.NET SDK** with `dotnet build` support
- Bannerlord installed at the default Steam path, or update the path in the `.csproj`

## Project Structure

```
AutoEquipBest/
├── AutoEquipBest.sln
├── deploy.ps1                          # Build & deploy script
├── Module/
│   ├── SubModule.xml                   # Bannerlord module definition
│   └── GUI/
│       └── Prefabs/
│           └── AutoEquipBestButton.xml # Gauntlet UI button
└── src/
    └── AutoEquipBest/
        ├── AutoEquipBest.csproj
        ├── SubModule.cs                # Mod entry point (Harmony init)
        ├── AutoEquipLogic.cs           # Core scoring and equip logic
        ├── SPInventoryMixin.cs         # ViewModel mixin for button binding
        └── Patches/
            ├── SPInventoryVMPatch.cs           # Hooks into inventory VM
            ├── GauntletInventoryScreenPatch.cs # Injects button layer
            └── InventoryHotkeyPatch.cs         # Ctrl+E hotkey support
```

## Setup & Build

### 1. Verify your game path

Open `src/AutoEquipBest/AutoEquipBest.csproj` and verify the `<GameFolder>` property
points to your Bannerlord installation:

```xml
<GameFolder>C:\Games\steamapps\common\Mount &amp; Blade II Bannerlord</GameFolder>
```

### 2. Build and deploy

Run from the project root:

```powershell
.\deploy.ps1
```

Or specify a custom game path:

```powershell
.\deploy.ps1 -GameFolder "D:\Games\Mount & Blade II Bannerlord"
```

### 3. Manual build (alternative)

```powershell
dotnet build src\AutoEquipBest\AutoEquipBest.csproj -c Release
```

Then copy the output files to `<GameFolder>\Modules\AutoEquipBest\`.

## How to Use

1. Launch Bannerlord
2. On the launcher, go to **Mods** and enable **"Auto Equip Best"**
3. Load/start a campaign
4. Open your **inventory** (press `I`)
5. Click the **"Auto Equip Best"** button in the top-right, or press **Ctrl+E**
6. Your character will auto-equip the best-scoring items from your party inventory

## How Scoring Works

### Armor

Each armor piece is scored by its primary stat for that slot:

- **Head:** Head armor value
- **Body:** Body armor + 0.5× arm armor + 0.3× leg armor
- **Legs:** Leg armor value
- **Gloves:** Arm armor value
- **Cape:** Body armor + 0.5× arm armor

### Weapons

Weapons are scored by a weighted combination of:

- Swing damage (×1.2), thrust damage (×1.0)
- Swing/thrust speed (×0.3 each)
- Weapon length (×0.2), handling (×0.2)
- Missile speed (×0.5), missile damage (×1.0) for ranged
- Shield: HP + 2× armor rating
- Tier bonus: +10 per tier

The mod picks the **top 4 weapons** while preferring different weapon classes for diversity
(e.g., a sword, a polearm, a bow, and arrows rather than 4 swords).

### Horses

Speed + 0.5× maneuver + 0.3× charge damage.

## Customization

To tweak the scoring weights, edit the `ScoreItem()` and `ScoreWeapon()` methods in
`src/AutoEquipBest/AutoEquipLogic.cs`.

## Troubleshooting

- **Button doesn't appear:** The Gauntlet UI injection depends on the game version. If the
  button doesn't show, use **Ctrl+E** as a fallback.
- **Build errors about missing DLLs:** Make sure `<GameFolder>` in the `.csproj` points to
  a valid Bannerlord installation with the game's DLLs present.
- **Mod not in launcher list:** Ensure the `SubModule.xml` is at
  `<GameFolder>\Modules\AutoEquipBest\SubModule.xml`.
- **Crash on load:** Check Bannerlord's log files in `%APPDATA%\..\ProgramData\Mount and Blade II Bannerlord\logs\`
  for error details.

## Compatibility

- Built for **Bannerlord e1.8+** (should work with most recent versions)
- Uses **Harmony 2.2** for runtime patching
- No save-game modifications — safe to add/remove mid-playthrough

## License

MIT — do whatever you want with it.
