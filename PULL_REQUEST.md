# Rebuild Gudako-x10 v1.0.10 for current Risk of Rain 2

## Summary
Reverse-engineered and rebuilt the "10x Pickup Mod" (Gudako-x10.dll) so it works on the latest RoR2 with BepInEx 5.4.x. This PR supersedes the earlier 1.0.9 transpiler approach, which silently failed to multiply pickups.

## What's in this build (v1.0.10)
- **Working multiplication**: reverts to the proven 1.0.4 patch point — a Harmony prefix on `RoR2.ItemCollection.Add(ItemIndex, int)`, the real choke point every pickup/give path flows through. In current RoR2 builds the public `Inventory.Give*` methods compile to stubs (`ldnull; throw`), which is why patching them never worked.
- **Rarity filters**: new `[RarityFilter]` section with 7 true/false toggles (Common, Uncommon, Legendary, Lunar, Void, Distinguished, Radioactive). Default `false` = behaviour unchanged. Keys use apostrophe-free wording so BepInEx config binding doesn't abort.
- **Cleaner config**: Forbidden Item List and EnableByIndex entries are hidden from the generated .cfg; their safety logic still runs internally with built-in defaults.
- **No R2API dependency**, no false-positive fatal error on load.

## Testing
Picking up 1 Soldier's Syringe gives 10. On launch the log should show:
`Patched ItemCollection.Add - pickups will be multiplied.`

## Upgrade notes
Delete any stale `BepInEx/config/gudako.X10.cfg` so the fresh layout regenerates, then replace `Gudako-x10.dll` in `BepInEx/plugins/`.

## Files changed
- `Gudako-x10.dll` (rebuilt, v1.0.10)
- `manifest.json` (version bump)
- `src/GudakoX10/Plugin.cs` + `.csproj` (reconstructed source)
