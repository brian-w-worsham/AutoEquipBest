# AutoEquipBest — Copilot Instructions

## Project Overview

Bannerlord mod that auto-equips the best armor and weapons from a player's inventory. Built with Harmony runtime patching against the TaleWorlds game SDK.

## Tech Stack

- **Language:** C# 9.0 targeting .NET Framework 4.7.2
- **Game SDK:** TaleWorlds Mount & Blade II: Bannerlord (TaleWorlds.Core, TaleWorlds.CampaignSystem, TaleWorlds.Library, TaleWorlds.Engine.GauntletUI)
- **Patching:** Harmony 2.2.2 for runtime method patching
- **Testing:** xUnit 2.6.6 with `TestItemFactory` for constructing game objects via reflection
- **Nullable:** Disabled project-wide

## Build & Test Commands

```powershell
# Build
dotnet build src\AutoEquipBest\AutoEquipBest.csproj -c Release

# Run tests
dotnet test tests\AutoEquipBest.Tests\AutoEquipBest.Tests.csproj

# Deploy to game
./deploy.ps1
```

## Architecture

| File | Role |
|------|------|
| `SubModule.cs` | Mod entry point — applies Harmony patches, ticks hotkey listener |
| `AutoEquipLogic.cs` | Core scoring and equip logic (static, testable) |
| `SPInventoryMixin.cs` | ViewModel mixin binding auto-equip command to inventory UI |
| `Patches/SPInventoryVMPatch.cs` | Harmony postfix for inventory VM — creates mixin, adds button overlay |
| `Patches/InventoryHotkeyPatch.cs` | Ctrl+E hotkey listener and cleanup on inventory close |
| `Patches/GauntletInventoryScreenPatch.cs` | Injects button layer into Gauntlet UI |

### Key Design Decisions

- **Two equip paths:** `EquipBestItems` (direct equipment modification for tests) and `EquipBestItemsViaInventory` (transfer commands through `InventoryLogic` for live UI).
- **`EquipmentElement` over `ItemObject`:** Always use `EquipmentElement` modified stats instead of base `ItemObject` component fields. For armor: `GetModifiedBodyArmor()`, `GetModifiedLegArmor()`, `GetModifiedArmArmor()`, etc. For weapons: `GetModifiedSwingDamageForUsage(0)`, `GetModifiedThrustDamageForUsage(0)`, `GetModifiedSwingSpeedForUsage(0)`, `GetModifiedThrustSpeedForUsage(0)`, `GetModifiedHandlingForUsage(0)`, `GetModifiedMissileSpeedForUsage(0)`, `GetModifiedMissileDamageForUsage(0)`, `GetModifiedMaximumHitPointsForUsage(0)`. For weapon length, use `WeaponComponentData.WeaponLength` directly (no modifier API). These modified methods account for in-game item modifiers (e.g. "Rusty", "Fine").
- **Reflection access** in `SPInventoryMixin` and `TestItemFactory` is intentional — the game SDK does not expose needed fields publicly. These are read-only, null-guarded, and wrapped in try-catch.

## Code Conventions

- **Namespace:** `AutoEquipBest` at root, `AutoEquipBest.Patches` for Harmony patches
- **XML documentation:** All public and internal methods must have `<summary>`, `<param>`, and `<returns>` XML doc comments
- **Scoring methods:** `ScoreItem` for armor/non-weapon slots, `ScoreWeapon` for weapons/shields. Body armor uses a dedicated `IsBodyArmorBetter` comparison with tie-break rules (total armor → lower weight → higher tier → higher price)
- **Weight penalty:** All scoring formulas (armor and weapons) subtract `element.Weight * 0.5f` so lighter items score higher. Horses and generic goods are excluded. This applies to `ScoreItem` (all armor types) and `ScoreWeapon` (all weapon branches including One Handed Polearm, Shield, and general weapons).
- **Weapon-class matching:** Phase 1 weapon upgrades match by `WeaponClass` (e.g. `OneHandedSword`, `OneHandedAxe`), not by `ItemTypeEnum`. A sword will only be replaced by a better sword, never by an axe, even if both are `OneHandedWeapon`. Phase 2 fills empty slots with the best available weapon of any class.
- **Weapon-type-specific scoring:** `ScoreWeapon` branches by weapon class. One Handed Polearms use a thrust-only formula (ThrustDamage × 1.0, ThrustSpeed × 0.75, WeaponLength × 0.6, Handling × 0.5, Tier × 0.1) — no swing or missile stats. Shields use hit points + armor. All other weapons use the general formula with swing, thrust, missile, and tier components.
- **Test items:** Use `TestItemFactory` helpers (`CreateArmorItem`, `CreateWeaponItem`, `CreateSimpleItem`, `ToElement`, `ToElementWithModifier`, `CreateModifier`) — never construct game objects directly in tests
- **Analyzer compliance:** Keep methods below cognitive complexity thresholds; extract helpers when needed

## Post-Change Workflow

After making any code changes, always follow these steps in order:

1. **Build:** `dotnet build src\AutoEquipBest\AutoEquipBest.csproj -c Release`
2. **Test:** `dotnet test tests\AutoEquipBest.Tests\AutoEquipBest.Tests.csproj` — confirm all tests pass before proceeding
3. **Deploy:** `./deploy.ps1` — copies the built DLL to the game's module folder

Do not deploy if the build fails or any tests are failing.

## Testing Guidelines

- Tests use `InternalsVisibleTo` to access `internal` methods
- `TestItemFactory` uses reflection to set read-only game properties — the `TierfOverride` is set to `0.1f` by default
- Prefer `Assert.True(score >= X)` over `Assert.Equal(exactValue)` for scoring tests to tolerate formula tuning
- Game APIs like `Tier` may throw outside the game runtime — scoring methods catch and fallback gracefully
