using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using System.Reflection.Emit; // CodeInstruction/OpCode live here in Lib.Harmony (HarmonyX)

// Reconstructed source for Gudako-x10.dll (namespace megidont, class X10).
// Reverse-engineered from the shipped binary and rebuilt against modern
// Risk of Rain 2 APIs.
//
// NOTE ON PATCHING: The current game's Inventory.GiveItem(ItemIndex, int)
// overload takes the count BY VALUE, so a normal prefix/postfix cannot modify
// it (prefix ref params fail with "Parameter ... not found"; postfix ref to a
// value param throws at IL-emit time), and DynamicMethod-based prefixes are
// rejected by HarmonyX ("Cannot directly reference dynamic method"). We
// therefore use a transpiler that rewrites the raw IL of each give overload,
// injecting `count = MultiplyTranspiled(itemDefOrIndex, count)` right before
// the first Stloc of the count local. Transpiler manipulation is positional,
// so it is immune to parameter-name binding issues entirely.
namespace megidont
{
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.9")]
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
                log.LogError("Could not find any Inventory give methods to patch - item multiplication is DISABLED on this game build.");

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

        // Installs a multiply prefix on Inventory.<name>(<types>). The count
        // parameter is passed BY VALUE in the real game, so we cannot mutate
        // it from a normal prefix; instead we emit (at runtime, via
        // Reflection.Emit) a tiny DynamicMethod whose parameter NAMES are
        // copied verbatim from the target method and whose count parameter is
        // declared `ref int`. HarmonyX binds patch parameters by name, so the
        // generated method always matches whatever the current game build
        // calls the argument ("count", "countToAdd", ...), and declaring it
        // ref lets us scale it. If the emitted prefix fails to apply for any
        // reason, we fall back to a transpiler that rewrites the IL of the
        // first "Ldarg <count>; Stloc x" pair into a MultiplyFromIndex call.
        // Returns false (without throwing) when the overload doesn't exist in
        // this game build.
        private static bool TryPatchGive(Harmony harmony, string name, Type[] types)
        {
            MethodInfo target = AccessTools.Method(typeof(Inventory), name, types);
            if (target == null) return false;

            ParameterInfo[] ps = target.GetParameters();
            int itemPos = Array.FindIndex(ps, p => p.ParameterType == typeof(ItemIndex)
                                                || p.ParameterType == typeof(ItemDef));
            int countPos = Array.FindIndex(ps, p => p.ParameterType == typeof(int));
            if (itemPos < 0 || countPos < 0) return false;

            // v1.0.9 fix: the previous version tried to apply a runtime-
            // generated DynamicMethod as a Harmony prefix. HarmonyX rejects
            // that with "Cannot directly reference dynamic method ... Use a
            // factory method instead", so no patches were ever installed and
            // pickups were never multiplied. The only supported approach for
            // by-value count parameters is a transpiler on a normal static
            // method — which is exactly what we install now, unconditionally.
            bool itemIsDef = ps[itemPos].ParameterType == typeof(ItemDef);

            // Arg slots are 1-based for instance methods (slot 0 is `this`).
            MultiplyTranspiler.Register(target, itemPos + 1, countPos + 1, itemIsDef);

            var tm = new HarmonyMethod(typeof(MultiplyTranspiler), nameof(MultiplyTranspiler.Transpile));
            try
            {
                harmony.Patch(target, transpiler: tm);
                log.LogInfo($"Patched RoR2.Inventory.{name}({string.Join(", ", Array.ConvertAll(types, t => t.Name))}) - pickups will be multiplied.");
                return true;
            }
            catch (Exception e)
            {
                log.LogError($"Could not patch {name}: {e.Message}");
                return false;
            }
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

        internal static void LogWarning(string message)
        {
            if (log != null) log.LogWarning(message);
        }
    }

    // Harmony transpiler installed on each Inventory give overload. The
    // count parameter is passed BY VALUE in the real game, so we rewrite the
    // method IL: immediately before the first instruction that stores the
    // count argument into a local (Ldarg count; Stloc), we inject
    //     count = MultiplyTranspiled(itemArg, count)
    // which applies the multiplier exactly once per pickup. MultiplyOnce
    // semantics are enforced with a [ThreadStatic] flag because the give
    // methods form a call chain (e.g. GiveItem(itemDef,count) ->
    // GiveItem(index,count); GiveItemChanneled/Rented -> GiveItemPermanent)
    // and without the guard an item would be multiplied once per hop.
    internal static class MultiplyTranspiler
    {
        private class TargetInfo
        {
            public int ItemArgIndex;   // arg slot of ItemIndex/ItemDef param (1-based for Ldarg opcode calc)
            public int CountArgIndex;  // arg slot of the int count param
            public bool ItemIsDef;
        }

        private static readonly Dictionary<MethodBase, TargetInfo> targets =
            new Dictionary<MethodBase, TargetInfo>();

        [ThreadStatic] private static bool multiplying;

        public static void Register(MethodInfo target, int itemArgSlot, int countArgSlot, bool itemIsDef)
        {
            lock (targets)
            {
                targets[target] = new TargetInfo
                {
                    ItemArgIndex = itemArgSlot,
                    CountArgIndex = countArgSlot,
                    ItemIsDef = itemIsDef,
                };
            }
        }

        // Called by Harmony when patching the DMD body. We only registered a
        // transpiler for methods we inspected in Register, so info must exist.
        public static IEnumerable<CodeInstruction> Transpile(
            MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            TargetInfo info;
            lock (targets) { targets.TryGetValue(original, out info); }
            if (info == null) return codes;

            CodeInstruction multiplyCall = info.ItemIsDef
                ? new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(MultiplyTranspiler), nameof(MultiplyFromDef)))
                : new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(MultiplyTranspiler), nameof(MultiplyFromIndex)));

            // Find the earliest "Ldarg <count>; Stloc x" pair — Unity's
            // decompiler-style stubs always stash their args into locals at
            // the top of the body. Inject our scaling call between them so
            // the local receives the multiplied value.
            for (int i = 0; i < codes.Count - 1; i++)
            {
                CodeInstruction ld = codes[i];
                CodeInstruction st = codes[i + 1];
                if (!IsLdarg(ld, info.CountArgIndex)) continue;
                if (st.opcode != OpCodes.Stloc && !(st.opcode.Name.StartsWith("stloc"))) continue;

                // Replace "ldarg.N; stloc.X" with
                // "ldarg.item; ldarg.count; CALL; stloc.X"
                codes[i] = LoadArg(info.ItemArgIndex);
                codes.Insert(i + 1, LoadArg(info.CountArgIndex));
                codes.Insert(i + 2, multiplyCall);
                return codes;
            }

            // Fallback: no stash pattern found. If the method is tiny
            // (stub-like tail call or direct use), wrap differently isn't
            // safe — log and leave unmodified rather than corrupt IL.
            X10.LogWarning($"Could not locate count-argument usage in {original.Name}; this overload will NOT multiply.");
            return codes;
        }

        private static bool IsLdarg(CodeInstruction c, int argSlot)
        {
            if (c.opcode == OpCodes.Ldarg_0) return argSlot == 0;
            if (c.opcode == OpCodes.Ldarg_1) return argSlot == 1;
            if (c.opcode == OpCodes.Ldarg_2) return argSlot == 2;
            if (c.opcode == OpCodes.Ldarg_3) return argSlot == 3;
            if (c.opcode == OpCodes.Ldarg || c.opcode == OpCodes.Ldarg_S)
                return c.operand is int i && i == argSlot;
            return false;
        }

        private static CodeInstruction LoadArg(int slot)
        {
            switch (slot)
            {
                case 0: return new CodeInstruction(OpCodes.Ldarg_0);
                case 1: return new CodeInstruction(OpCodes.Ldarg_1);
                case 2: return new CodeInstruction(OpCodes.Ldarg_2);
                case 3: return new CodeInstruction(OpCodes.Ldarg_3);
                default: return new CodeInstruction(OpCodes.Ldarg_S, slot);
            }
        }

        // Runtime helpers invoked from the injected IL.
        public static int MultiplyFromIndex(ItemIndex itemIndex, int count)
            => MultiplyCore(itemIndex, count);

        public static int MultiplyFromDef(ItemDef itemDef, int count)
            => MultiplyCore(itemDef ? itemDef.itemIndex : ItemIndex.None, count);

        private static int MultiplyCore(ItemIndex itemIndex, int count)
        {
            // Only act on additions; removals/zero pass through.
            if (count <= 0) return count;

            // Already multiplied higher up in the give-method chain.
            if (multiplying) return count;

            try
            {
                multiplying = true;
                if (!X10.ShouldMultiply(itemIndex)) return count;
                return count * X10.MultiplierValue;
            }
            finally
            {
                multiplying = false;
            }
        }
    }
}
