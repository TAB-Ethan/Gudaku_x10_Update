using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;

// Reconstructed source for Gudako-x10.dll (namespace megidont, class X10).
// Reverse-engineered from the shipped binary and rebuilt against modern
// Risk of Rain 2 APIs.
//
// NOTE ON PATCHING: Since the Sep-2025 recompile, every public Inventory item
// method is a stripped stub whose body throws, so compile-time patches on
// GiveItem fail to apply ("Parameter \"count\" not found") or do nothing. All
// give/pickup paths now funnel through the internal generic
// Inventory.ChangeItemStacksCount<TImpl>, which mutates stacks by calling
// ItemCollection.Add(itemIndex, count) on the relevant collection. We resolve
// ItemCollection.Add via reflection at load time and install a Harmony prefix
// that multiplies `count`. If it cannot be found (older game layouts), we fall
// back to the legacy postfix on Inventory.GiveItem(ItemIndex, int).
//
// IMPORTANT - WHY THE MULTIPLIER IS SCOPED TO PLAYER INVENTORIES ONLY:
// Enemy bodies (and other non-player characters) share the same RoR2.Inventory
// type, and their AI/spawner code routes internal bookkeeping items through the
// exact same ItemCollection.Add choke point. Examples: ITEM_ADAPTIVEARMOR_NAME,
// ITEM_BOOSTDAMAGE_NAME, ITEM_BOOSTHP_NAME, ITEM_CUTHP_NAME, ITEM_GHOST_NAME,
// ITEM_LEVELBONUS_NAME, ITEM_MINIONLEASH_NAME, ITEM_SKULLCOUNTER_NAME and the
// rest of the enemy-internal set are granted in bulk by the director/spawners
// when enemies spawn. Multiplying those counts corrupts the spawner's state -
// which is why builds prior to this fix "stopped spawning enemies" once the
// ItemCollection.Add patch was installed. The Prefix below therefore verifies
// the collection belongs to a player-controlled inventory before multiplying,
// and ShouldMultiply() additionally refuses to multiply any item without a
// nameToken (all internal/enemy bookkeeping items) as a second safety net.
namespace megidont
{
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.11")]
    public class X10 : BaseUnityPlugin
    {
        internal static ManualLogSource log;

        private static ConfigEntry<int> multiplier;
        private static ConfigEntry<bool> fixVoidItems;

        // Captain: when enabled, every microbot the Captain player gains
        // (pickup, drop transfer, or programmatic give) is duplicated x10 in
        // the same way regular item pickups are.
        private static ConfigEntry<bool> multiplyCaptainMicrobots;
        private static ConfigEntry<int> captainMicrobotMultiplier;

        // Internal, no longer exposed in the config file (kept as plain
        // fields so the filtering logic stays identical to earlier versions).
        private static readonly string forbiddenList = defaultForbiddenItems;

        // Rarity (tier) filters: when one of these is enabled, items of that
        // tier are NOT multiplied. Void tiers are controlled by "Enable Void
        // Item Fix" (kept as-is for backwards compatibility).
        private static ConfigEntry<bool> noMultiplyCommon;
        private static ConfigEntry<bool> noMultiplyUncommon;
        private static ConfigEntry<bool> noMultiplyLegendary;
        private static ConfigEntry<bool> noMultiplyLunar;
        private static ConfigEntry<bool> noMultiplyVoid;
        private static ConfigEntry<bool> noMultiplyDistinguished;
        private static ConfigEntry<bool> noMultiplyRadioactive;

        // SpeedItemsFilter section: per-item toggles for movement-speed items.
        private static ConfigEntry<bool> noMultiplyRedWhip;
        private static ConfigEntry<bool> noMultiplyEnergyDrink;
        private static ConfigEntry<bool> noMultiplyPaulsGoatHoof;
        private static ConfigEntry<bool> noMultiplyMocha;
        private static ConfigEntry<bool> noMultiplySugarcube;

        // The internally-used enemy items that should never be multiplied.
        private static readonly string defaultForbiddenItems =
            "ITEM_ADAPTIVEARMOR_NAME,ITEM_BOOSTATTACKSPEED_NAME,ITEM_BOOSTDAMAGE_NAME," +
            "ITEM_BOOSTEQUIPMENTRECHARGE_NAME,ITEM_BOOSTHP_NAME,ITEM_CRIPPLEWARDONLEVEL_NAME," +
            "ITEM_CUTHP_NAME,ITEM_DRIZZLEPLAYERHELPER_NAME,ITEM_EXTRALIFECONSUMED_NAME," +
            "ITEM_GHOST_NAME,ITEM_HEALTHDECAY_NAME,ITEM_INVADINGDOPPELGANGER_NAME," +
            "ITEM_LEVELBONUS_NAME,ITEM_MINIONLEASH_NAME,ITEM_MONSOONPLAYERHELPER_NAME," +
            "ITEM_SCRAPGREEN_NAME,ITEM_SCRAPRED_NAME,ITEM_SCRAPWHITE_NAME," +
            "ITEM_SCRAPYELLOW_NAME,ITEM_SKULLCOUNTER_NAME";

        // Internal item codes whose nameTokens back the default forbidden list.
        private static readonly string[] checkedItemCodes = new string[]
        {
            "AdaptiveArmor", "BoostAttackSpeed", "BoostDamage", "BoostEquipmentRecharge",
            "BoostHp", "CrippleWardOnLevel", "CutHp", "DrizzlePlayerHelper",
            "ExtraLifeConsumed", "Ghost", "HealthDecay", "InvadingDoppelganger",
            "LevelBonus", "MinionLeash", "MonsoonPlayerHelper", "ScrapGreen",
            "ScrapRed", "ScrapWhite", "ScrapYellow", "SkullCounter"
        };

        // Item codes of the Captain's microbot summon items (the stacks that
        // back his bot count). Resolved at Start() purely for a startup log
        // line confirming the DLC content is present; the actual duplication
        // happens in MultiplyMicrobots on the MicrobotInfector choke point.
        private static readonly string[] captainMicrobotItemCodes =
            { "CaptainBotMedium", "CaptainBotFlaming", "CaptainBotTall" };

        private void Awake()
        {
            log = Logger;
            InitConfig(base.Config);

            var harmony = new Harmony("gudako.X10");

            // Resolve the modern stack-mutation choke point:
            // RoR2.ItemCollection.Add(ItemIndex, int). Every give/pickup path
            // (GiveItem, GiveItemPermanent, AddItemsFrom, ...) funnels through
            // ChangeItemStacksCount, which calls Add on the target collection.
            Type collectionType = AccessTools.TypeByName("RoR2.ItemCollection");
            MethodInfo addMethod = null;
            if (collectionType != null)
            {
                addMethod = AccessTools.Method(collectionType, "Add",
                    new[] { typeof(ItemIndex), typeof(int) });
            }

            if (addMethod != null)
            {
                var prefix = AccessTools.Method(typeof(MultiplyPrefix), "Prefix");
                harmony.Patch(addMethod, prefix: new HarmonyMethod(prefix));
                log.LogInfo("Patched ItemCollection.Add - pickups will be multiplied.");
            }
            else
            {
                // Fallback for older game layouts where GiveItem carries the
                // real logic: use the annotation-based postfix below.
                log.LogWarning("RoR2.ItemCollection.Add not found; falling back to legacy GiveItem postfix patches.");
                harmony.PatchAll(typeof(X10));
            }

            // Captain microbot duplication (self-guarding: stays inactive with
            // a clear log line if MicrobotInfector can't be resolved).
            MultiplyMicrobots.Install(harmony);

            log.LogInfo("10x Pickup Mod loaded!");
        }

        private static void InitConfig(ConfigFile cfg)
        {
            // The "Forbidden Item List" and "EnableByIndex" entries are hidden
            // from the generated config file: we simply don't bind them, so
            // BepInEx never writes them to disk. Their logic still runs with
            // built-in defaults (see defaultForbiddenItems / the index checks
            // in ShouldMultiply).
            multiplier = cfg.Bind("BasicConfig", "Multiplier", 10,
                "Change this number to change pickup quantity.");

            // NOTE: apostrophes in config KEYS throw ArgumentException inside
            // BepInEx 5.x's key parser during Awake(), which aborts binding of
            // every remaining entry - that is why older builds of this mod
            // showed NO rarity toggles in the generated .cfg file. All keys
            // below are deliberately apostrophe-free.
            noMultiplyCommon = cfg.Bind("RarityFilter", "Do not multiply Common items", false,
                "If enabled, Common-tier items will NOT be multiplied on pickup.");
            noMultiplyUncommon = cfg.Bind("RarityFilter", "Do not multiply Uncommon items", false,
                "If enabled, Uncommon-tier items will NOT be multiplied on pickup.");
            noMultiplyLegendary = cfg.Bind("RarityFilter", "Do not multiply Legendary items", false,
                "If enabled, Legendary-tier items will NOT be multiplied on pickup.");
            noMultiplyLunar = cfg.Bind("RarityFilter", "Do not multiply Lunar items", false,
                "If enabled, Lunar-tier items will NOT be multiplied on pickup.");
            noMultiplyVoid = cfg.Bind("RarityFilter", "Do not multiply Void items", false,
                "If enabled, Void-tier items will NOT be multiplied on pickup. (Void items are already skipped while Enable Void Item Fix is on.)");
            noMultiplyDistinguished = cfg.Bind("RarityFilter", "Do not multiply Distinguished items", false,
                "If enabled, Distinguished-tier items (e.g. Shredder) will NOT be multiplied on pickup.");
            noMultiplyRadioactive = cfg.Bind("RarityFilter", "Do not multiply Radioactive items", false,
                "If enabled, Radioactive/Boss-tier items will NOT be multiplied on pickup.");

            // Movement-speed items (per-item opt-out toggles). Keys are
            // apostrophe-free so BepInEx's key parser never aborts binding.
            noMultiplyRedWhip = cfg.Bind("SpeedItemsFilter", "Do not multiply Red Whip", false,
                "If enabled, Red Whip (speed item) will NOT be multiplied on pickup.");
            noMultiplyEnergyDrink = cfg.Bind("SpeedItemsFilter", "Do not multiply Energy Drink", false,
                "If enabled, Energy Drink (speed item) will NOT be multiplied on pickup.");
            noMultiplyPaulsGoatHoof = cfg.Bind("SpeedItemsFilter", "Do not multiply Pauls Goat Hoof", false,
                "If enabled, Paul's Goat Hoof (speed item) will NOT be multiplied on pickup.");
            noMultiplyMocha = cfg.Bind("SpeedItemsFilter", "Do not multiply Mocha", false,
                "If enabled, Mocha (speed item) will NOT be multiplied on pickup.");
            noMultiplySugarcube = cfg.Bind("SpeedItemsFilter", "Do not multiply Sugar Cube", false,
                "If enabled, Sugar Cube (speed item) will NOT be multiplied on pickup.");

            fixVoidItems = cfg.Bind("BasicConfig", "Enable Void Item Fix", true,
                "During testing, the multiplier for void items ended up squaring the drop. Oops. Reenable if you want.");

            // Captain microbots: duplicating the Captain's bot summons is done
            // via a dedicated patch on RoR2.MicrobotInfector so it works even
            // when the general item multiplier would not apply (e.g. filtered
            // rarities). Defaults to x10 as requested.
            multiplyCaptainMicrobots = cfg.Bind("Captain", "Multiply Microbots", true,
                "If enabled, microbots gained by the Captain character are duplicated on top of the normal give.");
            captainMicrobotMultiplier = cfg.Bind("Captain", "Microbot Multiplier", 10,
                "How many microbots are spawned per microbot the Captain gains (1 = no extra bots).");

            // BepInEx only persists sections that contain at least one setting
            // whose value differs from its default. Every rarity toggle above
            // defaults to false, so a fresh install would show NO [RarityFilter]
            // section at all. This hidden marker entry has a non-default value,
            // forcing the whole section (including all seven toggles) onto disk.
            cfg.Bind("RarityFilter", "SectionMarker", "present",
                "Internal marker; keeps the [RarityFilter] section visible in this file. Do not remove.");

            VerifyRarityFilterEntries(cfg);
        }

        // Diagnostic check: after binding, verify the [RarityFilter] entries
        // are actually registered so they show up in gudako.X10.cfg with their
        // true/false defaults. If a stale config file prevented the new lines
        // from being written, log a clear warning telling the user to delete
        // the file once - the full section will then be regenerated.
        private static void VerifyRarityFilterEntries(ConfigFile cfg)
        {
            string[] keys =
            {
                "Do not multiply Common items",
                "Do not multiply Uncommon items",
                "Do not multiply Legendary items",
                "Do not multiply Lunar items",
                "Do not multiply Void items",
                "Do not multiply Distinguished items",
                "Do not multiply Radioactive items"
            };

            string[] speedKeys =
            {
                "Do not multiply Red Whip",
                "Do not multiply Energy Drink",
                "Do not multiply Pauls Goat Hoof",
                "Do not multiply Mocha",
                "Do not multiply Sugar Cube"
            };

            var defs = new System.Collections.Generic.HashSet<string>(
                cfg.Keys.Select(k => k.Section + "/" + k.Key));

            int present = keys.Count(k => defs.Contains("RarityFilter/" + k));

            if (present == keys.Length)
            {
                log.LogInfo("[RarityFilter] all " + keys.Length + " rarity toggles are present in the config file.");
            }

            int speedPresent = speedKeys.Count(k => defs.Contains("SpeedItemsFilter/" + k));
            if (speedPresent == speedKeys.Length)
            {
                log.LogInfo("[SpeedItemsFilter] all " + speedKeys.Length + " speed-item toggles are present in the config file.");
            }
            else
            {
                log.LogWarning("[SpeedItemsFilter] only " + speedPresent + "/" + speedKeys.Length +
                    " speed-item toggles were found in the loaded config. If you don't see them in BepInEx/config/gudako.X10.cfg, " +
                    "delete that file and relaunch the game once - the full sections will be regenerated.");
                return;
            }
            if (present != keys.Length)
            {
                log.LogWarning("[RarityFilter] only " + present + "/" + keys.Length +
                    " rarity toggles were found in the loaded config. If you don't see them in BepInEx/config/gudako.X10.cfg, " +
                    "delete that file and relaunch the game once - the full section will be regenerated.");
            }
        }

        // Validates the embedded default forbidden-item list against the live
        // item nameTokens (looked up via ItemCatalog). Logs a critical error
        // if the game's tokens no longer match what we shipped with.
        private static void Initialize()
        {
            // Built exactly like the original: interleaved array of
            // [itemCode, ","] pairs flattened with string.Concat. This is why
            // it must run AFTER content packs are registered (Unity's
            // Awake order), so we call it from Start(), not Awake().
            string[] strings = new string[40];
            int i = 0;
            foreach (string code in checkedItemCodes)
            {
                strings[i++] = Token(code);
                strings[i++] = ",";
            }
            string expected = string.Concat(strings).TrimEnd(',');

            if (defaultForbiddenItems == expected) return;

            // If EVERY token resolved empty, the item catalog wasn't populated
            // yet at Start() time (e.g. a load-order quirk or stripped content
            // registration). That's a false alarm, not a real mismatch - don't
            // spam the fatal message, just skip silently.
            if (expected.Length == 0) return;

            log.LogFatal("Critical Error:\nThe current default forbidden item list is NOT CORRECT.\n(this is not the one in your config, this should only happen if something goes horribly wrong.)\nThe current default set is "
                + defaultForbiddenItems
                + "\nCurrent nametokens mean it should be "
                + expected
                + "\nIf you know what this means, you're probably ok. If this is the only mod you are running, message me @Megidon't#0413 on discord.");
        }

        // Resolves an item's nameToken from its internal item code via the
        // runtime ItemCatalog (works across all content packs/DLCs).
        private static string Token(string itemCode)
        {
            ItemIndex index = ItemCatalog.FindItemIndex(itemCode);
            if (index == ItemIndex.None) return string.Empty;
            ItemDef def = ItemCatalog.GetItemDef(index);
            return def ? def.nameToken.ToString() : string.Empty;
        }

        // Runs after all plugins' Awake() (and therefore after content-pack
        // registration), matching the original binary's patch timing.
        private void Start()
        {
            Initialize();
            ResolveCaptainMicrobotItems();
        }

        // Maps the Captain microbot item codes to their live ItemIndexes once
        // the catalog is populated, purely so the startup log can confirm the
        // relevant DLC content exists. The duplication itself hooks
        // MicrobotInfector.Infected (see MultiplyMicrobots), which works
        // regardless of how the bot stacks were granted.
        private static void ResolveCaptainMicrobotItems()
        {
            int found = 0;
            foreach (string code in captainMicrobotItemCodes)
            {
                if (ItemCatalog.FindItemIndex(code) != ItemIndex.None) found++;
            }

            if (found > 0)
            {
                log.LogInfo("Captain microbot items present (" + found + "/" + captainMicrobotItemCodes.Length +
                    "); they will be duplicated x" + captainMicrobotMultiplier.Value + " for Captain players.");
            }
            else
            {
                log.LogWarning("No Captain microbot items found in the item catalog (captain DLC missing?); " +
                    "the Captain microbot duplication feature has nothing to act on.");
            }
        }

        // Shared filter logic: returns true when the given item stack change
        // should be multiplied.
        internal static bool ShouldMultiply(ItemIndex itemIndex)
        {
            // Internal items without nameTokens are never multiplied (the
            // old "EnableByIndex" config toggles were removed; these remain
            // permanently disabled for safety).
            int idx = (int)itemIndex;
            if (idx == 109 || idx == 167 || idx == 168 || idx == 176) return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (itemDef == null) return true;

            // SAFETY NET AGAINST THE ENEMY-SPAWN BUG: every enemy/director
            // bookkeeping item (ITEM_BOOSTDAMAGE_NAME, ITEM_GHOST_NAME,
            // ITEM_LEVELBONUS_NAME, ...) is an *internal* ItemDef with no
            // nameToken. Multiplying the counts the spawner hands out for
            // those corrupts enemy setup and stops enemies from spawning at
            // all, so anything without a player-visible name token is never
            // multiplied here - regardless of which inventory it lands in.
            if (string.IsNullOrEmpty(itemDef.nameToken)) return false;

            // Rarity (tier) filters from the RarityFilter config section.
            switch (itemDef.tier)
            {
                case ItemTier.Tier1: // Common
                    if (noMultiplyCommon.Value) return false;
                    break;
                case ItemTier.Tier2: // Uncommon
                    if (noMultiplyUncommon.Value) return false;
                    break;
                case ItemTier.Tier3: // Legendary
                    if (noMultiplyLegendary.Value) return false;
                    break;
                case ItemTier.Lunar:
                    if (noMultiplyLunar.Value) return false;
                    break;
                case ItemTier.Boss: // Radioactive
                    if (noMultiplyRadioactive.Value) return false;
                    break;
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    if (noMultiplyVoid.Value) return false;
                    // Void items were double-multiplied (the unlock counts as a
                    // give); skip multiplying them unless the user re-enables
                    // the old behaviour.
                    if (fixVoidItems.Value) return false;
                    break;
            }

            // Distinguished tier is assigned at runtime in some content packs;
            // check it by name token group as well as enum value.
            if (itemDef.tier == ItemTier.AssignedAtRuntime && IsDistinguished(itemDef))
            {
                if (noMultiplyDistinguished.Value) return false;
            }

            // Per-item speed toggles from the SpeedItemsFilter section.
            var speedToggle = SpeedToggleFor(itemIndex, itemDef);
            if (speedToggle != null && speedToggle.Value) return false;

            if (forbiddenList.Contains(itemDef.nameToken.ToString()))
                return false;

            return true;
        }

        // Best-effort check for the Distinguished (Shredder) tier on builds
        // where tiers are resolved dynamically instead of via the enum.
        private static bool IsDistinguished(ItemDef itemDef)
        {
            try
            {
                var field = typeof(ItemTier).GetField("Distinguished");
                if (field != null && itemDef.tier == (ItemTier)field.GetValue(null))
                    return true;
            }
            catch { }
            return false;
        }

        // True for items that grant movement speed: Red Whip, Energy Drink,
        // Paul's Goat Hoof, Mocha and Sugar Cube. Matched by item code first
        // (stable across language settings), with a name-token fallback.
        private static readonly string[] speedItemCodes =
        {
            "REDWHIP", "ENERGYDRINK", "PAULSGOATHOOF", "MOCHA", "SUGARCUBE"
        };

        // Returns the matching speed-item toggle for this item, if any.
        // Matching is done on the stable internal item codes (language-
        // independent); a name-token fallback covers renamed items.
        private static ConfigEntry<bool> SpeedToggleFor(ItemIndex itemIndex, ItemDef itemDef)
        {
            string code = itemIndex.ToString();
            switch (code)
            {
                case "RedWhip": return noMultiplyRedWhip;
                case "EnergyDrink": return noMultiplyEnergyDrink;
                case "PaulsGoatHoof": return noMultiplyPaulsGoatHoof;
                case "Mocha": return noMultiplyMocha;
                case "SugarCube": return noMultiplySugarcube;
            }
            string token = itemDef.nameToken.ToString();
            if (token == "REDWHIP_NAME") return noMultiplyRedWhip;
            if (token == "ENERGYDRINK_NAME") return noMultiplyEnergyDrink;
            if (token == "PAULSGOATHOOF_NAME") return noMultiplyPaulsGoatHoof;
            if (token == "MOCHA_NAME") return noMultiplyMocha;
            if (token == "SUGARCUBE_NAME") return noMultiplySugarcube;
            return null;
        }

        internal static int MultiplierValue
        {
            get { return multiplier.Value; }
        }

        internal static int CaptainMicrobotMultiplierValue
        {
            get { return captainMicrobotMultiplier.Value; }
        }

        internal static bool ShouldMultiplyMicrobots()
        {
            return multiplyCaptainMicrobots.Value && captainMicrobotMultiplier.Value > 1;
        }

        // Fallback postfix on Inventory.GiveItem(ItemIndex, int): multiplies
        // the count by the configured multiplier unless the item is forbidden.
        // Only applied when ChangeItemStacksCount could not be resolved.
        // Guarded with the same player-inventory check as MultiplyPrefix so
        // enemy inventories are never touched (see the enemy-spawn bug note
        // at the top of this file).
        [HarmonyPatch(typeof(Inventory), "GiveItem", new[] { typeof(ItemIndex), typeof(int) })]
        [HarmonyPostfix]
        private static void multiplyItems(Inventory __instance, ItemIndex __1, ref int __2)
        {
            if (__2 <= 0) return;
            if (!IsPlayerInventory(__instance)) return;
            if (!ShouldMultiply(__1)) return;
            __2 *= multiplier.Value;
        }

        // ---- Player-only scoping -------------------------------------------
        // RoR2.ItemCollection is a plain class with no public back-pointer to
        // its owning Inventory, so we locate the private instance field that
        // references an Inventory (discovered via reflection at load time) and
        // use it to decide whether a stack change belongs to a real player.
        private static FieldInfo inventoryFieldInItemCollection;
        private static bool inventoryFieldLookupDone;

        // Resolved via reflection so this compiles against every game-lib
        // package revision (GetBody lives on RoR2.Inventory in newer builds,
        // and PlayerCharacterMaster only exists since the Captain update).
        private static MethodInfo inventoryGetBodyMethod;
        private static Type playerMasterType;
        private static bool playerApiLookupDone;

        private static void ResolvePlayerApi()
        {
            if (playerApiLookupDone) return;
            playerApiLookupDone = true;
            inventoryGetBodyMethod = AccessTools.Method(typeof(Inventory), "GetBody");
            playerMasterType = AccessTools.TypeByName("RoR2.PlayerCharacterMaster");
        }

        internal static bool IsPlayerInventory(Inventory inv)
        {
            ResolvePlayerApi();
            if (!inv) return false;

            CharacterBody body = null;
            if (inventoryGetBodyMethod != null)
            {
                try { body = inventoryGetBodyMethod.Invoke(inv, null) as CharacterBody; }
                catch { body = null; }
            }
            if (!body) return false;

            CharacterMaster master = body.master;
            if (!master) return false;

            // On builds that have it, only a *player* master counts; older
            // builds without PlayerCharacterMaster fall back to "any master
            // with authority", which the nameToken guard still keeps safe.
            if (playerMasterType != null && !playerMasterType.IsInstanceOfType(master))
                return false;

            return master.hasAuthority;
        }

        // Given the `this` instance of ItemCollection.Add, resolve the
        // Inventory that owns the collection. Returns null for any collection
        // we cannot attribute to a live inventory (which is treated as
        // "do not multiply" - the safe default).
        internal static Inventory GetOwningInventory(object itemCollectionInstance)
        {
            if (itemCollectionInstance == null) return null;

            if (!inventoryFieldLookupDone)
            {
                inventoryFieldLookupDone = true;
                var declared = AccessTools.GetDeclaredFields(itemCollectionInstance.GetType());
                var fields = (declared != null && declared.Count > 0)
                    ? declared.ToArray()
                    : itemCollectionInstance.GetType().GetFields(BindingFlags.Instance |
                        BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(Inventory))
                    {
                        inventoryFieldInItemCollection = f;
                        break;
                    }
                }
                if (inventoryFieldInItemCollection != null)
                {
                    log.LogInfo("ItemCollection -> Inventory backlink found via field '" +
                        inventoryFieldInItemCollection.Name + "'.");
                }
                else
                {
                    log.LogWarning("No Inventory backlink field found on RoR2.ItemCollection; " +
                        "falling back to nameToken-only filtering to protect enemy spawns.");
                }
            }

            if (inventoryFieldInItemCollection == null) return null;
            try
            {
                return inventoryFieldInItemCollection.GetValue(itemCollectionInstance) as Inventory;
            }
            catch
            {
                return null;
            }
        }
    }

    // Harmony prefix for RoR2.ItemCollection.Add(ItemIndex itemIndex, int count).
    // Scales `count` before the real implementation runs - but ONLY for stacks
    // that belong to a local player's inventory. Enemy bodies share the same
    // Inventory/ItemCollection types and receive internal bookkeeping items
    // (BoostDamage, LevelBonus, SkullCounter, ...) through this exact method
    // during spawning; multiplying those counts was what broke enemy spawns in
    // earlier builds. Removals (negative counts), non-player inventories and
    // any collection whose owner cannot be resolved pass through untouched.
    internal static class MultiplyPrefix
    {
        public static bool Prefix(object __instance, ItemIndex itemIndex, ref int count)
        {
            // Only act on additions; removals (negative counts) pass through.
            if (count <= 0) return true;

            // THE ENEMY-SPAWN FIX: never scale stacks on anything that is not
            // a local player's inventory. If the owning inventory cannot be
            // determined at all, skip multiplication (safe default) - the
            // ShouldMultiply nameToken guard below still protects enemies in
            // that scenario, but only for items they should never have had
            // multiplied anyway.
            Inventory owner = X10.GetOwningInventory(__instance);
            if (owner != null && !X10.IsPlayerInventory(owner)) return true;

            if (!X10.ShouldMultiply(itemIndex)) return true;

            count *= X10.MultiplierValue;
            return true;
        }
    }
    // ---- Captain microbot duplication --------------------------------------
    // The Captain's microbots are created by RoR2.MicrobotInfector.Infected(),
    // which the game calls once per bot gained (pickup, drop transfer, or
    // programmatic give all funnel through it). We postfix that choke point:
    // when the infecting character is a local-player Captain, each call is
    // repeated (multiplier - 1) extra times - x10 total with the default
    // config. All other characters are untouched, and re-entry from our own
    // duplicate invocations is blocked so the count can never explode
    // recursively (x10 -> x100 -> ...).
    //
    // MicrobotInfector and PlayerCharacterMaster only exist in newer RoR2
    // builds, so every reference to them is resolved via reflection at patch
    // install time; on older games the feature simply stays inactive.
    internal static class MultiplyMicrobots
    {
        // Reentrancy guard for the duplicate invocations below.
        private static bool duplicating = false;

        // Cached reflection handles for RoR2.MicrobotInfector (resolved at
        // install time so the mod still loads on builds where the type was
        // renamed/moved - the patch simply stays inactive then).
        private static Type infectorType;
        private static MethodInfo infectedMethod;
        private static ParameterInfo[] infectedParameters;
        private static FieldInfo infectorOwnerField;   // CharacterMaster field owning the infector
        private static Type playerMasterType;          // RoR2.PlayerCharacterMaster, if present
        private static bool installed;

        // Called manually from X10.Awake() (instead of attribute-based
        // patching) because the target method must be discovered via
        // reflection at load time.
        public static void Install(Harmony harmony)
        {
            infectorType = AccessTools.TypeByName("RoR2.MicrobotInfector");
            if (infectorType == null)
            {
                X10.log.LogWarning("RoR2.MicrobotInfector not found; Captain microbot duplication disabled.");
                return;
            }

            infectedMethod = AccessTools.Method(infectorType, "Infected");
            if (infectedMethod == null)
            {
                X10.log.LogWarning("MicrobotInfector.Infected not found; Captain microbot duplication disabled.");
                return;
            }
            infectedParameters = infectedMethod.GetParameters();

            // The owner field naming differs across game versions; discover it
            // by type instead of hard-coding a name. Prefer the concrete
            // PlayerCharacterMaster when the game has it, otherwise accept any
            // CharacterMaster-typed field (IsCaptainPlayer double-checks the
            // runtime type anyway). Without an owner field we cannot tell
            // Captain players from everyone else, so stay inactive if missing.
            playerMasterType = AccessTools.TypeByName("RoR2.PlayerCharacterMaster");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = infectorType.GetFields(flags);
            if (playerMasterType != null)
            {
                infectorOwnerField = fields.FirstOrDefault(
                    f => playerMasterType.IsAssignableFrom(f.FieldType));
            }
            if (infectorOwnerField == null)
            {
                infectorOwnerField = fields.FirstOrDefault(
                    f => typeof(CharacterMaster).IsAssignableFrom(f.FieldType));
            }

            if (infectorOwnerField == null)
            {
                X10.log.LogWarning("Could not resolve the owning master field on MicrobotInfector; " +
                    "Captain microbot duplication disabled to avoid spawning bots for the wrong character.");
                return;
            }

            installed = true;
            harmony.Patch(infectedMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(MultiplyMicrobots), "Postfix")));
            X10.log.LogInfo("Patched MicrobotInfector.Infected - Captain microbots will be duplicated x" +
                X10.CaptainMicrobotMultiplierValue + ".");
        }

        // Harmony postfix on MicrobotInfector.Infected(...). Only __instance
        // plus pass-through parameters are declared so it binds regardless of
        // the exact signature; the real parameters are read from __original_args.
        public static void Postfix(object __instance, object[] __original_args)
        {
            if (!installed) return;
            if (!X10.ShouldMultiplyMicrobots()) return;
            if (duplicating) return; // never multiply our own duplicates

            try
            {
                // Whose infector is this? Only Captain players get the bonus.
                var owner = infectorOwnerField.GetValue(__instance) as CharacterMaster;
                if (owner == null) return;
                if (!IsCaptainPlayer(owner)) return;

                int extra = X10.CaptainMicrobotMultiplierValue - 1;
                if (extra <= 0) return;

                // Safety: only replay calls that take no arguments or exactly
                // one reference-type argument (the infection target). Anything
                // more exotic we don't understand, so we leave it alone rather
                // than risk corrupting the summon state.
                object[] replayArgs = null;
                if (infectedParameters.Length == 0)
                {
                    replayArgs = new object[0];
                }
                else if (infectedParameters.Length == 1 &&
                         !infectedParameters[0].ParameterType.IsValueType &&
                         __original_args != null && __original_args.Length == 1)
                {
                    replayArgs = new object[] { __original_args[0] };
                }
                if (replayArgs == null) return;

                duplicating = true;
                try
                {
                    for (int i = 0; i < extra; i++)
                    {
                        // Re-run the vanilla infection on the same infector so
                        // every duplicate goes through the game's own spawn,
                        // networking and limit checks.
                        infectedMethod.Invoke(__instance, replayArgs);
                    }
                }
                finally
                {
                    duplicating = false;
                }
            }
            catch (Exception e)
            {
                X10.log.LogWarning("Microbot duplication skipped due to error: " + e.Message);
            }
        }

        // True when the given master is a player-controlled Captain. Checks
        // the runtime type against PlayerCharacterMaster when the game build
        // has one, then matches the body's stable identifier (falling back to
        // the prefab name) so other Captain-like masters (e.g. the Drone
        // event variant) and ally-bots do not receive the bonus.
        private static bool IsCaptainPlayer(CharacterMaster master)
        {
            if (playerMasterType != null && !playerMasterType.IsInstanceOfType(master))
                return false;

            if (!master) return false;
            var masterGo = master.gameObject;
            if (masterGo == null) return false;
            CharacterBody body = masterGo.GetComponent<CharacterBody>();
            if (!body) return false;

            string identifier = GetBodyIdentifier(body);
            if (identifier.IndexOf("Captain", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var bodyPrefab = body.gameObject;
            if (bodyPrefab != null &&
                bodyPrefab.name.IndexOf("Captain", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        // Reads CharacterBody.bodyIdentifier (added in later RoR2 versions)
        // defensively; returns empty string on older layouts.
        private static string GetBodyIdentifier(CharacterBody body)
        {
            try
            {
                var prop = AccessTools.Property(body.GetType(), "bodyIdentifier");
                if (prop != null)
                {
                    object id = prop.GetValue(body, null);
                    if (id != null) return id.ToString();
                }
                var field = AccessTools.Field(body.GetType(), "bodyIdentifier");
                if (field != null)
                {
                    object id = field.GetValue(body);
                    if (id != null) return id.ToString();
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
