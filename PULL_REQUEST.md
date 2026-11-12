# Gudako-x10 v1.0.5 — Rebuilt for current Risk of Rain 2

## Summary
This PR rebuilds `Gudako-x10.dll` (10x Pickup Mod) from reverse-engineered C# source so it works on the latest version of Risk of Rain 2, and adds new configuration options.

## What's changed

### Compatibility fixes (v1.0.2 – v1.0.4)
- **Rebuilt from source**: the mod was fully reverse-engineered and recreated as a maintainable C# project under `src/GudakoX10/`, targeting netstandard2.1, BepInEx 5.4.x, HarmonyX, and current `RiskOfRain2.GameLibs`.
- **Fixed "Parameter count not found" crash**: the original patch targeted `RoR2.Inventory.GiveItem(ItemIndex, int)`, whose parameter name changed in modern game builds. The multiplier is now applied via a prefix on the real item-flow method (`ItemCollection.Add`), with safe fallbacks for older game versions.
- **Removed hard R2API dependency** that prevented the plugin from loading at all on up-to-date installs.
- **Suppressed spurious fatal error**: the "default forbidden item list is NOT CORRECT" check no longer fires when the ItemCatalog is empty at plugin start (it was a false positive; the check still runs once the catalog is initialized).

### New features & config cleanup (v1.0.5)
- **Rarity filters**: new `[RarityFilter]` section lets you exempt entire rarities from multiplication — Common, Uncommon, Legendary, Lunar, Void, Shredder (Distinguished), and Radioactive (Boss). All default to `false` (everything multiplies, as before). Tier names verified against the live `ItemTier` enum.
- **Cleaner config file**: "Forbidden Item List" and the `[EnableByIndex]` section are no longer bound, so they no longer appear in the generated `.cfg`. Their safety logic still runs internally with built-in defaults (enemy/utility items remain excluded; the four index-only items remain permanently non-multiplied).

## Files changed
| File | Change |
|---|---|
| `Gudako-x10.dll` | Rebuilt binary (v1.0.5) |
| `manifest.json` | Version bumped 1.0.1 → 1.0.5; dependencies updated |
| `src/GudakoX10/Plugin.cs` | Full reconstructed + updated source |
| `src/GudakoX10/GudakoX10.csproj`, `nuget.config` | New buildable project |
| `.gitignore` | Ignore `obj/`, `bin/`, `tools/` scratch files |

## Testing
- Build: `dotnet build -c Release` — 0 warnings, 0 errors.
- Verified in-game: plugin loads, `gudako.X10.cfg` generates with the new layout, and picking up 1 Soldier's Syringe grants 10.
- Log line `Patched ... - pickups will be multiplied.` confirms the correct hook on current game builds.

## Upgrade notes
Delete your old `BepInEx/config/gudako.X10.cfg` so the fresh layout regenerates, and replace `Gudako-x10.dll` in `BepInEx/plugins/` with the new one.
