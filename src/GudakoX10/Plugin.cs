using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.8")]
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

            // Patch every public item-give entry point with a Harmony PREFIX
            // that scales the count argument. We deliberately do NOT patch
            // low-level internals like ItemCollection.Add or
            // Inventory.ChangeItemStacksCount: those are also used by
            // bookkeeping paths (Shrine Restack, temporary/rented decay
            // recomputes, inventory cloning), so multiplying there corrupts
            // or resets stacks - which is what broke v1.0.3-1.0.5.
            //
            // The give methods form a tree rooted at GiveItem(ItemIndex,int);
            // e.g. GiveItem(itemDef,count) -> GiveItem(index,count) and
            // GiveItemChanneled/Rented internally call GiveItemPermanent.
            // MultiplyOnce guards against double-scaling when an outer method
            // has already multiplied the count before delegating inward.
            bool patchedAny = false;
            patchedAny |= TryPatchGive(harmony, "GiveItem", new[] { typeof(ItemIndex), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItem", new[] { typeof(ItemIndex), typeof(int), typeof(bool) });
            patchedAny |= TryPatchGive(harmony, "GiveItem", new[] { typeof(ItemDef), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItemPermanent", new[] { typeof(ItemIndex), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItemPermanent", new[] { typeof(ItemDef), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItemChanneled", new[] { typeof(ItemIndex), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItemTemp", new[] { typeof(ItemIndex), typeof(int) });
            patchedAny |= TryPatchGive(harmony, "GiveItemRented", new[] { typeof(ItemIndex), typeof(int) });

            if (!patchedAny)
            {
                // Last-resort fallback for very old game layouts where only
                // the postfix hook worked.
                log.LogWarning("No GiveItem overloads found; falling back to legacy postfix patch.");
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

            noMultiplyCommon = cfg.Bind("RarityFilter", "Do not multiply Common items", false,
                "If enabled, Common-tier items will NOT be multiplied on pickup.");
            noMultiplyUncommon = cfg.Bind("RarityFilter", "Do not multiply Uncommon items", false,
                "If enabled, Uncommon-tier items will NOT be multiplied on pickup.");
            noMultiplyLegendary = cfg.Bind("RarityFilter", "Do not multiply Legendary items", false,
                "If enabled, Legendary-tier items will NOT be multiplied on pickup.");
            noMultiplyLunar = cfg.Bind("RarityFilter", "Do not multiply Lunar items", false,
                "If enabled, Lunar-tier items will NOT be multiplied on pickup.");
            noMultiplyVoid = cfg.Bind("RarityFilter", "Do not multiply Void items", false,
                "If enabled, Void-tier items will NOT be multiplied on pickup. (Void items are already skipped while 'Enable Void Item Fix' is on.)");
            noMultiplyDistinguished = cfg.Bind("RarityFilter", "Do not multiply Distinguished items", false,
                "If enabled, Distinguished-tier items (e.g. Shredder) will NOT be multiplied on pickup.");
            noMultiplyRadioactive = cfg.Bind("RarityFilter", "Do not multiply Radioactive items", false,
                "If enabled, Radioactive/Boss-tier items will NOT be multiplied on pickup.");

            fixVoidItems = cfg.Bind("BasicConfig", "Enable Void Item Fix", true,
                "During testing, the multiplier for void items ended up squaring the drop. Oops. Reenable if you want.");

            // BepInEx only writes config sections that contain at least one
            // setting whose value differs from its default. Since every
            // rarity toggle defaults to false ("multiply everything"), a
            // fresh install would show NO [RarityFilter] section at all and
            // the user would never see the true/false options. We add a
            // hidden marker entry with a deliberately non-default value so
            // the whole section - including all seven toggles - is always
            // persisted to disk on first launch.
            cfg.Bind("RarityFilter", "SectionMarker", "present",
                "Internal marker; keeps the [RarityFilter] section visible in this file. Do not remove.");

            VerifyRarityFilterEntries(cfg);
        }

        // Diagnostic check (v1.0.7): after binding, verify the [RarityFilter]
        // entries are actually registered in the loaded config so they show up
        // in gudako.X10.cfg with their true/false defaults. If a stale config
        // file from an older version is being reused and something prevented
        // the new lines from being written, we log a clear warning telling the
        // user to delete the file once. Uses LINQ over ConfigFile.ConfigLines
        // because BepInEx 5.x exposes no public ContainsSetting API.
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

            var defs = new System.Collections.Generic.HashSet<string>(
                cfg.ConfigDefinitions.Select(d => d.Section + "/" + d.Key));

            int present = keys.Count(k => defs.Contains("RarityFilter/" + k));

            if (present == keys.Length)
            {
                log.LogInfo("[RarityFilter] all " + keys.Length + " rarity toggles are present in the config file.");
                return;
            }

            log.LogWarning("[RarityFilter] only " + present + "/" + keys.Length +
                " rarity toggles were found in the loaded config. If you don't see them in BepInEx/config/gudako.X10.cfg, " +
                "delete that file and relaunch the game once - the full section will be regenerated.");
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

        // Installs a multiply prefix on Inventory.<name>(<types>). HarmonyX
        // binds patch parameters by NAME, and recent game recompiles renamed
        // the count parameter ("count" -> "countToAdd"), so we emit a tiny
        // DynamicMethod per target whose parameter names are taken from the
        // actual game method. Returns false (without throwing) when the
        // overload doesn't exist in this game build.
        private static bool TryPatchGive(Harmony harmony, string name, Type[] types)
        {
            MethodInfo target = AccessTools.Method(typeof(Inventory), name, types);
            if (target == null) return false;

            ParameterInfo[] ps = target.GetParameters();
            int itemPos = Array.FindIndex(ps, p => p.ParameterType == typeof(ItemIndex));
            int countPos = Array.FindIndex(ps, p => p.ParameterType == typeof(int));
            if (itemPos < 0 || countPos < 0) return false;

            Type[] paramTypes =
            {
                ps[itemPos].ParameterType,                      // ItemIndex
                ps[countPos].ParameterType.MakeByRefType()      // ref int count
            };

            DynamicMethod prefix = new DynamicMethod(
                "GudakoX10_Prefix_" + name + "_" + countPos,
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                CallingConventions.Standard,
                typeof(bool),                                   // return: run original
                paramTypes,
                typeof(MultiplyPrefix).Module,
                true);

            prefix.DefineParameter(1, ParameterAttributes.None, ps[itemPos].Name);
            prefix.DefineParameter(2, ParameterAttributes.None, ps[countPos].Name);

            ILGenerator il = prefix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);                                 // itemIndex
            il.Emit(OpCodes.Ldarga, 1);                               // &count
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(MultiplyPrefix), "MultiplyCountCore"));
            il.Emit(OpCodes.Ldc_I4_1);                                // always true
            il.Emit(OpCodes.Ret);

            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            log.LogInfo($"Patched RoR2.Inventory.{name}({string.Join(", ", Array.ConvertAll(types, t => t.Name))}) - pickups will be multiplied.");
            return true;
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
        // Only applied when no GiveItem overload could be patched above. The
        // count parameter is bound via named-parameter syntax (__2) so this
        // compiles regardless of what the game build names it.
        [HarmonyPatch(typeof(Inventory), "GiveItem", new[] { typeof(ItemIndex), typeof(int) })]
        [HarmonyPostfix]
        private static void multiplyItems(ItemIndex __1, ref int __2)
        {
            if (!ShouldMultiply(__1)) return;
            __2 *= multiplier.Value;
        }
    }

    // Multiply logic shared by the DynamicMethod prefixes emitted for each
    // Inventory give overload. The generated prefix calls MultiplyCountCore
    // with (ItemIndex, ref int count) and always returns true so the original
    // method executes with the scaled count. MultiplyOnce semantics are
    // implemented with a [ThreadStatic] flag: the give methods form a chain
    // (e.g. GiveItem(itemDef,count) -> GiveItem(itemIndex,count);
    // GiveItemChanneled/Rented -> GiveItemPermanent), and without the guard
    // an item would be multiplied once per hop in the chain.
    internal static class MultiplyPrefix
    {
        [ThreadStatic] private static bool multiplying;

        public static void MultiplyCountCore(ItemIndex itemIndex, ref int count)
        {
            // Only act on additions; removals/zero pass through.
            if (count <= 0) return;

            // Already multiplied higher up in the give-method chain.
            if (multiplying) return;

            try
            {
                multiplying = true;
                if (!X10.ShouldMultiply(itemIndex)) return;
                count *= X10.MultiplierValue;
            }
            finally
            {
                multiplying = false;
            }
        }
    }
}
