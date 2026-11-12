using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;

// Reconstructed source for Gudako-x10.dll (namespace megidont, class X10).
// Reverse-engineered from the shipped binary and rebuilt against modern
// Risk of Rain 2 / BepInEx APIs.
namespace megidont
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("gudako.X10", "10x Pickup Mod", "1.0.0")]
    public class X10 : BaseUnityPlugin
    {
        private static ManualLogSource log;
        private static ConfigFile config;

        private static ConfigEntry<int> multiplier;
        private static ConfigEntry<string> forbiddenItems;
        private static ConfigEntry<bool> fixVoidItems;
        private static ConfigEntry<bool> enableIndex109;
        private static ConfigEntry<bool> enableIndex167;
        private static ConfigEntry<bool> enableIndex168;
        private static ConfigEntry<bool> enableIndex176;

        // The internally-used enemy items that should never be multiplied.
        private static readonly string defaultForbiddenItems =
            "ITEM_ADAPTIVEARMOR_NAME,ITEM_BOOSTATTACKSPEED_NAME,ITEM_BOOSTDAMAGE_NAME," +
            "ITEM_BOOSTEQUIPMENTRECHARGE_NAME,ITEM_BOOSTHP_NAME,ITEM_CRIPPLEWARDONLEVEL_NAME," +
            "ITEM_CUTHP_NAME,ITEM_DRIZZLEPLAYERHELPER_NAME,ITEM_EXTRALIFECONSUMED_NAME," +
            "ITEM_GHOST_NAME,ITEM_HEALTHDECAY_NAME,ITEM_INVADINGDOPPELGANGER_NAME," +
            "ITEM_LEVELBONUS_NAME,ITEM_MINIONLEASH_NAME,ITEM_MONSOONPLAYERHELPER_NAME," +
            "ITEM_SCRAPGREEN_NAME,ITEM_SCRAPRED_NAME,ITEM_SCRAPWHITE_NAME," +
            "ITEM_SCRAPYELLOW_NAME,ITEM_SKULLCOUNTER_NAME";

        private void Awake()
        {
            log = Logger;
            config = base.Config;
            InitConfig(config);

            // Self-check: verify our hard-coded forbidden list still matches
            // the game's current item name tokens. If it doesn't, the game
            // updated an internal item's token and we need to know about it.
            Initialize();

            var harmony = new Harmony("gudako.X10");
            harmony.PatchAll(typeof(X10));
            log.LogInfo("10x Pickup Mod loaded!");
        }

        private static void InitConfig(ConfigFile cfg)
        {
            multiplier = cfg.Bind("BasicConfig", "Multiplier", 10,
                "Change this number to change pickup quantity.");
            forbiddenItems = cfg.Bind("BasicConfig", "Forbidden Item List", defaultForbiddenItems,
                "Change this list to change which items aren't multiplied. By default, this contains internally used enemy items. You can find a list of internal item names at https://github.com/risk-of-thunder/R2Wiki/wiki/Item-&-Equipment-IDs-and-Names - from there it should be fairly easy to figure out the rest!");
            fixVoidItems = cfg.Bind("BasicConfig", "Enable Void Item Fix", true,
                "During testing, the multiplier for void items ended up squaring the drop. Oops. Reenable if you want.");
            enableIndex109 = cfg.Bind("EnableByIndex", "Enable multiplication of MinHealthPercentage", false,
                "Because these items do not have nameTokens, we have options here in case you wanna enable them.");
            enableIndex167 = cfg.Bind("EnableByIndex", "Enable multiplication of TeamSizeDamageBonus", false,
                "Because these items do not have nameTokens, we have options here in case you wanna enable them.");
            enableIndex168 = cfg.Bind("EnableByIndex", "Enable multiplication of TeleportWhenOob", false,
                "Because these items do not have nameTokens, we have options here in case you wanna enable them.");
            enableIndex176 = cfg.Bind("EnableByIndex", "Enable multiplication of UseAmbientLevel", false,
                "Because these items do not have nameTokens, we have options here in case you wanna enable them.");
        }

        // Validates the embedded default forbidden-item list against the live
        // RoR2Content.Items / JunkContent.Items nameTokens. Logs a critical
        // error if the game's tokens no longer match what we shipped with.
        private static void Initialize()
        {
            string expected = string.Concat(
                RoR2Content.Items.AdaptiveArmor.nameToken, ",",
                RoR2Content.Items.BoostAttackSpeed.nameToken, ",",
                RoR2Content.Items.BoostDamage.nameToken, ",",
                RoR2Content.Items.BoostEquipmentRecharge.nameToken, ",",
                RoR2Content.Items.BoostHp.nameToken, ",",
                RoR2Content.Items.CrippleWardOnLevel.nameToken, ",",
                RoR2Content.Items.CutHp.nameToken, ",",
                RoR2Content.Items.DrizzlePlayerHelper.nameToken, ",",
                RoR2Content.Items.ExtraLifeConsumed.nameToken, ",",
                RoR2Content.Items.Ghost.nameToken, ",",
                RoR2Content.Items.HealthDecay.nameToken, ",",
                RoR2Content.Items.InvadingDoppelganger.nameToken, ",",
                RoR2Content.Items.LevelBonus.nameToken, ",",
                RoR2Content.Items.MinionLeash.nameToken, ",",
                RoR2Content.Items.MonsoonPlayerHelper.nameToken, ",",
                JunkContent.Items.ScrapGreen.nameToken, ",",
                JunkContent.Items.ScrapRed.nameToken, ",",
                JunkContent.Items.SkullCounter.nameToken);

            if (defaultForbiddenItems == expected) return;

            log.LogFatal("Critical Error:\nThe current default forbidden item list is NOT CORRECT.\n(this is not the one in your config, this should only happen if something goes horribly wrong.)\nThe current default set is "
                + defaultForbiddenItems
                + "\nCurrent nametokens mean it should be "
                + expected
                + "\nIf you know what this means, you're probably ok. If this is the only mod you are running, message me @Megidon't#0413 on discord.");
        }

        // Postfix on Inventory.GiveItem(ItemIndex, int): multiplies the count
        // by the configured multiplier unless the item is forbidden.
        [HarmonyPatch(typeof(Inventory), "GiveItem", new[] { typeof(ItemIndex), typeof(int) })]
        [HarmonyPostfix]
        private static void multiplyItems(ItemIndex itemIndex, ref int count)
        {
            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);

            // Items without a nameToken can only be filtered by index; each
            // has its own opt-in config switch (disabled by default).
            int[] disabledIndexes = new int[4];
            if (!enableIndex109.Value) disabledIndexes[0] = 109;
            if (!enableIndex167.Value) disabledIndexes[1] = 167;
            if (!enableIndex168.Value) disabledIndexes[2] = 168;
            if (!enableIndex176.Value) disabledIndexes[3] = 176;

            bool byIndex = disabledIndexes.Contains((int)itemIndex);

            // Void items were double-multiplied (the unlock counts as a give);
            // skip multiplying them unless the user re-enables the old behaviour.
            bool isVoid = false;
            if (itemDef.tier == ItemTier.Void)
            {
                isVoid = !fixVoidItems.Value ? false : true;
            }

            if (forbiddenItems.Value.Contains(itemDef.nameToken) || byIndex || isVoid)
                return;

            count *= multiplier.Value;
        }
    }
}
