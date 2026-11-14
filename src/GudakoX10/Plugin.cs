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
// NOTE ON PATCHING: patch installation is deferred until the BepInEx
// chainloader has finished loading every plugin, because R2API's MessagePipe
// IL patches must be active before RoR2.Inventory's generic give methods can
// be hooked safely (hooking them earlier crashed Awake() with
// InvalidProgramException in v1.0.11 and left the mod with zero patches).
// With R2API loaded we prefix Inventory.ChangeItemStacksCount<TImpl> (the
// modern choke point) plus both GiveItem overloads; a re-entrancy guard makes
// the overlapping patches multiply exactly once. Without R2API we fall back
// to the legacy postfix-grant approach that worked in v1.0.4. We never hook
// ItemCollection.Add itself: on stripped game-lib layouts its body is
// `ldnull; throw` and detouring it produced the InvalidProgramException seen
// in the v1.0.11 log.
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
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.14")]
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

        // The Captain's microbots are ordinary, purchasable item stacks:
        // Microbots (tier 1), Nano-bots (tier 2) and Flare-bots (tier 3).
        // NOTE: these are core game items - NOT DLC content - so we must never
        // assume they are absent just because a helper type like
        // RoR2.MicrobotInfector cannot be resolved by reflection at load time.
        private static readonly string[] captainMicrobotItemCodes =
            { "Microbots", "NanoBots", "FlareBots" };

        // True once at least one microbot item stack exists in the live item
        // catalog (set from ResolveCaptainMicrobotItems during Start()). Used
        // to decide whether the GiveItem-based x10 route should cover the
        // microbot stacks.
        internal static bool MicrobotItemsPresent { get; private set; }

        // The Harmony instance used for late/lazy patch installation (see
        // MultiplyMicrobots.TryLateInstall and EnsurePatchesInstalled).
        internal static Harmony HarmonyInstance { get; private set; }

        // Set when InstallAllPatches() ran but no multiplication patch
        // actually applied (e.g. every target was a stripped stub or failed
        // validation). We then keep retrying on the legacy grant-postfix path.
        private static bool patchesPending = true;

        // Idempotent: installs the multiplier patches as soon as it is safe
        // (after the chainloader finished) and re-attempts them whenever any
        // runtime path notices they are missing. Returns true when at least
        // one multiplying patch is believed active.
        internal static bool EnsurePatchesInstalled()
        {
            if (!patchesPending) return true;
            if (!chainloaderFinished) return false;
            try
            {
                InstallAllPatches();
            }
            catch (Exception e)
            {
                log.LogError("Deferred patch installation failed: " + e);
            }
            return !patchesPending;
        }

        private void Awake()
        {
            log = Logger;
            InitConfig(base.Config);

            // CRITICAL LOAD-ORDER FIX: BepInEx 5.x creates plugin GameObjects
            // in dictionary order, which is NOT dependency order. If this mod's
            // Awake() runs before R2API's, the RoR2: MessagePipe / ILHelper
            // patches that make Inventory's generic methods callable are not
            // installed yet - hooking them then aborts Awake() with
            // InvalidProgramException and leaves the mod with zero patches
            // (this is exactly what happened on v1.0.11). We therefore defer
            // patch installation until R2API has definitely loaded.
            if (!chainloaderFinished)
            {
                chainloaderHook = new Harmony("gudako.X10.loadorder");
                var startMethod = AccessTools.Method(
                    Type.GetType("BepInEx.Chainloader, BepInEx"), "Start");
                if (startMethod != null)
                {
                    chainloaderHook.Patch(startMethod,
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(X10),
                            "Chainloader_Postfix")));
                    log.LogInfo("Deferring patches until after the BepInEx chainloader finishes " +
                        "(ensures R2API is loaded first).");
                }
                else
                {
                    log.LogWarning("Could not hook Chainloader.Start; installing patches immediately.");
                    InstallAllPatches();
                }
            }
            else
            {
                InstallAllPatches();
            }
        }

        private static bool chainloaderFinished = false;
        private static Harmony chainloaderHook;

        private static void Chainloader_Postfix()
        {
            chainloaderFinished = true;
            try { chainloaderHook?.UnpatchSelf(); } catch { }
            chainloaderHook = null;
            try { instance?.InstallAllPatches(); }
            catch (Exception e) { log.LogError("Deferred patch installation failed: " + e); }
        }

        private static X10 instance;

        private void InstallAllPatches()
        {
            instance = this;
            var harmony = new Harmony("gudako.X10");
            HarmonyInstance = harmony;

            // Log whether R2API is present BEFORE any detours are created:
            // its MessagePipe IL patches must be active for Inventory's
            // generic methods to be safely hooked at all.
            r2apiPresent = Type.GetType("R2API.R2API, R2API") != null ||
                AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetName().Name == "R2API");
            log.LogInfo(r2apiPresent
                ? "R2API detected - hooking the modern Inventory give pipeline."
                : "R2API NOT detected - falling back to legacy ItemCollection.Add patching.");

            // Wrap all patch installation: previously an InvalidProgramException
            // from a bad detour aborted Awake() mid-way, silently disabling the
            // entire mod. Now we log the failure and keep going.
            try
            {
                InstallPatches(harmony);
            }
            catch (Exception e)
            {
                log.LogError("Failed to install item-multiplier patches: " + e);
            }
        }

        internal static bool R2ApiPresent => r2apiPresent;
        private static bool r2apiPresent;

        private void InstallPatches(Harmony harmony)
        {
            // PRIMARY TARGETS: both overloads of RoR2.Inventory.GiveItem -
            //   GiveItem(ItemIndex itemIndex, int countToAdd)
            //   GiveItem(ItemDef itemDef, int count)
            // Every pickup/give path funnels through one of these, and this is
            // the same patch site family that worked in v1.0.4 (the version
            // where multiplication was confirmed working).
            //
            // CRITICAL LESSON FROM THE FAILED BUILDS: Harmony matches
            // [ref]int parameters by NAME. On 1.5.x the ItemIndex overload's
            // count parameter is named `countToAdd`, while the ItemDef
            // overload's is named `count`. A previous build declared `ref int
            // count` for BOTH prefixes, so the ItemIndex-overload patch failed
            // at runtime with 'Parameter "count" not found' and NOTHING was
            // multiplied. Each prefix below must therefore use the exact
            // parameter name of its target overload, and we verify after
            // patching that at least one GiveItem prefix actually applied.
            //
            // We deliberately do NOT touch ItemCollection.Add or
            // Inventory.ChangeItemStacksCount: those tiny methods are inlined
            // by Mono, so patches there either never execute or crash Awake()
            // with InvalidProgramException (which previously left the mod with
            // no patches at all).
            // ---- Modern path (R2API present): patch the generic give pipeline
            // The real per-stack work happens in
            //   Inventory.ChangeItemStacksCount<TImpl>(TImpl impl, ItemIndex itemIndex, int countToAdd)
            // which every GiveItem / GiveItemPermanent / pickup path funnels
            // through once R2API's MessagePipe IL patches are active. It is a
            // generic method definition, so we also patch its closed
            // instantiations (ChangeItemStacksCount<ItemIndex,int>,
            // <CharacterMaster,int>, ...) that other assemblies call directly.
            // A re-entrancy flag inside the prefix makes nested calls safe, so
            // patching the whole family can never double-multiply.
            bool patchedModern = false;
            if (r2apiPresent)
            {
                var candidates = AccessTools.GetDeclaredMethods(typeof(Inventory))
                    .Where(m => m.Name == "ChangeItemStacksCount" && !m.IsGenericMethod &&
                        m.GetParameters().Length == 3 &&
                        m.GetParameters()[1].ParameterType == typeof(ItemIndex) &&
                        m.GetParameters()[2].ParameterType == typeof(int))
                    .ToList();
                foreach (var gd in candidates)
                {
                    patchedModern |= TryPatchCaller(harmony, gd.DeclaringType,
                        gd.Name, gd.GetParameters().Select(p => p.ParameterType).ToArray(),
                        "PrefixChangeItemStacksCount",
                        declaredParams: new[] { "__instance", "itemIndex", "countToAdd" });
                }
                // Closed instantiations of the generic definition may already
                // exist (other assemblies call them directly); patch those too.
                foreach (var m in typeof(Inventory).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "ChangeItemStacksCount" || !m.IsGenericMethod) continue;
                    try
                    {
                        patchedModern |= TryPatchCaller(harmony, m.DeclaringType, m.Name,
                            m.GetParameters().Select(p => p.ParameterType).ToArray(),
                            "PrefixChangeItemStacksCount",
                            declaredParams: new[] { "__instance", "itemIndex", "countToAdd" });
                    }
                    catch { }
                }
            }

            bool patchedGiveItemIndex = TryPatchCaller(harmony, typeof(Inventory), "GiveItem",
                new[] { typeof(ItemIndex), typeof(int) }, "Prefix");
            bool patchedGiveItemDef = TryPatchCaller(harmony, typeof(Inventory), "GiveItem",
                new[] { typeof(ItemDef), typeof(int) }, "PrefixGiveItemDef");

            // Permanent-give paths run as BACKSTOPS whenever the GiveItem pair
            // did not apply; they are skipped when GiveItem was patched because
            // GiveItem*Permanent routes onward into GiveItem on modern builds
            // (installing both there would multiply twice: x10 then x10 = x100).
            bool patchedBackstop = false;
            if (!patchedGiveItemIndex && !patchedGiveItemDef)
            {
                patchedBackstop |= TryPatchCaller(harmony, typeof(Inventory), "GiveItemPermanent",
                    new[] { typeof(ItemIndex), typeof(int) }, "PrefixGiveItemPermanent");
                patchedBackstop |= TryPatchCaller(harmony, typeof(Inventory), "GiveItemPermanent",
                    new[] { typeof(ItemDef), typeof(int) }, "PrefixGiveItemPermanentDef");
            }

            if (patchedModern || patchedGiveItemIndex || patchedGiveItemDef || patchedBackstop)
            {
                // At least one multiplying patch installed cleanly; the
                // legacy grant-postfix is NOT needed and must not be added
                // on top (that would double-multiply).
                patchesPending = false;
            }
            else
            {
                // Legacy fallback (pre-R2API layouts, e.g. what worked in
                // v1.0.4): postfix on GiveItem(ItemIndex, int) that grants the
                // extra stacks itself. Only installed when NOTHING else applied.
                log.LogWarning("No modern give method could be patched; " +
                    "falling back to legacy GiveItem postfix patches.");
                try
                {
                    harmony.PatchAll(typeof(X10));
                    legacyPostfixInstalled = true;
                    patchesPending = false;
                }
                catch (Exception e)
                {
                    log.LogError("Legacy postfix patch installation failed too: " + e.Message);
                    // Keep patchesPending = true so EnsurePatchesInstalled()
                    // retries at the next pickup attempt.
                }
            }

            // Captain microbot duplication (self-guarding: retries lazily at
            // runtime; see MultiplyMicrobots).
            MultiplyMicrobots.Install(harmony);

            log.LogInfo("10x Pickup Mod loaded!");
        }

        internal static bool LegacyPostfixInstalled => legacyPostfixInstalled;
        private static bool legacyPostfixInstalled;

        // Resolve a method on `type` by name+signature and install a Harmony
        // prefix from MultiplyPrefix. `prefixMethod` names the method on
        // MultiplyPrefix to use (defaults to "Prefix"). Returns false if the
        // target does not exist on this game build or the prefix could not be
        // applied (never throws).
        //
        // After a successful harmony.Patch() call we verify the prefix is
        // actually attached: HarmonyX applies patches lazily via
        // ManagedMethodPatcher, so a broken prefix (e.g. one whose `ref int`
        // parameter name doesn't match the target's) does NOT throw here - it
        // fails later at first-invocation JIT time with 'Parameter "x" not
        // found'. We pre-empt that by checking the prefix's own parameter
        // names against the resolved target up front and rejecting mismatches
        // before they can silently disable multiplication.
        private static bool TryPatchCaller(Harmony harmony, Type type, string methodName,
            Type[] signature, string prefixMethod = "Prefix",
            string[] declaredParams = null)
        {
            MethodInfo target;
            if (signature == null && declaredParams != null)
            {
                // Pick the overload whose parameter names match declaredParams
                // exactly (used for generic methods like ChangeItemStacksCount
                // where several same-arity overloads can exist).
                target = AccessTools.GetDeclaredMethods(type).FirstOrDefault(m =>
                    m.Name == methodName &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Select(p => p.Name).SequenceEqual(declaredParams));
            }
            else
            {
                target = AccessTools.Method(type, methodName, signature);
            }
            if (target == null) return false;
            try
            {
                var prefix = AccessTools.Method(typeof(MultiplyPrefix), prefixMethod);

                // Guard: every named parameter the prefix declares (other
                // than __instance/__result/Harmony specials) must exist on the
                // target with the same name. This catches the exact failure
                // mode that killed v1.0.12 ('count' vs 'countToAdd').
                foreach (var pp in prefix.GetParameters())
                {
                    string n = pp.Name;
                    if (n.StartsWith("__")) continue;
                    if (target.GetParameters().All(tp => tp.Name != n))
                    {
                        log.LogWarning("Skipping patch " + type.Name + "." + methodName +
                            ": prefix parameter '" + n + "' does not exist on the target " +
                            "(its parameters are: " + string.Join(", ",
                            target.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ").");
                        return false;
                    }
                }

                harmony.Patch(target, prefix: new HarmonyMethod(prefix));

                // VERIFY THE PATCH ACTUALLY APPLIED. With BepInEx's HarmonyX,
                // harmony.Patch() only queues the detour (ManagedMethodPatcher
                // applies it lazily at first call), so a broken prefix - e.g.
                // one whose parameter names don't match the target, or a
                // method Mono inlines away - would fail SILENTLY later and we
                // would report success here (exactly what happened on
                // v1.0.12/v1.0.13/v1.0.14: "Patched ..." logged, nothing
                // multiplied). We force the patch to materialise now via
                // PatchProcessor.CreateILInfo/PatchAllState is not public, so
                // instead we check Harmony's own patch database AND, most
                // importantly, verify the target method's body is real:
                // stripped reference-lib methods have 2-instruction stubs
                // (`ldnull; throw`) that Mono compiles into invalid wrappers,
                // which is the source of every InvalidProgramException /
                // IL Compile Error seen in earlier logs.
                if (IsStubMethod(target))
                {
                    log.LogWarning("Target " + type.Name + "." + methodName + " has a stubbed body " +
                        "(ldnull;throw) in the loaded assembly - Harmony cannot wrap it reliably. " +
                        "This mod must run against the REAL game Assembly-CSharp/RoR2 assemblies.");
                    try { new Harmony("gudako.X10.unwind").Unpatch(target); } catch { }
                    return false;
                }

                // NOTE: with BepInEx's HarmonyX, harmony.Patch() only queues
                // the detour (ManagedMethodPatcher applies it lazily at first
                // call), so a parameter-name mismatch would NOT throw here -
                // it would fail silently later. The up-front name guard above
                // is what actually protects us; the log line prints the real
                // target parameter names so mismatches are obvious in the log.
                log.LogInfo("Patched " + type.Name + "." + methodName + "(" +
                    string.Join(", ", target.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ").");
                return true;
            }
            catch (Exception e)
            {
                log.LogWarning("Could not patch " + type.Name + "." + methodName + ": " + e.Message);
                return false;
            }
        }

        // True when the method's compiled IL is a trivial `ldnull; throw`
        // placeholder (the shape every public Inventory/ItemCollection method
        // has in stripped reference assemblies such as the RiskOfRain2.GameLibs
        // NuGet package). Real game builds never produce this shape.
        private static bool IsStubMethod(MethodBase method)
        {
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return false;
                byte[] il = body.GetILAsByteArray();
                if (il == null || il.Length > 8) return false;
                // Scan for the ldnull(0x75)+throw(0x7A) pair covering the body.
                int nonTrailingNop = il.Length;
                while (nonTrailingNop > 0 && il[nonTrailingNop - 1] == 0) nonTrailingNop--;
                bool sawLdnull = false, sawThrow = false;
                for (int i = 0; i < nonTrailingNop; i++)
                {
                    if (il[i] == 0x75) sawLdnull = true;
                    else if (il[i] == 0x7A) sawThrow = true;
                    else return false;
                }
                return sawLdnull && sawThrow;
            }
            catch { return false; }
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

        // Maps the Captain microbot item codes (Microbots / Nano-bots /
        // Flare-bots) to their live ItemIndexes once the catalog is populated.
        // These are CORE items that have been in RoR2 since launch - they are
        // NOT DLC content - so if none resolve here, something is wrong with
        // the catalog or our item codes, and we say so loudly instead of
        // blaming a missing DLC. The actual x10 for these stacks runs through
        // the normal GiveItem/ChangeItemStacksCount multiplier; the separate
        // MicrobotInfector duplication (see MultiplyMicrobots) additionally
        // spawns extra bots when the game has that helper component.
        private static void ResolveCaptainMicrobotItems()
        {
            int found = 0;
            foreach (string code in captainMicrobotItemCodes)
            {
                if (ItemCatalog.FindItemIndex(code) != ItemIndex.None) found++;
            }

            MicrobotItemsPresent = found > 0;

            if (found > 0)
            {
                log.LogInfo("Captain microbot items present (" + found + "/" + captainMicrobotItemCodes.Length +
                    "); picking them up will grant x" + captainMicrobotMultiplier.Value + " stacks.");
            }
            else
            {
                log.LogError("None of the core microbot items (Microbots/NanoBots/FlareBots) could be " +
                    "found in the item catalog at Start(). This should never happen on an unmodified " +
                    "game - please report this line together with your mod list.");
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
        private static void multiplyItems(Inventory __instance, ItemIndex __1, int __2)
        {
            if (__2 <= 0) return;
            if (inFallbackGrant) return;      // anti-recursion guard
            if (!IsPlayerInventory(__instance)) return;
            if (!ShouldMultiply(__1)) return;

            int extra = __2 * (multiplier.Value - 1);
            if (extra <= 0) return;
            inFallbackGrant = true;
            try { __instance.GiveItem(__1, extra); }
            finally { inFallbackGrant = false; }
        }
        private static bool inFallbackGrant = false;

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

        public static bool IsPlayerInventory(Inventory inv)
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

    // Harmony prefixes for the item-give entry points on RoR2.Inventory:
    //   ChangeItemStacksCount(ItemIndex, int) - the modern choke point that
    //     every give/remove path funnels through (it internally calls
    //     ItemCollection.Add).
    //   GiveItemPermanent(ItemIndex, int) - backstop for permanent-item paths.
    // We deliberately do NOT patch ItemCollection.Add itself: it is tiny and
    // Mono always inlines it into its callers, so a prefix there never runs
    // and a detour/ILManipulator on it crashes with InvalidProgramException.
    // Multiplication only applies to stacks that belong to a local player's
    // inventory. Enemy bodies share the same Inventory type and receive
    // internal bookkeeping items (BoostDamage, LevelBonus, SkullCounter, ...)
    // through these exact methods during spawning; multiplying those counts
    // was what broke enemy spawns in earlier builds. Removals (negative
    // counts) and non-player inventories pass through untouched.
    internal static class MultiplyPrefix
    {
        // One-shot flag: the first time a prefix runs we log proof that the
        // patch is actually executing (HarmonyX applies detours lazily, so a
        // broken patch otherwise fails silently at first pickup).
        private static bool firstRunLogged = false;

        // Re-entrancy guard: GiveItem -> ChangeItemStacksCount and
        // GiveItemPermanent -> GiveItem chains mean several of our prefixes
        // can fire for one logical give. Only the outermost one multiplies.
        [ThreadStatic] private static bool insideMultiply;

        // Prefix for Inventory.ChangeItemStacksCount<TImpl>(TImpl impl,
        // ItemIndex itemIndex, int countToAdd) - the modern choke point every
        // give path funnels through once R2API's MessagePipe patches are
        // active. Parameter names match the game method exactly (HarmonyX
        // binds ref parameters by name); `impl` is deliberately not declared.
        public static bool PrefixChangeItemStacksCount(Inventory __instance,
            ItemIndex itemIndex, ref int countToAdd)
        {
            LogFirstRun("ChangeItemStacksCount");
            return Multiply(ref countToAdd, __instance, itemIndex, checkFilters: true);
        }

        // Prefix for Inventory.GiveItem(ItemIndex itemIndex, int countToAdd).
        // The ref-parameter MUST be named `countToAdd` to match the game's
        // method exactly - HarmonyX binds `ref int` parameters by name, and
        // naming it `count` made this patch fail with
        // 'Parameter "count" not found' in v1.0.12 (nothing multiplied).
        public static bool Prefix(Inventory __instance, ItemIndex itemIndex, ref int countToAdd)
        {
            LogFirstRun("GiveItem(ItemIndex)");
            return Multiply(ref countToAdd, __instance, itemIndex, checkFilters: true);
        }

        // Prefix for Inventory.GiveItem(ItemDef itemDef, int count). This
        // overload's count parameter really is named `count`.
        public static bool PrefixGiveItemDef(Inventory __instance, ItemDef itemDef, ref int count)
        {
            if (itemDef == null) return true;
            LogFirstRun("GiveItem(ItemDef)");
            return Multiply(ref count, __instance, itemDef.itemIndex, checkFilters: true);
        }

        // Prefix for Inventory.GiveItemPermanent(ItemIndex itemIndex, int countToAdd).
        // Backstop only - installed solely when neither GiveItem overload
        // could be patched (see InstallPatches), so double-multiplication via
        // Permanent -> GiveItem routing cannot occur.
        public static bool PrefixGiveItemPermanent(Inventory __instance, ItemIndex itemIndex, ref int countToAdd)
        {
            LogFirstRun("GiveItemPermanent(ItemIndex)");
            return Multiply(ref countToAdd, __instance, itemIndex, checkFilters: true);
        }

        // Prefix for Inventory.GiveItemPermanent(ItemDef itemDef, int count).
        public static bool PrefixGiveItemPermanentDef(Inventory __instance, ItemDef itemDef, ref int count)
        {
            if (itemDef == null) return true;
            LogFirstRun("GiveItemPermanent(ItemDef)");
            return Multiply(ref count, __instance, itemDef.itemIndex, checkFilters: true);
        }

        private static void LogFirstRun(string site)
        {
            if (firstRunLogged) return;
            firstRunLogged = true;
            X10.log.LogInfo("Item multiplier active - first give routed through " + site + ".");
        }

        // Shared multiplication core. Always returns true so the original
        // method still executes normally with the (possibly scaled) count.
        //   count <= 0        -> removals pass through untouched
        //   nested call       -> only the outermost prefix multiplies (prevents
        //                        x100 when both GiveItem and
        //                        ChangeItemStacksCount are patched)
        //   non-player inv.   -> enemy/director bookkeeping passes untouched
        //                        (this is what previously broke enemy spawns)
        //   checkFilters      -> run ShouldMultiply's rarity/forbidden filters
        //                        only when the path might bypass GiveItem.
        private static bool Multiply(ref int count, Inventory inventory, ItemIndex itemIndex, bool checkFilters)
        {
            if (count <= 0) return true;
            if (insideMultiply) return true;
            if (!X10.IsPlayerInventory(inventory)) return true;
            if (checkFilters && !X10.ShouldMultiply(itemIndex)) return true;

            insideMultiply = true;
            try { count *= X10.MultiplierValue; }
            finally { insideMultiply = false; }
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
    // MicrobotInfector is a helper component whose presence varies between
    // game builds; every reference to it (and to PlayerCharacterMaster) is
    // resolved via reflection at patch install time. When it cannot be found
    // we log an informational line and rely on the STACK multiplier instead:
    // the microbot items themselves (Microbots/Nano-bots/Flare-bots) are core
    // launch-era items, and picking them up is already multiplied x10 by the
    // GiveItem/ChangeItemStacksCount prefixes above.
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
                // NOT a DLC problem: microbots are core launch-era items.
                // At Awake()/chainloader time the game's character content
                // packs may not have been registered yet, so helper types
                // like MicrobotInfector can legitimately be missing from the
                // loaded type tables. Retry lazily: TryLateInstall() is
                // called from the GiveItem postfix on the first pickups and
                // from any later attempt until it succeeds.
                X10.log.LogInfo("RoR2.MicrobotInfector not found yet; will retry when the Captain " +
                    "content is registered (microbot STACKS are still multiplied x" +
                    X10.MultiplierValue + " by the normal pickup patch).");
                return;
            }
            DoPatch(harmony);
        }

        // Lazy retry path: attempts to install the Infected postfix once the
        // type becomes available. Safe to call repeatedly; does nothing after
        // a successful install.
        public static void TryLateInstall()
        {
            if (installed || lateAttemptFailedHard) return;
            if (harmonyRef == null) return;
            if (infectorType != null) { DoPatch(harmonyRef); return; }
            try
            {
                infectorType = AccessTools.TypeByName("RoR2.MicrobotInfector");
                if (infectorType != null) DoPatch(harmonyRef);
            }
            catch { lateAttemptFailedHard = true; }
        }

        private static Harmony harmonyRef;
        private static bool lateAttemptFailedHard;

        private static void DoPatch(Harmony harmony)
        {
            harmonyRef = harmony;
            infectedMethod = AccessTools.Method(infectorType, "Infected");
            if (infectedMethod == null)
            {
                X10.log.LogWarning("MicrobotInfector.Infected not found; Captain microbot duplication disabled.");
                lateAttemptFailedHard = true;
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
                    "Captain microbot duplication will retry later.");
                return;
            }

            installed = true;
            try
            {
                harmony.Patch(infectedMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(MultiplyMicrobots), "Postfix")));
            }
            catch (Exception e)
            {
                installed = false;
                X10.log.LogWarning("Could not patch MicrobotInfector.Infected: " + e.Message);
                return;
            }
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
