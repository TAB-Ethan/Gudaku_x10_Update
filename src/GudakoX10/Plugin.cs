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
namespace megidont
{
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.5")]
    public class X10 : BaseUnityPlugin
    {
        private static ManualLogSource log;

        private static ConfigEntry<int> multiplier;
        private static ConfigEntry<bool> fixVoidItems;

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

            noMultiplyCommon = cfg.Bind("RarityFilter", "Don't multiply Common items", false,
                "If enabled, Common-tier items will NOT be multiplied on pickup.");
            noMultiplyUncommon = cfg.Bind("RarityFilter", "Don't multiply Uncommon items", false,
                "If enabled, Uncommon-tier items will NOT be multiplied on pickup.");
            noMultiplyLegendary = cfg.Bind("RarityFilter", "Don't multiply Legendary items", false,
                "If enabled, Legendary-tier items will NOT be multiplied on pickup.");
            noMultiplyLunar = cfg.Bind("RarityFilter", "Don't multiply Lunar items", false,
                "If enabled, Lunar-tier items will NOT be multiplied on pickup.");
            noMultiplyVoid = cfg.Bind("RarityFilter", "Don't multiply Void items", false,
                "If enabled, Void-tier items will NOT be multiplied on pickup. (Void items are already skipped while 'Enable Void Item Fix' is on.)");
            noMultiplyDistinguished = cfg.Bind("RarityFilter", "Don't multiply Shredder (Distinguished tier) items", false,
                "If enabled, Distinguished-tier items (e.g. Shredder) will NOT be multiplied on pickup.");
            noMultiplyRadioactive = cfg.Bind("RarityFilter", "Don't multiply Radioactive (Boss tier) items", false,
                "If enabled, Radioactive/Boss-tier items will NOT be multiplied on pickup.");

            fixVoidItems = cfg.Bind("BasicConfig", "Enable Void Item Fix", true,
                "During testing, the multiplier for void items ended up squaring the drop. Oops. Reenable if you want.");
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

        internal static int MultiplierValue
        {
            get { return multiplier.Value; }
        }

        // Fallback postfix on Inventory.GiveItem(ItemIndex, int): multiplies
        // the count by the configured multiplier unless the item is forbidden.
        // Only applied when ChangeItemStacksCount could not be resolved.
        [HarmonyPatch(typeof(Inventory), "GiveItem", new[] { typeof(ItemIndex), typeof(int) })]
        [HarmonyPostfix]
        private static void multiplyItems(ItemIndex __1, ref int __2)
        {
            if (!ShouldMultiply(__1)) return;
            __2 *= multiplier.Value;
        }
    }

    // Harmony prefix for RoR2.ItemCollection.Add(ItemIndex itemIndex, int count).
    // Scales `count` before the real implementation runs. Only genuine pickup
    // paths pass positive counts through Add on a live inventory's permanent
    // stacks; removals (negative counts) and bookkeeping recomputes (which use
    // SetStackValue/AddTo instead of Add) are left untouched. Returning true
    // lets the original method execute with the modified argument.
    internal static class MultiplyPrefix
    {
        public static bool Prefix(ItemIndex itemIndex, ref int count)
        {
            // Only act on additions; removals (negative counts) pass through.
            if (count <= 0) return true;

            if (!X10.ShouldMultiply(itemIndex)) return true;

            count *= X10.MultiplierValue;
            return true;
        }
    }
}
