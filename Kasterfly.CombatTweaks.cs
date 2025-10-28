using HarmonyLib;
using Kasterfly.CombatTweaks;
using Kasterfly.CombatTweaks.Comps;
using Kasterfly.CombatTweaks.MainFunctions;
using Kasterfly.SettingsBuilder;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Kasterfly.CombatTweaks.HarmonyPatches;
using UnityEngine;
using Verse;
using static Kasterfly.CombatTweaks.CombatTweaksModSettings;

namespace Kasterfly.CombatTweaks
{
    [StaticConstructorOnStartup]
    public static class YayoUnpatcher
    {
        static YayoUnpatcher()
        {

            new Harmony("kasterfly.combattweaks").Patch(
                original: AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(YayoUnpatcher), nameof(DoYayoUnpatch))
            );
        }

        public static void DoYayoUnpatch()
        {
            var harmony = new Harmony("kasterfly.combattweaks.unpatcher");
            MethodInfo target = AccessTools.Method(typeof(DamageWorker_AddInjury), "ApplyDamageToPart");

            var patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo == null) return;

            bool unpatched = false;

            foreach (var prefix in patchInfo.Prefixes.Where(p => p.owner.ToLowerInvariant().Contains("yayoscombat")))
            {
                harmony.Unpatch(target, HarmonyPatchType.Prefix, prefix.owner);
                unpatched = true;
            }

            foreach (var transpiler in patchInfo.Transpilers.Where(p => p.owner.ToLowerInvariant().Contains("yayoscombat")))
            {
                harmony.Unpatch(target, HarmonyPatchType.Transpiler, transpiler.owner);
                unpatched = true;
            }

            if (unpatched)
            {
                CombatTweaksMod.Settings.yayoCombatCompat = true;
                Log.Message("[CombatTweaks] Yayo's Combat 3 patch found and unpatched successfully.");
            }
            else
            {
                Log.Message("[CombatTweaks] Yayo's Combat 3 patch not found or already removed.");
            }
        }

    }

    public class CombatTweaksMod : Mod
    {
        public static CombatTweaksModSettings Settings;
        private static SettingsGUI settingsGui = new SettingsGUI();
        private static Vector2 scrollPosition = Vector2.zero;



        public CombatTweaksMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("kasterfly.combatTweaks");
            harmony.PatchAll();
            Settings = GetSettings<CombatTweaksModSettings>();

            if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLower().Equals("mlie.yayoscombat3")))
                Settings.yayoCombatCompat = false;

            InitSettings();
            YayoUnpatcher.DoYayoUnpatch();
            Log.Message("[CombatTweaks] CombatTweaks (v1.1.16) for RimWorld 1.6");
        }

        public override string SettingsCategory() => "Combat Tweaks";
        public override void DoSettingsWindowContents(Rect inRect)
        {
            settingsGui.DrawSettings(inRect);
        }

        private void InitSettings()
        {
            settingsGui.AddCategory("General");
            settingsGui.AddItem("debugLogging", "debugLogging", "General",
                settingsGui.CreateCheckbox(() => Settings.debugLogging, val => Settings.debugLogging = val, "Enable Debug Logging"),
                35f);

            settingsGui.AddItem("showDetails", "showDetails", "General",
                settingsGui.CreateCheckbox(() => Settings.showDetails, val => Settings.showDetails = val, "Show Settings Explanations"),
                35f);

            settingsGui.AddItem("doTracking", "doTracking", "General",
                settingsGui.CreateCheckbox(() => Settings.doTracking, v => Settings.doTracking = v, "Tracking info in stats"));

            settingsGui.AddItem("difficultyBar", "difficultyBar", "General", rect =>
            {
                float difficulty = Settings.CalculateDifficultyScore();
                DrawDifficultyBar(rect, difficulty);
            });

            settingsGui.AddCategory("Compatibility");
            settingsGui.AddItem("yayoCompat", "yayoCompat", "Compatibility",
                settingsGui.CreateInfo(() => $"For Yayo's Combat 3 Compatibility:\n - This mod must be placed before Yayo's Combat 3 in your modlist"),
                90f);

            settingsGui.AddItem("yayoCombatCompatEnableYayoAAA", "yayoCombatCompatEnableYayoAAA", "Compatibility",
                settingsGui.CreateCheckbox(() => Settings.yayoCombatCompatEnableYayoAAA, val => Settings.yayoCombatCompatEnableYayoAAA = val,
                "Enable Yayo's \"Advanced Armor Algorithms\" Compatibility"),
                35f,
                new List<string> { "yayoArmorSlider" });

            settingsGui.AddItem("yayoArmorSlider", "yayoArmorSlider", "Compatibility",
                settingsGui.CreateSlider(
                    () => Settings.yayoCombatCompat_s_armorEf,
                    val => Settings.yayoCombatCompat_s_armorEf = val,
                    0f, 1f,
                    () => $"Yayo's Improved Defence Option Overwrite Value: {(Settings.yayoCombatCompat_s_armorEf * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "yayoCombatCompatEnableYayoAAA" });
            settingsGui.AddConditional("yayoArmorSlider", () => Settings.yayoCombatCompatEnableYayoAAA);

            settingsGui.AddCategory("Features");
            settingsGui.AddItem("allArmorEffectsEverywhere", "allArmorEffectsEverywhere", "Features",
                settingsGui.CreateCheckbox(() => Settings.allArmorEffectsEverywhere, v => Settings.allArmorEffectsEverywhere = v, "All Equipped Armor Protects All Body Parts"));

            settingsGui.AddItem("notArmor", "notArmor", "Features",
                settingsGui.CreateCheckbox(() => Settings.notArmor, v => Settings.notArmor = v, "That's not armor!"));

            settingsGui.AddItem("notArmorStillTakeDamage", "notArmorStillTakeDamage", "Features",
                 settingsGui.CreateCheckbox(() => Settings.notArmorStillTakeDamage, v => Settings.notArmorStillTakeDamage = v, "Non-Armor Still Takes Durability Damage From Combat"),
                 35f,
                 new List<string> { "notArmor" });
            settingsGui.AddConditional("notArmorStillTakeDamage", () => Settings.notArmor);

            settingsGui.AddItem("notArmorThreshold", "notArmorThreshold", "Features",
                settingsGui.CreateSlider(
                    () => Settings.notArmorThreshold,
                    v => Settings.notArmorThreshold = v,
                    0f, 2f,
                    () => $"If armor % is less than this than it's not counted as armor: {(Settings.notArmorThreshold * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "notArmor" });
            settingsGui.AddConditional("notArmorThreshold", () => Settings.notArmor);

            settingsGui.AddItem("durabilityEffectsArmor", "durabilityEffectsArmor", "Features",
                settingsGui.CreateCheckbox(() => Settings.durabilityEffectsArmor, v => Settings.durabilityEffectsArmor = v, "Durability Affects Armor"));

            settingsGui.AddItem("durabilityEffectsArmorMult", "durabilityEffectsArmorMult", "Features",
                 settingsGui.CreateSlider(
                    () => Settings.durabilityEffectsArmorMult,
                    v => Settings.durabilityEffectsArmorMult = v,
                    0f, 0.05f,
                    () => $"Armor effectiveness lost per 1% durability missing: {(Settings.durabilityEffectsArmorMult * 100f):F1}%",
                    0.001f),
                 35f,
                 new List<string> { "durabilityEffectsArmor" });
            settingsGui.AddConditional("durabilityEffectsArmorMult", () => Settings.durabilityEffectsArmor);

            settingsGui.AddItem("durabilityEffectsArmorDoTattered", "durabilityEffectsArmorDoTattered", "Features",
                 settingsGui.CreateCheckbox(
                     () => Settings.durabilityEffectsArmorDoTattered, 
                     v => Settings.durabilityEffectsArmorDoTattered = v, 
                     "Use Tattered Effect"),
                 35f,
                 new List<string> { "durabilityEffectsArmor" });
            settingsGui.AddConditional("durabilityEffectsArmorDoTattered", () => Settings.durabilityEffectsArmor);

            settingsGui.AddItem("doDamageEffect", "doDamageEffect", "Features",
                settingsGui.CreateCheckbox(() => Settings.doDamageEffect, v => Settings.doDamageEffect = v, "Large Damage Scaling"));

            settingsGui.AddItem("reliableArmors", "reliableArmors", "Features",
                settingsGui.CreateCheckbox(() => Settings.reliableArmors, v => Settings.reliableArmors = v, "Reliable Armors Mod's Damage Reductions"));

            settingsGui.AddItem("thickArmor", "thickArmor", "Features",
               settingsGui.CreateCheckbox(() => Settings.thickArmor, v => Settings.thickArmor = v, "Thick Armor Mod's Layered Armor Calculations"));

            settingsGui.AddItem("thickArmorEffectivenessLoss", "thickArmorEffectivenessLoss", "Features",
                settingsGui.CreateSlider(
                    () => Settings.thickArmorEffectivenessLoss,
                    v => Settings.thickArmorEffectivenessLoss = v,
                    0f, 1f,
                    () => $"Effectiveness Loss Per Layer: {(Settings.thickArmorEffectivenessLoss * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "thickArmor" });
            settingsGui.AddConditional("thickArmorEffectivenessLoss", () => Settings.thickArmor);

            settingsGui.AddItem("alwaysReduceDamage", "alwaysReduceDamage", "Features",
                settingsGui.CreateCheckbox(() => Settings.alwaysReduceDamage, v => Settings.alwaysReduceDamage = v, "Almost Always Reduce Damage"));
            settingsGui.AddItem("doAdrenaline", "doAdrenaline", "Features",
                settingsGui.CreateCheckbox(() => Settings.doAdrenaline, v => Settings.doAdrenaline = v, "Use Adrenaline System"),
                35f,
                new List<string> { "doAdrenaline" });

            settingsGui.AddItem("adrenalineChance", "adrenalineChance", "Features",
                settingsGui.CreateSlider(() => Settings.adrenalineChance, v => Settings.adrenalineChance = v, 0f, 1f, () => $"Adrenaline trigger chance: {(Settings.adrenalineChance * 100f):F0}%", 0.01f),
                60f,
                new List<string> { "doAdrenaline" });
            settingsGui.AddConditional("adrenalineChance", () => Settings.doAdrenaline);


            settingsGui.AddItem("doTechLevelEffect", "doTechLevelEffect", "Features",
                settingsGui.CreateCheckbox(() => Settings.doTechLevelEffect, v => Settings.doTechLevelEffect = v, "Tech Level Scaling"),
                35f,
                new List<string> { "techLevelBoost" });

            settingsGui.AddItem("techLevelBoost", "techLevelBoost", "Features",
                settingsGui.CreateSlider(() => Settings.techLevelBoost, v => Settings.techLevelBoost = v, 0f, 2f, () => $"Buff per Tech Level: {formatBuff(Settings.techLevelBoost, 0f)}", 0.05f),
                60f,
                new List<string> { "doTechLevelEffect" });
            settingsGui.AddConditional("techLevelBoost", () => Settings.doTechLevelEffect);

            settingsGui.AddItem("durabilityTypeInteraction", "durabilityTypeInteraction", "Features",
                settingsGui.CreateCheckbox(() => Settings.durabilityTypeInteraction, v => Settings.durabilityTypeInteraction = v, "Durability Type Interaction"),
                35f,
                new List<string> { "durabilityTypeInteractionMultiplier" });

            settingsGui.AddItem("durabilityTypeInteractionMultiplier", "durabilityTypeInteractionMultiplier", "Features",
                settingsGui.CreateSlider(() => Settings.durabilityTypeInteractionMultiplier, v => Settings.durabilityTypeInteractionMultiplier = v, 0f, 4f, () => $"Durability Damage Weakness Multiplier: {(Settings.durabilityTypeInteractionMultiplier * 100f):F0}%", 0.05f),
                60f,
                new List<string> { "durabilityTypeInteraction" });
            settingsGui.AddConditional("durabilityTypeInteractionMultiplier", () => Settings.durabilityTypeInteraction);

            settingsGui.AddItem("armorDeflectionRework", "armorDeflectionRework", "Features",
                settingsGui.CreateCheckbox(() => Settings.armorDeflectionRework, v => Settings.armorDeflectionRework = v, "Armor Deflection Change (Rework)"),
                55f,
                new List<string> { "armorDeflectionReworkThreshold" });

            settingsGui.AddItem("armorTiersRework", "armorTiersRework", "Features",
                settingsGui.CreateCheckbox(() => Settings.armorTiers, v => Settings.armorTiers = v, "Armor Tiers (Rework)"),
                35f,
                new List<string> { "armorTiersRework" });

            settingsGui.AddItem("armorDeflectionReworkThreshold", "armorDeflectionReworkThreshold", "Features",
                settingsGui.CreateSlider(() => Settings.armorDeflectionReworkThreshold, v => Settings.armorDeflectionReworkThreshold = v, 0f, 1f, () => $"Damage Leakage Threshold: {(Settings.armorDeflectionReworkThreshold * 100f):F0}%", 0.05f),
                60f,
                new List<string> { "armorDeflectionRework" });
            settingsGui.AddConditional("armorDeflectionReworkThreshold", () => Settings.armorDeflectionRework);

            settingsGui.AddItem("sharpConversionMode", "sharpConversionMode", "Features",
                settingsGui.CreateRadio(
                    () => (int)Settings.sharpConversionMode,
                    i => Settings.sharpConversionMode = (CombatTweaksModSettings.SharpConversionMode)i,
                    new[] { "Vanilla", "Only Metal Dulls", "Set Armor Threshold" }, "When to convert Sharp to Blunt Damage"),
                110f,
                new List<string> { "sharpToBluntThreshold" });

            settingsGui.AddItem("sharpToBluntThreshold", "sharpToBluntThreshold", "Features",
                settingsGui.CreateSlider(() => Settings.sharpToBluntThreshold, v => Settings.sharpToBluntThreshold = v, 0f, 2f, () => $"Convert Threshold: {(Settings.sharpToBluntThreshold * 100f):F0}%", 0.05f),
                60f,
                new List<string> { "sharpConversionMode" });
            settingsGui.AddConditional("sharpToBluntThreshold", () => Settings.sharpConversionMode == CombatTweaksModSettings.SharpConversionMode.ArmorThreshold);

            settingsGui.AddItem("finalArmorCalculations", "finalArmorCalculations", "Features",
                settingsGui.CreateRadio(
                    () => (int)Settings.finalArmorCalculations,
                    i => Settings.finalArmorCalculations = (CombatTweaksModSettings.FinalArmorCalculations)i,
                    new[] { "Vanilla", "Custom", "Yayo" }, "Final Armor Calculations"),
                110f);

            settingsGui.AddCategory("Tweaks");
            settingsGui.AddItem("maxArmorAmount", "maxArmorAmount", "Tweaks",
                settingsGui.CreateSlider(() => Settings.maxArmorAmount, v => Settings.maxArmorAmount = v, 0f, 20.1f,
                    () => Settings.maxArmorAmount > 20f ? "Max Armor Amount: Infinite" : $"Max Armor Amount: {(Settings.maxArmorAmount * 100f):F0}%", 0.1f),
                60f);
            settingsGui.AddItem("armorStr", "armorStr", "Tweaks",
                settingsGui.CreateSlider(() => Settings.armorStr, v => Settings.armorStr = v, 0f, 5f, () => $"Armor Strength: {formatBuff(Settings.armorStr)}", 0.05f),
                60f);
            settingsGui.AddItem("wepRange", "wepRange", "Tweaks",
                settingsGui.CreateSlider(() => Settings.wepRange, v => Settings.wepRange = v, 0f, 5f, () => $"Weapon Range Modifier: {formatBuff(Settings.wepRange)}", 0.01f),
                60f);
            settingsGui.AddItem("bulletSpread", "bulletSpread", "Tweaks",
                settingsGui.CreateSlider(() => Settings.bulletSpread, v => Settings.bulletSpread = v, 0f, 2f, () => $"Projectile Spread Amount: {(Settings.bulletSpread * 100):F0}%", 0.01f),
                60f);
            settingsGui.AddItem("burstSizeMultiplier", "burstSizeMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.burstSizeMultiplier, v => Settings.burstSizeMultiplier = v, 0.1f, 8f, () => $"Burst Size (*Requires Reloading Current Save*): {(formatBuff(Settings.burstSizeMultiplier))}", 0.1f),
                60f);
            settingsGui.AddItem("skillsEffectBulletSpread", "skillsEffectBulletSpread", "Tweaks",
                settingsGui.CreateCheckbox(() => Settings.skillsEffectBulletSpread, v => Settings.skillsEffectBulletSpread = v, "Scale with skill level"),
                55f,
                new List<string> { "bulletSpread" });
            settingsGui.AddConditional("skillsEffectBulletSpread", () => Settings.bulletSpread > 0.0001f);

            settingsGui.AddItem("armorPenImportance", "armorPenImportance", "Tweaks",
                settingsGui.CreateSlider(() => Settings.armorPenImportance, v => Settings.armorPenImportance = v, 0f, 5f, () => $"Armor Penetration Importance: {formatBuff(Settings.armorPenImportance)}", 0.05f),
                60f);
            settingsGui.AddItem("friendlyFireMultiplier", "friendlyFireMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.friendlyFireMultiplier, v => Settings.friendlyFireMultiplier = v, 0.05f, 5f, () => $"Friendly Fire Damage Multiplier: {formatBuff(Settings.friendlyFireMultiplier)}", 0.05f),
                60f);
            settingsGui.AddItem("enemyDamageMultiplier", "enemyDamageMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.enemyDamageMultiplier, v => Settings.enemyDamageMultiplier = v, 0.05f, 5f, () => $"Enemy Damage Boost: {formatBuff(Settings.enemyDamageMultiplier)}", 0.05f),
                60f);
            settingsGui.AddItem("deflectionChanceMultiplier", "deflectionChanceMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.deflectionChanceMultiplier, v => Settings.deflectionChanceMultiplier = v, 0.05f, 5f, () => $"Armor Deflection Chance Multiplier: {formatBuff(Settings.deflectionChanceMultiplier)}", 0.05f),
                60f);
            settingsGui.AddItem("reductionChanceMultiplier", "reductionChanceMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.reductionChanceMultiplier, v => Settings.reductionChanceMultiplier = v, 0.05f, 5f, () => $"Armor Damage Reduction Chance Multiplier: {formatBuff(Settings.reductionChanceMultiplier)}", 0.05f),
                60f);
            settingsGui.AddItem("reductionAmountMultiplier", "reductionAmountMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.reductionAmountMultiplier, v => Settings.reductionAmountMultiplier = v, 0.01f, 1f, () => $"Armor Damage Reduction Multiplier: {(Settings.reductionAmountMultiplier * 100):F0}%", 0.01f),
                60f);
            settingsGui.AddItem("damageBoost", "damageBoost", "Tweaks",
                settingsGui.CreateSlider(() => Settings.damageBoost, v => Settings.damageBoost = v, 0.1f, 10f, () => $"Damage Boost: {formatBuff(Settings.damageBoost)}", 0.1f),
                60f);
            settingsGui.AddItem("meleeDamageBoost", "meleeDamageBoost", "Tweaks",
                settingsGui.CreateSlider(() => Settings.meleeDamageBoost, v => Settings.meleeDamageBoost = v, 0.1f, 10f, () => $"Melee Damage Boost: {formatBuff(Settings.meleeDamageBoost)}", 0.1f),
                60f);
            settingsGui.AddItem("rangedDamageBoost", "rangedDamageBoost", "Tweaks",
                settingsGui.CreateSlider(() => Settings.rangedDamageBoost, v => Settings.rangedDamageBoost = v, 0.1f, 10f, () => $"Ranged Damage Boost: {formatBuff(Settings.rangedDamageBoost)}", 0.1f),
                60f);
            settingsGui.AddItem("itemDurabilityMultiplier", "itemDurabilityMultiplier", "Tweaks",
                settingsGui.CreateSlider(() => Settings.itemDurabilityMultiplier, v => Settings.itemDurabilityMultiplier = v, 0f, 10f, () => $"Durability Loss Multiplier (All): {formatBuff(Settings.itemDurabilityMultiplier)}", 0.1f),
                60f);
            settingsGui.AddItem("mechanoidBuff", "mechanoidBuff", "Tweaks",
                settingsGui.CreateSlider(() => Settings.mechanoidBuff, v => Settings.mechanoidBuff = v, 0.1f, 10f, () => $"Mechanoid Armor Buff: {formatBuff(Settings.mechanoidBuff)}", 0.1f),
                60f);
            settingsGui.AddItem("anomalyBuff", "anomalyBuff", "Tweaks",
                settingsGui.CreateSlider(() => Settings.anomalyBuff, v => Settings.anomalyBuff = v, 0.1f, 10f, () => $"Anomaly Armor Buff: {formatBuff(Settings.anomalyBuff)}", 0.1f),
                60f);
            settingsGui.AddItem("insectBuff", "insectBuff", "Tweaks",
                settingsGui.CreateSlider(() => Settings.insectBuff, v => Settings.insectBuff = v, 0.1f, 10f, () => $"Insect Armor Buff: {formatBuff(Settings.insectBuff)}", 0.1f),
                60f);
            settingsGui.AddItem("humanLikeBuff", "humanLikeBuff", "Tweaks",
                settingsGui.CreateSlider(() => Settings.humanLikeBuff, v => Settings.humanLikeBuff = v, 0.1f, 10f, () => $"Human-Like Armor Buff: {formatBuff(Settings.humanLikeBuff)}", 0.1f),
                60f);
            settingsGui.AddItem("bounusHeatDamageToDurability", "bounusHeatDamageToDurability", "Tweaks",
                settingsGui.CreateSlider(() => Settings.bounusHeatDamageToDurability, v => Settings.bounusHeatDamageToDurability = v, 0.1f, 10f, () => $"Durability Loss Multiplier (From Heat Damage): {formatBuff(Settings.bounusHeatDamageToDurability)}", 0.1f),
                60f);

            settingsGui.AddCategory("Presets");
            settingsGui.AddItem("presetButtons", "presetButtons", "Presets", rect =>
            {
                float buttonWidth = 180f;
                float y = rect.y;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Vanilla Rimworld"))
                {
                    Settings.ResetToVanilla();
                }
                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Yayo's Combat 3 Defaults"))
                {
                    Settings.ResetToYayo();
                }
                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Default / Modder's Choice"))
                {
                    Settings.ResetToDefaults();
                }
                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Random"))
                {
                    Settings.Random();
                }
            }, height: 150f);

            settingsGui.AddDescription("debugLogging", "Enables some detailed log messages for troubleshooting and debugging. I made this to confirm certain features were working while I made the mod, but I changed the what is logged a lot throughout making this mod, so, it might not be very helpful.");
            settingsGui.AddDescription("doTracking", "This will add a running average of damage for weapons and damage reduction for armor then store them in the item stats. This might have a performance effect, especially with large raids, so I would disable it if your not using it.");
            settingsGui.AddDescription("showDetails", "Toggles the visibility of descriptions under each mod setting.");
            settingsGui.AddDescription("difficultyBar", "An attempt to show how difficult the game will be after the Tweaks. (may not be super accurate)");

            settingsGui.AddDescription("yayoCombatCompatEnableYayoAAA", "Enables patching for Yayo's 'Advanced Armor Algorithms' system.\nYou do not need to have Yayo's Combat 3 in your modlist to use this.");
            settingsGui.AddDescription("yayoCombatCompat_s_armorEf", "Multiplier that overrides armor effectiveness when Yayo compatibility is enabled.");

            settingsGui.AddDescription("allArmorEffectsEverywhere", "This will simplify armor even more, if you want that. I would recommend weakening armor if you enable this since every single piece of armor is used to protect the wearer.");
            settingsGui.AddDescription("notArmor", "This will ignore armor if the armor % is under a certain threshold. If you disable durability loss too it, it can prevent your colonist from losing all their clothes when the durability loss settings are boosted.");
            

            settingsGui.AddDescription("durabilityEffectsArmor", "Makes armor protection scale based on the item's remaining durability.");
            settingsGui.AddDescription("thickArmor", "Uses the logic from the Thick Armor Mod. Armor will do its calculations an extra time for each layer it covers (Outer/Middle/Skin/etc), but will have a lesser effect each subsequent time after the first.");
            settingsGui.AddDescription("thickArmorEffectivenessLoss", "Each layer after the first will be reduce their effectiveness by this amount. For example if this is set to 75% and the armor covers 3 layers. If the armor had an armor rating of 100% the second layer would use an armor rating of 75% and the third would be 56.25%.");
            settingsGui.AddDescription("durabilityEffectsArmorMult", "This will effect a multiplier on how fast armor losses effectiveness depending on durability.\nThe math for it is: armorRating *= 1f - ((1f - (currentHP / maxHP)) * thisSetting);");
            settingsGui.AddDescription("durabilityEffectsArmorDoTattered", "If an item is less than 50% hitpoints it will lose effectiveness 3x faster");
            settingsGui.AddDescription("doDamageEffect", "Makes damage reduction of armor scale with damage.\nMakes single target high damage attacks stronger.");
            settingsGui.AddDescription("reliableArmors", "This will use the calculations from the \"Reliable Armors\" Mod. If deflect does not happen, the damage is reduced by a random percentage between 35% and 100% of the remaining armor value (after penetration). For example, if the target has 80% armor and the weapon has 30% penetration, the final armor is 80 - 30 = 50%, and damage will be reduced between 17.5% and 50%.");
            settingsGui.AddDescription("alwaysReduceDamage", "Forces all armor to reduce at least a small portion of incoming damage, as long as the effective armor is above 0.");
            settingsGui.AddDescription("doAdrenaline", "Has a chance to add a custom 'Adrenaline' hediff if a pawn takes damage and has more than 15% pain.");
            settingsGui.AddDescription("doTechLevelEffect", "Applies a scaling bonus to armor and armor penetration based on the difference in tech levels.");
            settingsGui.AddDescription("techLevelBoost", "Amount of armor bonus per difference in tech levels.");

            settingsGui.AddDescription("durabilityTypeInteraction", "Makes blunt damage increase the durability loss of metal armor, and makes sharp damage do more damage to the durability of non-metal armor.");
            settingsGui.AddDescription("durabilityTypeInteractionMultiplier", "Multiplier applied to damage when a weak damage type is used against the armor material.");

            settingsGui.AddDescription("armorTiersRework", "An attempt to make a CE-like armor calculations. This will assign a tier to each piece of armor and weapon. the math to calculate damage is:\n" +
                "Deflection chance = (2 + ArmorTier - WepTier) * 0.25\nReduction chance = 2 + ArmorTier - WepTier * 0.5;\nWARNING: Since this is a rework it might override some of the other settings you use!");

            settingsGui.AddDescription("armorDeflectionRework", "Reworks armor to always deflect damage at the cost of absorting the damage into the armor itself. " +
               "But after a certain threshold the damage will be reduced instead of being deflected. When the damage is reduced the armor will absorb its armor health percentage of the damage and leak the rest.\nWARNING: Since this is a rework it might override some of the other settings you use!");
            settingsGui.AddDescription("armorDeflectionReworkThreshold", "The armor health percentage where damage stops fully being deflected/absorbed");
            settingsGui.AddDescription("sharpConversionMode", "Controls how sharp damage gets converted to blunt based on material or thresholds.");
            settingsGui.AddDescription("sharpToBluntThreshold", "When the sharp armor value is above this percentage, incoming damage will be converted to blunt.");
            settingsGui.AddDescription("finalArmorCalculations", "Determines which system is used to finalize armor mitigation (vanilla, custom, or Yayo's logic). The 'custom' option will work like vanilla, except it will weaken armor penetration as it passes through the armor. It will also increase the chance to deflect sharp damage and make blunt damage lose its armor penetration extremely quickly.\nNote: BOTH rework settings ignore this setting if they are enabled");

            settingsGui.AddDescription("maxArmorAmount", "The cap on armor value");
            settingsGui.AddDescription("armorPenImportance", "Scales how much armor penetration reduces armor effectiveness.");
            settingsGui.AddDescription("armorStr", "A flat multiplier applied to armor to increase its strength.");
            settingsGui.AddDescription("wepRange", "A flat multiplier applied to the distance ranged weapons can shoot. This will only apply the effect after you restart your game. If the range exceeds 63 vanilla rimworld stops making the \"range circle thingy\". So, i made a custom one to show up after that point but it doesn't look quite the same as vanilla.");
            settingsGui.AddDescription("bulletSpread", "This changes the spread of projectiles that miss a target. It's set as a percentage of 90 degrees, so for example: 20% = 18 degrees or 200% = 180 degrees. If you set this to 0% it will just use the normal vanilla calculations.");
            settingsGui.AddDescription("burstSizeMultiplier", "This changes the burst size of weapons that shoot in bursts. It will round to the nearest whole number. \nWARNING: This will not apply mid game, you will need to exit to the main menu then reload a save for it to apply. (It will effect the visual 'Burst shot count' mid game, but will not actually update current existing weapons unless you reload the save)");
            settingsGui.AddDescription("skillsEffectBulletSpread", "Depending on the skill of the shooter it will apply a multiplier between 3 (at shooting level 0) and 0.33 (at shooting level 19+) to the spread amount set above.");
            settingsGui.AddDescription("deflectionChanceMultiplier", "Increases the chance for armor to deflect damage.");
            settingsGui.AddDescription("reductionChanceMultiplier", "Increases the chance for armor to reduce damage if it didn't deflect.");
            settingsGui.AddDescription("reductionAmountMultiplier", "The base reduction of damage when armor reduces damage.");
            settingsGui.AddDescription("enemyDamageMultiplier", "Flat multiplier applied to all incoming damage before armor calculations, but it only applies to enemies (Things from non-allied/non-neutral factions).");
            settingsGui.AddDescription("friendlyFireMultiplier", "Flat multiplier applied to all incoming damage before armor calculations, but it only applies to your colonists hitting each other.");
            settingsGui.AddDescription("damageBoost", "Flat multiplier applied to all incoming damage before armor calculations.");
            settingsGui.AddDescription("meleeDamageBoost", "Flat multiplier applied to all incoming damage before armor calculations from melee damage.");
            settingsGui.AddDescription("rangedDamageBoost", "Flat multiplier applied to all incoming damage before armor calculations from ranged damage.");
            settingsGui.AddDescription("itemDurabilityMultiplier", "Multiplier for how fast items lose durability when taking damage.");
            settingsGui.AddDescription("bounusHeatDamageToDurability", "Multiplier for how fast items lose durability when taking damage, but only for heat damage.");
            settingsGui.AddDescription("mechanoidBuff", "Buff to armor values for mechanoid pawns.");
            settingsGui.AddDescription("insectBuff", "Buff to armor values for inscect pawns.");
            settingsGui.AddDescription("anomalyBuff", "Buff to armor values for anomaly related pawns.");
            settingsGui.AddDescription("humanLikeBuff", "Buff to armor values for human-like pawns (should include baseliners and most other human xenotypes).");
        }

        public static string formatBuff(float percent, float netural = 1f)
        {
            if (percent == netural)
            {
                return "0% Buff / Vanilla";
            }
            else if (percent > netural)
            {
                return $"{(percent - netural) * 100:F0}% Buff";
            }
            else
            {
                return $"{(netural - percent) * 100:F0}% Nerf";
            }
        }

        public static void DrawDifficultyBar(Rect rect, float percent)
        {
            percent = Mathf.Clamp01(percent);

            Widgets.DrawBoxSolid(rect, new Color(0.6f, 0.6f, 0.6f));

            Color fillColor;
            if (percent >= 0.99f)
                fillColor = new Color(0.5f, 0f, 0f);
            else if (percent >= 0.66f)
                fillColor = Color.red;
            else if (percent >= 0.33f)
                fillColor = new Color(1f, 0.5f, 0f);
            else
                fillColor = Color.green;

            Rect fillRect = new Rect(rect.x, rect.y, rect.width * percent, rect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"{Mathf.RoundToInt(percent * 100)}% Difficulty");
            Text.Anchor = TextAnchor.UpperLeft;
        }



    }

    public class CombatTweaksModSettings : ModSettings
    {
        public enum SharpConversionMode
        {
            Vanilla,
            OnlyMetal,
            ArmorThreshold
        }
        public enum FinalArmorCalculations
        {
            Vanilla,
            Custom,
            Yayo
        }
        public HashSet<string> favorites = null;
        public SharpConversionMode sharpConversionMode = SharpConversionMode.ArmorThreshold;
        public FinalArmorCalculations finalArmorCalculations = FinalArmorCalculations.Custom;
        public bool yayoCombatCompat = false;
        public bool reliableArmors = false;
        public bool doTracking = false;
        public bool debugLogging = false;
        public bool durabilityEffectsArmor = true;
        public bool durabilityEffectsArmorDoTattered = true;
        public bool doDamageEffect = true;
        public bool alwaysReduceDamage = true;
        public bool doTechLevelEffect = true;
        public bool showDetails = true;
        public bool durabilityTypeInteraction = true;
        public bool armorDeflectionRework = false;
        public bool yayoCombatCompatEnableYayoAAA = false;
        public bool thickArmor = false;
        public bool armorTiers = false;
        public bool doAdrenaline = false;
        public bool allArmorEffectsEverywhere = false;
        public bool notArmor = true;
        public bool notArmorStillTakeDamage = true;
        public bool skillsEffectBulletSpread = true;
        public float thickArmorEffectivenessLoss = 0.5f;
        public float notArmorThreshold = 0.12f;
        public float durabilityEffectsArmorMult = 0.004f;
        public float adrenalineChance = 0.1f;
        public float anomalyBuff = 1.0f;
        public float armorStr = 1.0f;
        public float insectBuff = 1.0f;
        public float friendlyFireMultiplier = 1.0f;
        public float wepRange = 1.0f;
        public float lastRangeBuff = 1.0f;
        public float lastArmorCap = 2.0f;
        public float enemyDamageMultiplier = 1.0f;
        public float bulletSpread = 0.0f;
        public float deflectionChanceMultiplier = 1.0f;
        public float reductionChanceMultiplier = 1.0f;
        public float reductionAmountMultiplier = 0.5f;
        public float bounusHeatDamageToDurability = 2.0f;
        public float sharpToBluntThreshold = 0.2f;
        public float yayoCombatCompat_s_armorEf = 0.5f;
        public float techLevelBoost = 0.1f;
        public float maxArmorAmount = 20.1f;
        public float armorPenImportance = 1.0f;
        public float itemDurabilityMultiplier = 1.2f;
        public float mechanoidBuff = 1.2f;
        public float humanLikeBuff = 1.0f;
        public float damageBoost = 1.0f;
        public float meleeDamageBoost = 1.0f;
        public float rangedDamageBoost = 1.0f;
        public float durabilityTypeInteractionMultiplier = 1.5f;
        public float armorDeflectionReworkThreshold = 0.5f;
        public float burstSizeMultiplier = 1.0f;
        public float lastBurstSizeMultiplier = 1.0f;




        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sharpConversionMode, "sharpConversionMode", SharpConversionMode.ArmorThreshold);
            Scribe_Values.Look(ref finalArmorCalculations, "finalArmorCalculations", FinalArmorCalculations.Custom);
            Scribe_Values.Look(ref sharpToBluntThreshold, "sharpToBluntThreshold", 0.2f);
            Scribe_Values.Look(ref yayoCombatCompat, "yayoCombatCompat", false);
            Scribe_Values.Look(ref reliableArmors, "reliableArmor", true);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref doTracking, "doTracking", false);
            Scribe_Values.Look(ref durabilityEffectsArmor, "durabilityEffectsArmor", true);
            Scribe_Values.Look(ref durabilityEffectsArmorDoTattered, "durabilityEffectsArmorDoTattered", true);
            Scribe_Values.Look(ref doDamageEffect, "doDamageEffect", true);
            Scribe_Values.Look(ref doTechLevelEffect, "doTechLevelEffect", true);
            Scribe_Values.Look(ref alwaysReduceDamage, "alwaysReduceDamage", true);
            Scribe_Values.Look(ref showDetails, "showDetails", true);
            Scribe_Values.Look(ref durabilityTypeInteraction, "durabilityTypeInteraction", true);
            Scribe_Values.Look(ref armorDeflectionRework, "armorDeflectionRework", false);
            Scribe_Values.Look(ref yayoCombatCompatEnableYayoAAA, "yayoCombatCompatEnableYayoAAA", false);
            Scribe_Values.Look(ref allArmorEffectsEverywhere, "allArmorEffectsEverywhere", false);
            Scribe_Values.Look(ref notArmor, "notArmor", false);
            Scribe_Values.Look(ref notArmorStillTakeDamage, "notArmorStillTakeDamage", true);
            Scribe_Values.Look(ref armorTiers, "armorTiers", true);
            Scribe_Values.Look(ref thickArmor, "thickArmor", false);
            Scribe_Values.Look(ref doAdrenaline, "doAdrenaline", true);
            Scribe_Values.Look(ref skillsEffectBulletSpread, "skillsEffectBulletSpread", true);
            Scribe_Values.Look(ref maxArmorAmount, "maxArmorAmount", 20.1f);
            Scribe_Values.Look(ref lastArmorCap, "lastArmorCap", 20.1f);
            Scribe_Values.Look(ref armorPenImportance, "armorPenImportance", 1.0f);
            Scribe_Values.Look(ref itemDurabilityMultiplier, "itemDurabilityMultiplier", 1.5f);
            Scribe_Values.Look(ref techLevelBoost, "techLevelBoost", 0.1f);
            Scribe_Values.Look(ref mechanoidBuff, "mechanoidBuff", 1.2f);
            Scribe_Values.Look(ref damageBoost, "damageBoost", 1.0f);
            Scribe_Values.Look(ref durabilityTypeInteractionMultiplier, "durabilityTypeInteractionMultiplier", 1.5f);
            Scribe_Values.Look(ref armorDeflectionReworkThreshold, "armorDeflectionReworkThreshold", 0.5f);
            Scribe_Values.Look(ref yayoCombatCompat_s_armorEf, "yayoCombatCompat_s_armorEf", 0.5f);
            Scribe_Values.Look(ref bounusHeatDamageToDurability, "bounusHeatDamageToDurability", 2f);
            Scribe_Values.Look(ref notArmorThreshold, "notArmorThreshold", 0.12f);
            Scribe_Values.Look(ref anomalyBuff, "anomalyBuff", 1f);
            Scribe_Values.Look(ref insectBuff, "insectBuff", 1f);
            Scribe_Values.Look(ref humanLikeBuff, "humanLikeBuff", 1f);
            Scribe_Values.Look(ref wepRange, "wepRange", 1f);
            Scribe_Values.Look(ref lastRangeBuff, "lastRangeBuff", 1f);
            Scribe_Values.Look(ref friendlyFireMultiplier, "friendlyFireMultiplier", 1f);
            Scribe_Values.Look(ref enemyDamageMultiplier, "enemyDamageMultiplier", 1f);
            Scribe_Values.Look(ref deflectionChanceMultiplier, "deflectionChanceMultiplier", 1f);
            Scribe_Values.Look(ref reductionChanceMultiplier, "reductionChanceMultiplier", 1f);
            Scribe_Values.Look(ref reductionAmountMultiplier, "reductionAmountMultiplier", 0.5f);
            Scribe_Values.Look(ref armorStr, "armorStr", 1f);
            Scribe_Values.Look(ref bulletSpread, "bulletSpread", 0.1f);
            Scribe_Values.Look(ref meleeDamageBoost, "meleeDamageBoost", 1f);
            Scribe_Values.Look(ref rangedDamageBoost, "rangedDamageBoost", 1f);
            Scribe_Values.Look(ref adrenalineChance, "adrenalineChance", 0.1f);
            Scribe_Values.Look(ref durabilityEffectsArmorMult, "durabilityEffectsArmorMult", 0.004f);
            Scribe_Values.Look(ref thickArmorEffectivenessLoss, "thickArmorEffectivenessLoss", 0.5f);
            Scribe_Values.Look(ref burstSizeMultiplier, "burstSizeMultiplier", 1.0f);
            Scribe_Values.Look(ref lastBurstSizeMultiplier, "lastBurstSizeMultiplier", 1.0f);
            Scribe_Collections.Look(ref favorites, "favorites", LookMode.Value);

            if (favorites == null)
            {
                favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "doTracking",
                    "debugLogging",
                    "showDetails"
                };
            }

        }

        public void ResetToDefaults()
        {
            durabilityEffectsArmor = true;
            durabilityEffectsArmorDoTattered = true;
            doDamageEffect = true;
            doTechLevelEffect = true;
            alwaysReduceDamage = true;
            sharpConversionMode = SharpConversionMode.ArmorThreshold;
            finalArmorCalculations = FinalArmorCalculations.Custom;
            sharpToBluntThreshold = 0.2f;
            showDetails = true;
            doTracking = false;
            durabilityTypeInteraction = true;
            armorDeflectionRework = false;
            yayoCombatCompatEnableYayoAAA = false;
            allArmorEffectsEverywhere = false;
            notArmor = true;
            notArmorStillTakeDamage = true;
            armorTiers = false;
            reliableArmors = true;
            notArmorThreshold = 0.12f;
            anomalyBuff = 1.0f;
            doAdrenaline = true;
            thickArmor = true;
            skillsEffectBulletSpread = true;
            insectBuff = 1.0f;
            humanLikeBuff = 1.0f;
            armorStr = 1.0f;
            friendlyFireMultiplier = 1.0f;
            enemyDamageMultiplier = 1.0f;
            deflectionChanceMultiplier = 1.0f;
            bounusHeatDamageToDurability = 2.0f;
            reductionChanceMultiplier = 1.0f;
            adrenalineChance = 0.1f;
            wepRange = 1.0f;
            maxArmorAmount = 20.1f;
            mechanoidBuff = 1.2f;
            bulletSpread = 0.0f;
            armorPenImportance = 1f;
            itemDurabilityMultiplier = 1.5f;
            techLevelBoost = 0.1f;
            damageBoost = 1.0f;
            durabilityTypeInteractionMultiplier = 1.5f;
            reductionAmountMultiplier = 0.5f;
            armorDeflectionReworkThreshold = 0.5f;
            yayoCombatCompat_s_armorEf = 0.5f;
            meleeDamageBoost = 1.0f;
            rangedDamageBoost = 1.0f;
            durabilityEffectsArmorMult = 0.004f;
            thickArmorEffectivenessLoss = 0.5f;
            burstSizeMultiplier = 1.0f;
        }

        public void ResetToVanilla()
        {
            durabilityEffectsArmor = false;
            doDamageEffect = false;
            doTechLevelEffect = false;
            alwaysReduceDamage = false;
            durabilityTypeInteraction = false;
            sharpConversionMode = SharpConversionMode.Vanilla;
            finalArmorCalculations = FinalArmorCalculations.Vanilla;
            sharpToBluntThreshold = 0.0f;
            doTracking = false;
            armorDeflectionRework = false;
            yayoCombatCompatEnableYayoAAA = false;
            allArmorEffectsEverywhere = false;
            notArmor = false;
            notArmorStillTakeDamage = true;
            skillsEffectBulletSpread = true;
            thickArmor = false;
            armorTiers = false;
            reliableArmors = false;
            notArmorThreshold = 0.15f;
            anomalyBuff = 1.0f;
            insectBuff = 1.0f;
            humanLikeBuff = 1.0f;
            bulletSpread = 0.0f;
            doAdrenaline = false;
            friendlyFireMultiplier = 1.0f;
            bounusHeatDamageToDurability = 1.0f;
            adrenalineChance = 0.1f;
            armorStr = 1.0f;
            enemyDamageMultiplier = 1.0f;
            deflectionChanceMultiplier = 1.0f;
            reductionChanceMultiplier = 1.0f;
            reductionAmountMultiplier = 0.5f;
            maxArmorAmount = 2.0f;
            mechanoidBuff = 1.0f;
            armorPenImportance = 1.0f;
            wepRange = 1.0f;
            itemDurabilityMultiplier = 1.0f;
            techLevelBoost = 0.0f;
            damageBoost = 1.0f;
            durabilityTypeInteractionMultiplier = 0.0f;
            armorDeflectionReworkThreshold = 0.5f;
            yayoCombatCompat_s_armorEf = 0.5f;
            meleeDamageBoost = 1.0f;
            rangedDamageBoost = 1.0f;
            thickArmorEffectivenessLoss = 0.5f;
            burstSizeMultiplier = 1.0f;
        }

        public void ResetToYayo()
        {
            ResetToVanilla();
            yayoCombatCompatEnableYayoAAA = true;
            finalArmorCalculations = FinalArmorCalculations.Yayo;
            yayoCombatCompat_s_armorEf = 0.5f;
        }

        public void Random()
        {
            durabilityEffectsArmor = Rand.Value > 0.5f;
            doDamageEffect = Rand.Value > 0.5f;
            doTechLevelEffect = Rand.Value > 0.5f;
            alwaysReduceDamage = Rand.Value > 0.5f;
            durabilityTypeInteraction = Rand.Value > 0.5f;
            doTracking = Rand.Value > 0.5f;
            yayoCombatCompatEnableYayoAAA = Rand.Value > 0.5f;
            allArmorEffectsEverywhere = Rand.Value > 0.5f;
            notArmor = Rand.Value > 0.5f;
            notArmorStillTakeDamage = Rand.Value > 0.5f;
            doAdrenaline = Rand.Value > 0.5f;
            reliableArmors = Rand.Value > 0.5f;
            thickArmor = Rand.Value > 0.5f;

            sharpConversionMode = (SharpConversionMode)Rand.RangeInclusive(0, 2);

            finalArmorCalculations = (FinalArmorCalculations)Rand.RangeInclusive(0, 2);
            notArmorThreshold = RoundTo(Rand.Range(0f, 3f), 0.05f);
            anomalyBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            insectBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            humanLikeBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            bounusHeatDamageToDurability = RoundTo(Rand.Range(0f, 3f), 0.05f);
            friendlyFireMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            adrenalineChance = RoundTo(Rand.Range(0f, 1f), 0.01f);
            enemyDamageMultiplier = RoundTo(Rand.Range(0f, 1f), 0.05f);
            deflectionChanceMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            bulletSpread = RoundTo(Rand.Range(0f, 0.9f), 0.01f);
            reductionChanceMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            sharpToBluntThreshold = RoundTo(Rand.Range(0f, maxArmorAmount), 0.05f);
            maxArmorAmount = RoundTo(Rand.Range(0f, 20.1f), 0.1f);
            mechanoidBuff = RoundTo(Rand.Range(0.1f, 3f), 0.1f);
            armorPenImportance = RoundTo(Rand.Range(0f, 3f), 0.1f);
            wepRange = RoundTo(Rand.Range(0f, 3f), 0.1f);
            armorStr = RoundTo(Rand.Range(0f, 3f), 0.1f);
            itemDurabilityMultiplier = RoundTo(Rand.Range(0f, 3f), 0.1f);
            techLevelBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            damageBoost = RoundTo(Rand.Range(0.1f, 3f), 0.1f);
            durabilityTypeInteractionMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            armorDeflectionReworkThreshold = RoundTo(Rand.Range(0f, 1f), 0.05f);
            yayoCombatCompat_s_armorEf = RoundTo(Rand.Range(0f, 1f), 0.01f);
            meleeDamageBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            rangedDamageBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            reductionAmountMultiplier = RoundTo(Rand.Range(0.01f, 1f), 0.01f);
            burstSizeMultiplier = RoundTo(Rand.Range(0.1f, 8f), 0.1f);

            if (finalArmorCalculations == FinalArmorCalculations.Vanilla)
            {
                armorTiers = Rand.Value > 0.5f;
                armorDeflectionRework = !armorTiers && Rand.Value > 0.5f;
            }

        }

        public float CalculateDifficultyScore()
        {
            float score = 0.14f;

            if (durabilityEffectsArmor) score += 0.03f;
            if (doDamageEffect) score += 0.03f;
            if (alwaysReduceDamage) score += 0.03f;
            if (durabilityTypeInteraction) score += 0.03f;
            if (armorDeflectionRework) score += 0.03f;


            if (doTechLevelEffect)
                score += Mathf.Clamp01(techLevelBoost / 0.5f) * 0.05f;

            score -= (1f - Mathf.Clamp01(durabilityTypeInteractionMultiplier / 2f)) * 0.02f;
            score += Mathf.Clamp01(damageBoost / 2f) * 0.1f;
            score += Mathf.Clamp01(itemDurabilityMultiplier / 2f) * 0.05f;
            score += Mathf.Clamp01(armorPenImportance / 1.5f) * 0.05f;
            score += Mathf.Clamp01(durabilityTypeInteractionMultiplier / 2f) * 0.05f;

            if (armorDeflectionRework)
                score += Mathf.Clamp01(armorDeflectionReworkThreshold / 0.75f) * 0.05f;

            score += (mechanoidBuff - 1f) * 0.15f;
            score += (anomalyBuff - 1f) * 0.15f;
            score += (insectBuff - 1f) * 0.15f;
            score += (enemyDamageMultiplier - 1f) * 0.25f;
            score += (friendlyFireMultiplier - 1f) * 0.05f;

            if (sharpConversionMode == SharpConversionMode.ArmorThreshold)
                score += Mathf.Clamp01((maxArmorAmount - sharpToBluntThreshold) / maxArmorAmount) * 0.05f;

            return Mathf.Clamp01(score);
        }


        private float RoundTo(float val, float roundTo)
        {
            return Mathf.Round(val / roundTo) * roundTo;
        }


        public override string ToString()
        {
            return $"Mod Settings Variables: [durabilityEffectsArmor: {durabilityEffectsArmor}], [doDamageEffect: {doDamageEffect}], [doTechLevelEffect: {doTechLevelEffect}]," +
               $"[alwaysReduceDamage: {alwaysReduceDamage}], [durabilityTypeInteraction: {durabilityTypeInteraction}], [sharpConversionMode: {sharpConversionMode}], " +
               $"[finalArmorCalculations: {finalArmorCalculations}], [sharpToBluntThreshold: {sharpToBluntThreshold}], [showDetails: {showDetails}]," +
               $"[armorDeflectionRework: {armorDeflectionRework}], [yayoCombatCompatEnableYayoAAA: {yayoCombatCompatEnableYayoAAA}], [maxArmorAmount: {maxArmorAmount}]," +
               $"[mechanoidBuff: {mechanoidBuff}], [armorPenImportance: {armorPenImportance}], [itemDurabilityMultiplier: {itemDurabilityMultiplier}], " +
               $"[techLevelBoost: {techLevelBoost}], [damageBoost: {damageBoost}], [durabilityTypeInteractionMultiplier: {durabilityTypeInteractionMultiplier}], " +
               $"[armorDeflectionReworkThreshold: {armorDeflectionReworkThreshold}], [yayoCombatCompat_s_armorEf: {yayoCombatCompat_s_armorEf}]";
        }


    }
}

namespace Kasterfly.SettingsBuilder
{
    public class SettingsGUI
    {
        private Vector2 scrollPos;
        private string selectedCategory = null;
        private Dictionary<string, List<SettingItem>> categoryItems = new Dictionary<string, List<SettingItem>>();
        private Dictionary<string, string> descriptions = new Dictionary<string, string>();
        private Dictionary<string, Func<bool>> conditionals = new Dictionary<string, Func<bool>>();

        public void AddCategory(string category)
        {
            if (!categoryItems.ContainsKey(category))
                categoryItems[category] = new List<SettingItem>();

            if (selectedCategory == null)
                selectedCategory = category;
        }

        public void AddItem(string label, string key, string category, Action<Rect> drawFunc, float height = 35f, List<string> linkedKeys = null)
        {
            if (!categoryItems.ContainsKey(category))
                AddCategory(category);

            categoryItems[category].Add(new SettingItem
            {
                Key = key,
                Label = label,
                DrawFunc = drawFunc,
                Height = height,
                LinkedKeys = linkedKeys ?? new List<string>()
            });
        }

        public void AddDescription(string key, string description)
        {
            descriptions[key] = description;
        }

        public void AddConditional(string key, Func<bool> condition)
        {
            conditionals[key] = condition;
        }

        public void DrawSettings(Rect canvas)
        {
            if (CombatTweaksMod.Settings.favorites == null)
                CombatTweaksMod.Settings.favorites = new HashSet<string>();

            var leftRect = new Rect(canvas.x, canvas.y, 180f, canvas.height);
            var rightRect = new Rect(canvas.x + 185f, canvas.y, canvas.width - 190f, canvas.height);

            Widgets.BeginGroup(leftRect);
            float y = 0f;
            foreach (var cat in categoryItems.Keys)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 170f, 30f), cat))
                {
                    selectedCategory = cat;
                    scrollPos = Vector2.zero;
                }

                y += 35f;
            }

            Widgets.EndGroup();

            Widgets.DrawMenuSection(rightRect);
            Widgets.BeginScrollView(rightRect, ref scrollPos, new Rect(0f, 0f, rightRect.width - 20f, 4000f));
            float curY = 10f;

            if (selectedCategory != null && categoryItems.ContainsKey(selectedCategory))
            {
                Func<SettingItem, string> getId = si =>
                {
                    var idProp = si.GetType().GetField("Id") != null
                        ? (string)si.GetType().GetField("Id").GetValue(si)
                        : null;
                    return string.IsNullOrEmpty(idProp) ? si.Key : idProp;
                };
                Func<SettingItem, bool> isFav = si => CombatTweaksMod.Settings.favorites.Contains(getId(si));

                var itemsRaw = categoryItems[selectedCategory];
                var items = itemsRaw
                    .Select((si, index) => new { si, index })
                    .OrderByDescending(x => isFav(x.si))
                    .ThenBy(x => x.index)                    
                    .Select(x => x.si)
                    .ToList();


                var visited = new HashSet<string>();

                foreach (var item in items)
                {
                    if (visited.Contains(item.Key)) continue;

                    List<SettingItem> group = new List<SettingItem> { item };
                    visited.Add(item.Key);

                    foreach (var other in items)
                    {
                        if (visited.Contains(other.Key)) continue;
                        if (item.LinkedKeys.Contains(other.Key) || other.LinkedKeys.Contains(item.Key))
                        {
                            group.Add(other);
                            visited.Add(other.Key);
                        }
                    }

                    if (CombatTweaksMod.Settings.favorites == null)
                        CombatTweaksMod.Settings.favorites = new HashSet<string>();
                    List<string> groupIds = new List<string>();
                    for (int gi = 0; gi < group.Count; gi++)
                        groupIds.Add(getId(group[gi]));

                    int subIndex = 0;
                    foreach (var subItem in group) {
                        if (conditionals.TryGetValue(subItem.Key, out var cond) && !cond())
                            continue;

                        const float starW = 40f;
                        Rect itemRect = new Rect(10f, curY, rightRect.width - 30f, subItem.Height);
                        Rect starRect = new Rect(itemRect.x, itemRect.y, starW, itemRect.height);
                        Rect contentRect = new Rect(itemRect.x + starW + 4f, itemRect.y, itemRect.width - starW - 4f,
                            itemRect.height);

                        bool isLabel = (subIndex == 0);

                        bool groupFav = true;
                        for (int gi = 0; gi < groupIds.Count; gi++)
                        {
                            if (!CombatTweaksMod.Settings.favorites.Contains(groupIds[gi]))
                            {
                                groupFav = false;
                                break;
                            }
                        }

                        var oldAnchor = Text.Anchor;
                        var oldColor = GUI.color;

                        if (isLabel)
                        {
                            Text.Anchor = TextAnchor.MiddleCenter;
                            GUI.color = groupFav ? new Color(1f, 0.85f, 0.2f) : Color.white;

                            if (Widgets.ButtonText(starRect, groupFav ? "★" : "☆", drawBackground: false))
                            {
                                if (groupFav)
                                {
                                    for (int gi = 0; gi < groupIds.Count; gi++)
                                        CombatTweaksMod.Settings.favorites.Remove(groupIds[gi]);
                                }
                                else
                                {
                                    for (int gi = 0; gi < groupIds.Count; gi++)
                                        CombatTweaksMod.Settings.favorites.Add(groupIds[gi]);
                                }
                            }
                        }

                        GUI.color = oldColor;
                        Text.Anchor = oldAnchor;

                        subItem.DrawFunc?.Invoke(contentRect);
                        curY += subItem.Height;

                        if (descriptions.TryGetValue(subItem.Key, out var desc) && CombatTweaksMod.Settings.showDetails)
                        {
                            Rect descRect = new Rect(20f, curY, rightRect.width - 40f, 90f);
                            Widgets.Label(descRect, desc);
                            curY += 90f;
                        }

                        curY += 10f;
                        subIndex++;
                    }

                    Widgets.DrawLineHorizontal(10f, curY, rightRect.width - 30f);
                    curY += 10f;
                }

            }

            Widgets.EndScrollView();

            if (Mathf.Abs(CombatTweaksMod.Settings.lastArmorCap - CombatTweaksMod.Settings.maxArmorAmount) > 0.001f)
            {
                ArmorCapInitializer.ApplyArmorCapFromSettings();
                CombatTweaksMod.Settings.lastArmorCap = CombatTweaksMod.Settings.maxArmorAmount;
            }
            if (Mathf.Abs(CombatTweaksMod.Settings.lastRangeBuff - CombatTweaksMod.Settings.wepRange) > 0.001f)
            {
                VerbRangeMultiplierPatch.ApplyRangeBuffFromSettings();
                CombatTweaksMod.Settings.lastRangeBuff = CombatTweaksMod.Settings.wepRange;
            }
            if (Mathf.Abs(CombatTweaksMod.Settings.lastBurstSizeMultiplier - CombatTweaksMod.Settings.burstSizeMultiplier) > 0.001f)
            {
                VerbBurstMultiplierPatch.ApplyBurstBuffFromSettings();
                CombatTweaksMod.Settings.lastBurstSizeMultiplier = CombatTweaksMod.Settings.burstSizeMultiplier;
            }
        }


        public Action<Rect> CreateCheckbox(Func<bool> getter, Action<bool> setter, string label)
        {
            return rect =>
            {
                bool val = getter();
                Widgets.CheckboxLabeled(rect, label.Bold(), ref val);
                setter(val);
            };
        }

        public Action<Rect> CreateSlider(Func<float> getter, Action<float> setter, float min, float max, Func<string> label, float roundTo = 1f)
        {
            return rect =>
            {
                float val = getter();
                string labelText = label();

                Rect labelRect = new Rect(rect.x, rect.y, rect.width, 25f);
                Rect sliderRect = new Rect(rect.x, rect.y + 25f, rect.width, 30f);
                Widgets.Label(labelRect, labelText.Bold());
                val = Widgets.HorizontalSlider(sliderRect, val, min, max);


                val = Mathf.Round(val / roundTo) * roundTo;
                setter(val);
            };
        }

        public Action<Rect> CreateRadio(Func<int> getIndex, Action<int> setIndex, string[] labels, string groupLabel)
        {
            return rect =>
            {
                float y = rect.y;

                Widgets.Label(new Rect(rect.x, y, rect.width, 25f), groupLabel.Bold());
                y += 28f;

                for (int i = 0; i < labels.Length; i++)
                {
                    var radioRect = new Rect(rect.x, y, rect.width, 25f);
                    if (Widgets.RadioButtonLabeled(radioRect, labels[i], getIndex() == i))
                    {
                        setIndex(i);
                    }
                    y += 25f;
                }
            };
        }


        public Action<Rect> CreateInfo(Func<string> text)
        {
            return rect => Widgets.Label(rect, text());
        }

        private class SettingItem
        {
            public string Key;
            public string Label;
            public Action<Rect> DrawFunc;
            public float Height;
            public List<string> LinkedKeys;
        }
    }

    public static class GUIExtensions
    {
        public static string Bold(this string s)
        {
            return "<b>" + s + "</b>";
        }

        public static Rect LeftHalf(this Rect rect)
        {
            return new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
        }

        public static Rect RightHalf(this Rect rect)
        {
            return new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height);
        }
    }
}

namespace Kasterfly.CombatTweaks.MainFunctions
{
    public static class CombatTweaksArmorUtility
    {

        public static float GetPostArmorDamage(
             Pawn pawn,
             ref float damAmount,
             float armorPenetration,
             ref DamageDef damageDef,
             out bool deflectedByMetalArmor,
             out bool diminishedByMetalArmor,
             BodyPartRecord part,
             DamageInfo dinfo
        )
        {
            CombatTweaksModSettings settings = CombatTweaksMod.Settings;
            deflectedByMetalArmor = false;
            diminishedByMetalArmor = false;

            if (pawn == null || part == null || damageDef == null)
                return damAmount;

            bool isPlayerPawn = pawn.Faction == Faction.OfPlayer && pawn.IsColonist;
            bool isHumanlike = pawn.RaceProps.Humanlike;
            RaceProperties race = pawn.RaceProps;

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- GetPostArmorDamage Called");

            Thing instigatorThing = dinfo.Instigator;
            if (instigatorThing != null)
            {
                bool isPlayerDamage = instigatorThing.Faction == Faction.OfPlayer;
                bool isEnemyDamage = !instigatorThing.Faction.AllyOrNeutralTo(Faction.OfPlayer);

                if (isPlayerPawn && isPlayerDamage)
                    damAmount *= settings.friendlyFireMultiplier;
                else if (!isEnemyDamage)
                    damAmount *= settings.enemyDamageMultiplier;
            }

            damAmount *= damageDef.isRanged ? settings.rangedDamageBoost : settings.meleeDamageBoost;

            if (damageDef.armorCategory == null)
                return damAmount;

            float armorMult = settings.armorStr;
            float armorAdd = 0f;

            TechLevel targetBodyTech = TechLevel.Undefined;
            TechLevel attackTech = dinfo.Weapon?.techLevel ?? TechLevel.Industrial;

            if (race.IsMechanoid)
            {
                targetBodyTech = TechLevel.Ultra;
                armorAdd += (settings.mechanoidBuff-1f) / 10f;
                armorMult *= settings.mechanoidBuff;
            }
            else if (race.Insect)
            {
                armorAdd += (settings.insectBuff-1f) / 10f;
                armorMult *= settings.insectBuff;
            }
            else if (race.IsAnomalyEntity)
            {
                armorAdd += (settings.anomalyBuff-1f) / 10f;
                armorMult *= settings.anomalyBuff;
            }
            else if (isHumanlike)
            {
                armorAdd += (settings.humanLikeBuff-1f) / 10f;
                armorMult *= settings.humanLikeBuff;
            }

            armorPenetration *= settings.armorPenImportance;
            StatDef armorRatingStat = damageDef.armorCategory.armorRatingStat;

            float originalDamage = damAmount;
            int result = 0, highestResult = 0;
            TechLevel targetTech = TechLevel.Undefined;
            if (pawn.apparel != null)
            {
                var apparelList = pawn.apparel.WornApparel;
                for (int i = apparelList.Count - 1; i >= 0; i--)
                {
                    Apparel armor = apparelList[i];
                    if (armor.def.apparel.CoversBodyPart(part) || settings.allArmorEffectsEverywhere)
                    {
                        int layers = armor.def.apparel.layers?.Count ?? 0;
                        if (!settings.thickArmor)
                            layers = 1;
                        if (settings.debugLogging && settings.thickArmor)
                            Log.Message($"[CombatTweaks] Armor Layers: {layers}");
                        targetTech = armor.def.techLevel;
                        float before = damAmount;
                        float rating = armor.GetStatValue(armorRatingStat, true) * armorMult + armorAdd;
                        bool deflected2 = false;
                        bool metalArmor2 = false;
                        for (int x = 0; x < layers; x++)
                        {
                            damAmount = ApplyArmor(ref damAmount, ref armorPenetration, rating, armor, ref damageDef, pawn,
                                out bool metalArmor, out bool deflected, settings, dinfo, ref result, targetTech, attackTech);
                            deflected2 = deflected2 || deflected;
                            metalArmor2 = metalArmor2 || metalArmor;
                            rating *= settings.thickArmorEffectivenessLoss;
                            targetTech = TechLevel.Undefined;

                        }
                        if (CombatTweaksMod.Settings.doTracking)
                        {
                            var trackComp = armor.TryGetComp<CompCombatTracking>();
                            float percentReduced = (before - damAmount) / before;
                            if (trackComp != null)
                            {
                                trackComp.RecordEffect(percentReduced);
                                if (CombatTweaksMod.Settings.debugLogging)
                                {
                                    Log.Message($"[CombatTweaks] Armor {armor.LabelCap} tracked {percentReduced:P0} damage reduction. Total uses: {trackComp.TotalUses}");
                                }
                            }
                        }
                        if (result > highestResult)
                            highestResult = result;

                        if (damAmount < 0.001f)
                        {
                            deflectedByMetalArmor = deflected2 || metalArmor2;
                            damAmount = 0f;
                            break;
                        }

                        if (damAmount < before && deflected2)
                            diminishedByMetalArmor = true;

                        if (deflected2)
                            deflectedByMetalArmor = true;

                    }
                }
            }

            float finalBefore = damAmount;
            if (damAmount > 0f)
            {
                float rating = pawn.GetStatValue(armorRatingStat, true) * armorMult + armorAdd;
                damAmount = ApplyArmor(ref damAmount, ref armorPenetration, rating, null, ref damageDef, pawn,
                    out bool metalArmor, out bool deflected, settings, dinfo, ref result, targetBodyTech, attackTech);

                if (result > highestResult)
                    highestResult = result;

                if (damAmount < finalBefore && deflected)
                    diminishedByMetalArmor = true;

                if (deflected)
                    deflectedByMetalArmor = true;
            }

            if (
                highestResult != 2 &&
                isHumanlike &&
                pawn.health != null &&
                settings.doAdrenaline
            )
            {
                float pain = pawn.health.hediffSet.PainTotal;
                if (pain > 0.15f)
                {
                    var adrenaline = DefDatabase<HediffDef>.GetNamed("Adrenaline", false);
                    var crash = DefDatabase<HediffDef>.GetNamed("AdrenalineCrash", false);
                    if (adrenaline != null && crash != null &&
                        !pawn.health.hediffSet.HasHediff(adrenaline) &&
                        !pawn.health.hediffSet.HasHediff(crash))
                    {
                        pawn.health.AddHediff(adrenaline);
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Adrenaline triggered for {pawn.LabelShortCap} (pain: {pain * 100f:F0}%)");
                    }
                }
            }

            if (CombatTweaksMod.Settings.doTracking && instigatorThing != null && originalDamage > 0f)
            {
                Thing weaponThing = null;

                if (instigatorThing is Pawn p && p.equipment?.Primary != null)
                {
                    weaponThing = p.equipment.Primary;
                }
                else if (instigatorThing.def.IsWeapon)
                {
                    weaponThing = instigatorThing;
                }

                if (weaponThing != null)
                {
                    var weaponComp = weaponThing.TryGetComp<CompCombatTracking>();
                    float percentDealt = damAmount;

                    if (weaponComp != null)
                    {
                        weaponComp.RecordEffect(percentDealt);
                        if (CombatTweaksMod.Settings.debugLogging)
                        {
                            Log.Message($"[CombatTweaks] Weapon {weaponThing.LabelCap} tracked {percentDealt:P0} damage dealt. Total uses: {weaponComp.TotalUses}");
                        }
                    }
                }
            }


            if (damAmount < 0.001f || deflectedByMetalArmor)
            {
                damAmount = 0f;
                return 0f;
            }

            return damAmount;
        }


        public static float ApplyArmor(
            ref float damAmount,
            ref float armorPenetrationValue,
            float armorRating,
            Thing armorThing,
            ref DamageDef damageDef,
            Pawn pawn,
            out bool metalArmor,
            out bool forcedDefl,
            CombatTweaksModSettings settings,
            DamageInfo dinfo,
            ref int result,
            TechLevel targetTechLevel,
            TechLevel damageTechLevel
        )
        {
            metalArmor = false || pawn.RaceProps.IsMechanoid;
            forcedDefl = false;


            if (settings.notArmor && armorRating < settings.notArmorThreshold && !settings.notArmorStillTakeDamage)
            {
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- That's Not Armor! activated -Skipped...");

                result = 0;
                return damAmount;
            }

            int techLevelDiff = (int)targetTechLevel - (int)damageTechLevel;
            float techLevelMultiplier = (techLevelDiff * settings.techLevelBoost);
            bool flag = false;
            float armorPenetration = armorPenetrationValue;
            float yayoArmorPenetration = armorPenetration;
            float startDamage = damAmount;




            if (armorThing != null)
            {
                targetTechLevel = armorThing.def.techLevel;
                flag = true;
                bool flag2;
                if (!armorThing.def.apparel.useDeflectMetalEffect)
                {
                    ThingDef stuff = armorThing.Stuff;
                    flag2 = (stuff != null && stuff.IsMetal);
                }
                else
                {
                    flag2 = true;
                }

                metalArmor = flag2;


                if (targetTechLevel == TechLevel.Undefined && armorThing.Stuff != null)
                {
                    targetTechLevel = armorThing.Stuff.techLevel;
                }
            }

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- Before ApplyArmor() edits start: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                    $"[Target Pawn: {pawn?.LabelShortCap ?? "null"}], [Armor Item: {armorThing?.LabelCap ?? "natural"}], [Damage Type: {damageDef?.defName ?? "null"}]," +
                    $" [isMetal: {metalArmor}] | " + settings.ToString());


            if (settings.doTechLevelEffect)
            {
                armorRating *= (1 + techLevelMultiplier * 0.9f);
                armorPenetration *= (1 - techLevelMultiplier * 0.1f);
            }
            else if (settings.yayoCombatCompatEnableYayoAAA)
            {
                if (flag && dinfo.Weapon != null)
                {
                    if (armorThing.def.techLevel >= TechLevel.Spacer || pawn.RaceProps.IsMechanoid)
                    {
                        if (dinfo.Weapon.IsMeleeWeapon)
                        {
                            if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                            {
                                yayoArmorPenetration *= 0.5f;
                            }
                        }
                        else if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                        {
                            yayoArmorPenetration *= 0.35f;
                        }
                    }
                    else if (armorThing.def.techLevel >= TechLevel.Industrial)
                    {
                        if (dinfo.Weapon.IsMeleeWeapon)
                        {
                            if (dinfo.Weapon.techLevel <= TechLevel.Neolithic)
                            {
                                yayoArmorPenetration *= 0.75f;
                            }
                        }
                        else if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                        {
                            yayoArmorPenetration *= 0.5f;
                        }
                    }
                }
            }

            float yayoNum2 = Mathf.Max(armorRating - yayoArmorPenetration, 0f);
            float yayoNum3 = (yayoArmorPenetration - armorRating * 0.15f) * 5f;
            yayoNum3 = Mathf.Clamp01(yayoNum3);

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] (Durability Effects Armor) - PostTechLevel: [Armor Rating: {armorRating}], [armorPenetration: {armorPenetration}], [techLevelMultiplier: {techLevelMultiplier}]");
            float currentHP = 0;
            float maxHP = 0;
            if (armorThing != null)
            {
                currentHP = (float)armorThing.HitPoints;
                maxHP = (float)armorThing.MaxHitPoints;
            }

            if (settings.durabilityEffectsArmor)
            {
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] (Durability Effects Armor) - PreEffectInfo: [Armor Rating: {armorRating}]");

                float DEM  = settings.durabilityEffectsArmorMult*100;
                if (maxHP != 0)
                {
                    if (settings.durabilityEffectsArmorDoTattered && maxHP / 2 >= currentHP)
                        DEM *= 3;
                    if (currentHP / maxHP >= 0)
                    {
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Modified Stats (Durability Effects Armor) - Info: [Armor Rating: {armorRating}], Item health ratio: {currentHP / maxHP}");

                        armorRating *= Mathf.Clamp01(1f - ((1f - (currentHP / maxHP)) * DEM));
                    }
                    else
                    {
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Modified Stats (Durability Effects Armor) - Info: Skipped because durability does not exist");

                    }

                }
            }




            float DurabilityMultiplier = settings.itemDurabilityMultiplier;

            if (armorThing != null && !settings.armorDeflectionRework)
            {
               

                if (settings.durabilityTypeInteraction)
                {
                    bool isBlunt = damageDef == DamageDefOf.Blunt;
                    bool isSharp = damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp;

                    if ((isBlunt && metalArmor) || (isSharp && !metalArmor))
                    {
                        DurabilityMultiplier *= settings.durabilityTypeInteractionMultiplier;
                    }
                }

                if (damageDef == DamageDefOf.Flame || damageDef == DamageDefOf.Burn || damageDef.defName == "Heat" || damageDef.defName == "HotTemperature")
                {
                    DurabilityMultiplier *= settings.bounusHeatDamageToDurability;
                }

                float f = damAmount * 0.25f * DurabilityMultiplier;
                if (settings.yayoCombatCompatEnableYayoAAA)
                    f = damAmount * (0.2f + yayoNum3 * 0.5f) * DurabilityMultiplier;


                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Info: [Item Durability Loss Multiplier: {settings.itemDurabilityMultiplier}], [Item Durability Loss: 0-{f}]");


                if (f > 0.5f)
                    armorThing.TakeDamage(new DamageInfo(damageDef, (float)GenMath.RoundRandom(f)));

                if (settings.notArmor && armorRating < settings.notArmorThreshold)
                {
                    metalArmor = false;
                    forcedDefl = false;
                    result = 0;
                    return damAmount;

                }
            }

            float yayoNum4 = (!flag) ? pawn.health.summaryHealth.SummaryHealthPercent : ((float)armorThing.HitPoints / (float)armorThing.MaxHitPoints);
            float yayoNum5 = Mathf.Max(armorRating * 0.9f - yayoArmorPenetration, 0f);
            float yayoNum6 = 1f - settings.yayoCombatCompat_s_armorEf;

            if (settings.maxArmorAmount < 20.05f && armorRating > settings.maxArmorAmount)
            {
                armorRating = settings.maxArmorAmount;
            }
            float effectiveArmorRating = Mathf.Max(armorRating - armorPenetration, 0f);



            bool changeToBlunt = (metalArmor && settings.sharpConversionMode == SharpConversionMode.OnlyMetal) ||
                settings.sharpConversionMode == SharpConversionMode.Vanilla ||
                (effectiveArmorRating > settings.sharpToBluntThreshold && settings.sharpConversionMode == SharpConversionMode.ArmorThreshold);


            if (settings.armorDeflectionRework && armorThing != null)
            {
                float durabilityDamage = 0;
                if (effectiveArmorRating > 0f)
                {
                    durabilityDamage = damAmount * DurabilityMultiplier / Mathf.Max(effectiveArmorRating, 1f);
                }
                else
                {
                    durabilityDamage = damAmount * DurabilityMultiplier * (1 + armorPenetration);
                }


                if (currentHP > maxHP * settings.armorDeflectionReworkThreshold)
                {
                    damAmount = 0f;
                    result = 2;
                }
                else
                {
                    durabilityDamage *= currentHP / maxHP;
                    damAmount = (float)GenMath.RoundRandom(damAmount * currentHP / maxHP);
                    result = 1;

                }
                if (durabilityDamage > 0.5f)
                    armorThing.TakeDamage(new DamageInfo(damageDef, (float)GenMath.RoundRandom(durabilityDamage)));

                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                {
                    damageDef = DamageDefOf.Blunt;
                }
            }

            if (settings.armorDeflectionRework)
            {

                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ApplyArmor() ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                    $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");

                return damAmount;
            }

            float value = Rand.Value;
            float DeflectionModifier = 1.0f;
            float DeflectionCap = 1.0f;
            float newArmorPen = armorPenetration;

            float defChance = -999999998f;
            float redChance = -999999998f;
            if (settings.armorTiers)
            {
                int armorTier = (int)Math.Floor((-15.0f + Mathf.Sqrt(225.0f + 8.0f * (armorRating * 100))) / 4.0f) + 1;
                float discriminant = 3.5f * 3.5f - 4f * 0.75f * (0.75f - (armorPenetration * 100));
                if (discriminant < 0f)
                    discriminant = 0;
                int apTier = Mathf.FloorToInt((-3.5f + Mathf.Sqrt(discriminant)) / (2f * 0.75f));
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Armor Tiers Tier Checking: [Weapon Tier: T{apTier} - {armorPenetration * 100}%], [Armor Tier: T{armorTier} - {armorRating * 100}%]");

                if (armorTier - 1.01 > apTier)
                {
                    damAmount = 0;
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] (Guaranteed Deflection) ArmorUtility.ApplyArmor ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                        $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");
                    result = 2;
                    return 0;
                }
                else if (apTier - 1.01 > armorTier)
                {
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks]  (Guaranteed Full Damage) ArmorUtility.ApplyArmor ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                        $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");
                    result = 0;
                    return damAmount;
                }
                defChance = Mathf.Clamp01((2 + armorTier - apTier) * 0.25f);
                redChance = Mathf.Clamp01((2 + armorTier - apTier) * 0.5f);
            }
            else if (settings.finalArmorCalculations == FinalArmorCalculations.Yayo)
            {
                if (value * yayoNum6 < yayoNum5 * yayoNum4)
                {
                    if (Rand.Value < Mathf.Min(yayoNum2, 0.9f) * settings.deflectionChanceMultiplier)
                    {
                        damAmount = 0f;
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                                $" [isMetal: {metalArmor}]");
                        result = 2;
                        return damAmount;
                    }
                    if (flag)
                    {
                        forcedDefl = true;
                        damAmount = (float)GenMath.RoundRandom(damAmount * (0.25f + yayoNum3 * 0.25f));
                        if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                        {
                            damageDef = DamageDefOf.Blunt;
                        }
                    }
                    else
                    {
                        forcedDefl = true;
                        damAmount = (float)GenMath.RoundRandom(damAmount * (0.25f + yayoNum3 * 0.5f));
                        if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                        {
                            damageDef = DamageDefOf.Blunt;
                        }
                    }
                }
                else if (value < yayoNum2 * (0.5f + yayoNum4 * 0.5f) * settings.reductionChanceMultiplier)
                {
                    damAmount = (float)GenMath.RoundRandom(damAmount * 0.5f);
                    if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
                    {
                        damageDef = DamageDefOf.Blunt;
                    }
                }
                result = 2;
                if (settings.alwaysReduceDamage && effectiveArmorRating > 0f)
                {
                    damAmount = Mathf.Max(damAmount * 0.9f, damAmount - 3);
                    result = 1;
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] Modified Stats (Almost Always Reduce Damage) - Info: [Damage Amount: {damAmount}]");
                }
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                        $" [isMetal: {metalArmor}]");

                return damAmount;
            }
            else if (settings.finalArmorCalculations == FinalArmorCalculations.Custom)
            {
                newArmorPen = Math.Min(0.0f, armorPenetration - Math.Max(effectiveArmorRating * 0.25f, armorPenetration * 0.05f));
                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
                {
                    DeflectionModifier *= Mathf.Min(effectiveArmorRating * 0.5f * DeflectionModifier, DeflectionCap) * settings.deflectionChanceMultiplier;
                }
                else
                {
                    newArmorPen = Math.Min(0.0f, armorPenetration - Math.Max(effectiveArmorRating * 0.5f, armorPenetration * 0.1f));
                }
            }



            if (defChance < -9999f || redChance < -9999f)
            {
                defChance = Mathf.Min(effectiveArmorRating * 0.5f * DeflectionModifier, DeflectionCap);
                redChance = effectiveArmorRating;
            }
            defChance *= settings.deflectionChanceMultiplier;
            redChance *= settings.reductionChanceMultiplier;
            if (settings.reliableArmors) {
                redChance = 2f;
            }


            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] Info: [Deflection Chance: {defChance}], [damage reduction chance: {redChance}]");


            if (value < defChance)
            {
                damAmount = 0f;
                result = 2;
            }
            else if (value < redChance)
            {
                float damReduction = settings.reductionAmountMultiplier;
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount: {Mathf.Clamp01(damReduction) * 100:F0}%");

                if (settings.reliableArmors)
                {
                    damReduction *= (armorRating - armorPenetration) * Rand.Range(0.7f, 2f);
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount (After Reliable Armors): {Mathf.Clamp01(damReduction) * 100:F0}%");
                }

                if (settings.doDamageEffect && damAmount > 0f)
                {
                    if(armorPenetration < 0.01f)
                    {
                        armorPenetration = 0.01f;
                    }
                    damReduction *= Mathf.Clamp(((armorRating * 12) / (armorPenetration * damAmount)), 0.4f, 1.6f);
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount (Damage Effect): {Mathf.Clamp01(damReduction) * 100:F0}%");
                }

                

                damAmount *= (1-Mathf.Clamp01(damReduction));
                if (damAmount < 0.5f)
                    damAmount = 0f;

                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                {
                    damageDef = DamageDefOf.Blunt;
                }
                result = 1;
            } else if (settings.alwaysReduceDamage && effectiveArmorRating > 0f)
            {
                damAmount = Mathf.Min(Mathf.Max(damAmount * 1.8f * settings.reductionAmountMultiplier, damAmount * 0.95f), damAmount - 3);
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Modified Stats (Almost Always Reduce Damage) - Info: [Damage Amount: {damAmount}]");
            }
            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                    $" [isMetal: {metalArmor}]");
            armorPenetration = newArmorPen;
            return damAmount;
        }
    }

    public static class CustomArmorUtility
    {
        public static float CalculatePostArmorDamage(Pawn pawn, float amount, float armorPenetration, BodyPartRecord part, ref DamageDef damageDef, ref bool deflectedByMetalArmor, ref bool diminishedByMetalArmor, DamageInfo dinfoOrNull)
        {
            DamageInfo dinfo = new DamageInfo(new DamageInfo(damageDef, (float)GenMath.RoundRandom(amount)));
            if (dinfoOrNull is DamageInfo setDInfo)
            {
                dinfo = new DamageInfo(setDInfo);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- dinfo toString(): {dinfo.ToString()}");
            }
            else
            {
                dinfo.SetHitPart(part);
                dinfo.SetAmount(amount);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- dinfo is missing, creating a generic one instead");
            }
            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- DamageWorker_AddInjury Called");

            float result = CombatTweaksArmorUtility.GetPostArmorDamage(pawn, ref amount, armorPenetration, ref damageDef, out deflectedByMetalArmor, out diminishedByMetalArmor, part, dinfo);
            return result;

        }

    }
}

namespace Kasterfly.CombatTweaks.Hediff
{
    public class Adrenaline : HediffWithComps
    {
        public override void PostRemoved()
        {
            base.PostRemoved();

            if (pawn != null && !pawn.Dead && pawn.Map != null)
            {
                HediffDef crashDef = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineCrash");
                if (crashDef != null && !pawn.health.hediffSet.HasHediff(crashDef))
                {
                    pawn.health.AddHediff(crashDef);
                }
            }
        }
    }
}

namespace Kasterfly.CombatTweaks.Stats
{
    public class StatWorker_CombatTweaks : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return CombatTweaksMod.Settings.doTracking && req.HasThing;
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!CombatTweaksMod.Settings.doTracking || req.Thing == null)
                return 0f;

            var comp = req.Thing.TryGetComp<CompCombatTracking>();
            if (comp == null || comp.TotalUses <= 1 || comp.TrackingValue < 0.001f)
                return 0f;

            float avg = comp.TrackingValue / comp.TotalUses;

            if (req.Thing.def.IsApparel)
                return avg * 100f;
            else
                return avg;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (!CombatTweaksMod.Settings.doTracking)
                return "Stat tracking is disabled.";

            var comp = req.Thing?.TryGetComp<CompCombatTracking>();
            if (comp == null || comp.TotalUses <= 1)
                return "Not enough combat data recorded.";

            float average = comp.TotalUses > 0 ? comp.TrackingValue / comp.TotalUses : 0f;
            if (average < 0.005f)
                average = 0f;

            string usesString = comp.TotalUses > 10000 ? "over 10000" : comp.TotalUses.ToString();

            if (req.Thing.def.IsApparel)
                return $"Average damage prevented: {average * 100f:F1}% across {usesString} hits.";
            else
                return $"Average damage inflicted: {average:F1} points across {usesString} attacks.";
        }
    }
}

namespace Kasterfly.CombatTweaks.Comps
{
    public class CompProperties_CombatTracking : CompProperties
    {
        public CompProperties_CombatTracking()
        {
            this.compClass = typeof(CompCombatTracking);
        }
    }

    public class CompCombatTracking : ThingComp
    {
        private float totalEffect = 0f;
        private int uses = 0;

        public float TrackingValue => totalEffect;
        public int TotalUses => uses;

        public string Explanation => uses > 0
            ? $"Average effectiveness based on {uses} tracked instance(s)."
            : "Not enough combat data to evaluate item performance.";

        public void RecordEffect(float value)
        { 
            if (uses < 10000)
            {
                totalEffect += value;
                uses++;
            }
            else
                totalEffect += (value - totalEffect) * 0.001f;
            
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref totalEffect, "totalEffect", 0f);
            Scribe_Values.Look(ref uses, "uses", 0);
        }
    }
}

namespace Kasterfly.CombatTweaks.HarmonyPatches
{

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
    public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
    {
        [HarmonyPriority(Priority.First)]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            try
            {
                MethodInfo targetMethod = AccessTools.Method(typeof(ArmorUtility), "GetPostArmorDamage");
                MethodInfo replacementMethod = AccessTools.Method(typeof(CustomArmorUtility), "CalculatePostArmorDamage");

                if (targetMethod == null || replacementMethod == null)
                {
                    return instructions;
                }

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(targetMethod))
                    {
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                        i++;
                        codes[i] = new CodeInstruction(OpCodes.Call, replacementMethod);


                        break;
                    }
                }

                return codes;
            }
            catch (Exception ex)
            {
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Warning($"[CombatTweaks] Transpiler for ApplyDamageToPart failed. Reverting to vanilla. Error: {ex}");
                return instructions;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class ArmorCapInitializer
    {
        static ArmorCapInitializer()
        {
            ApplyArmorCapFromSettings();
        }

        public static void ApplyArmorCapFromSettings()
        {
            float settingValue = CombatTweaksMod.Settings.maxArmorAmount;
            float cap = settingValue > 20f ? float.PositiveInfinity : settingValue;
            StatDefOf.ArmorRating_Sharp.maxValue = cap;
            StatDefOf.ArmorRating_Blunt.maxValue = cap;
            StatDefOf.ArmorRating_Heat.maxValue  = cap;
            if(CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Updated armor cap to {Mathf.Round(cap*100)}%");
        }
    }
    

    [StaticConstructorOnStartup]
    public static class VerbBurstMultiplierPatch
    {
        private static float _lastMult = 1f;

        private static readonly Dictionary<string, int> _baselineBurstByKey = new Dictionary<string, int>(1024);
        private static bool _baselinesInitialized = false;

        static VerbBurstMultiplierPatch()
        {
            _lastMult = 1f;
            ApplyBurstBuffFromSettings();
        }

        public static void ApplyBurstBuffFromSettings()
        {
            float m = Mathf.Clamp(CombatTweaksMod.Settings.burstSizeMultiplier, 0.01f, 8f);

            if (!_baselinesInitialized)
            {
                InitializeBaselines();
                _baselinesInitialized = true;
            }

            if (Mathf.Approximately(m, _lastMult))
                return;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    if (vp.verbClass != typeof(Verb_Shoot)) continue;
                    if (vp.defaultProjectile == null) continue;

                    string key = MakeKey(def, i);

                    if (!_baselineBurstByKey.TryGetValue(key, out int baseline))
                    {
                        int estimatedBase = Mathf.Max(1, Mathf.RoundToInt(vp.burstShotCount / Mathf.Max(0.01f, _lastMult)));
                        _baselineBurstByKey[key] = baseline = estimatedBase;
                    }

                    int newValue = Mathf.Max(1, Mathf.RoundToInt(baseline * m));
                    vp.burstShotCount = newValue;
                }
            }

            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Burst sizes set to baseline × {m:0.###} (was × {_lastMult:0.###}).");

            _lastMult = m;
        }

        private static void InitializeBaselines()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    if (vp.verbClass != typeof(Verb_Shoot)) continue;
                    if (vp.defaultProjectile == null) continue;

                    string key = MakeKey(def, i);

                    int original = vp.burstShotCount;
                    if (original <= 0) original = 1;
                    _baselineBurstByKey[key] = original;
                }
            }

            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Cached {_baselineBurstByKey.Count} burst baselines.");
        }

        private static string MakeKey(ThingDef def, int verbIndex)
        {
            return $"{def.defName}#{verbIndex}";
        }
    }



    [StaticConstructorOnStartup]
    public static class VerbRangeMultiplierPatch
    {
        private static float _lastMult = 1f;

        static VerbRangeMultiplierPatch()
        {
            _lastMult = 1f;
            ApplyRangeBuffFromSettings();
        }
        public static void ApplyRangeBuffFromSettings()
        {
            float m = CombatTweaksMod.Settings.wepRange;
            if (Mathf.Approximately(m, _lastMult)) return;
            if (Mathf.Approximately(_lastMult, 0f))
            {
                Log.Warning("[CombatTweaks] Last multiplier was 0; ratio scaling needs a baseline. Consider the zero-safe variant.");
                _lastMult = 1f;
            }

            float ratio = (_lastMult == 0f) ? m : (m / _lastMult);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    vp.range *= ratio;
                }
            }

            _lastMult = m;
            if(CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Rescaled ranged weapon ranges by x{ratio:0.###} (now x{_lastMult:0.###} total)");
        }
    }



    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.DrawRadiusRing))]
    public static class VerbProperties_DrawRadiusRing_Patch
    {
        static bool Prefix(VerbProperties __instance, IntVec3 center, Verb verb = null)
        {
            if (Find.CurrentMap == null || __instance.IsMeleeAttack || !__instance.targetable)
                return false;

            float minRange = __instance.EffectiveMinRange(true);
            float maxRange = (verb != null) ? verb.EffectiveRange : __instance.range;

            if (minRange > 0f)
            {
                if (minRange <= GenRadial.MaxRadialPatternRadius)
                    GenDraw.DrawRadiusRing(center, minRange);
                else
                    DrawHollowRadiusRing(center, minRange, Color.white);
            }

            if (__instance.drawHighlightWithLineOfSight)
            {
                if (maxRange <= GenRadial.MaxRadialPatternRadius)
                {
                    GenDraw.DrawRadiusRing(center, maxRange, Color.white,
                        (IntVec3 c) => GenSight.LineOfSight(center, c, Find.CurrentMap));
                }
                else
                {
                    DrawHollowRadiusRing(center, maxRange, Color.white,
                        (IntVec3 c) => GenSight.LineOfSight(center, c, Find.CurrentMap));
                }
            }
            else
            {
                if (maxRange <= GenRadial.MaxRadialPatternRadius)
                    GenDraw.DrawRadiusRing(center, maxRange, Color.white);
                else
                    DrawHollowRadiusRing(center, maxRange, Color.white);
            }

            return false;
        }

        private static void DrawHollowRadiusRing(IntVec3 center, float radius, Color color, System.Func<IntVec3, bool> predicate = null)
        {
            Map map = Find.CurrentMap;
            float outerSq = radius * radius;
            float innerSq = (radius - 1f) * (radius - 1f);

            CellRect bounds = CellRect.CenteredOn(center, (int)(radius + 2));
            bounds.ClipInsideMap(map);

            List<IntVec3> edgeCells = new List<IntVec3>();
            foreach (var cell in bounds)
            {
                float distSq = (cell - center).LengthHorizontalSquared;
                if (distSq <= outerSq && distSq >= innerSq)
                {
                    if (predicate == null || predicate(cell))
                        edgeCells.Add(cell);
                }
            }

            if (edgeCells.Count > 0)
                GenDraw.DrawFieldEdges(edgeCells, color);
        }
    }






    [HarmonyPatch(typeof(Projectile), "Launch", new Type[] {
    typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
    typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
})]
    public static class Patch_Projectile_Launch_OverrideSpread
    {
        static void Prefix(
            ref LocalTargetInfo usedTarget,
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire,
            Thing equipment,
            ThingDef targetCoverDef
        )
        {
            float baseSpread = CombatTweaksMod.Settings.bulletSpread;
            if (baseSpread <= 0.0001f || launcher == null || !usedTarget.IsValid || !intendedTarget.IsValid)
                return;
            Verb verb = launcher is Pawn pawn ? pawn?.equipment?.PrimaryEq?.PrimaryVerb : null;
            if (verb == null)
            {
                return;
            }
            ShotReport report = ShotReport.HitReportFor(launcher, verb, intendedTarget);
            if (Rand.Chance(report.AimOnTargetChance))
            {
                return;
            }

            float maxAngle = Mathf.Min(baseSpread * 90f, 180f);
            if (CombatTweaksMod.Settings.skillsEffectBulletSpread && launcher is Pawn pawn2 && pawn2.skills != null)
                maxAngle = Mathf.Max(maxAngle / 3, maxAngle * (3 - pawn2.skills.GetSkill(SkillDefOf.Shooting).Level / 7));
            maxAngle = Mathf.Min(maxAngle, 180f);

            Vector3 originPos = origin;
            Vector3 targetPos = intendedTarget.HasThing
                ? intendedTarget.Thing.Position.ToVector3Shifted()
                : intendedTarget.Cell.ToVector3Shifted();

            float distance = Vector3.Distance(originPos, targetPos);
            Vector3 forward = (targetPos - originPos).normalized;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            Vector3 right = new Vector3(-forward.z, 0f, forward.x);

            float deviationAngle = Rand.Range(maxAngle * (-0.5f), maxAngle * 0.5f);

            float angleRad = deviationAngle * Mathf.Deg2Rad;
            float lateral = Mathf.Tan(Mathf.Abs(angleRad)) * distance;
            lateral = Mathf.Min(lateral, distance * 0.75f);
            lateral *= Mathf.Sign(deviationAngle);

            Vector3 missPoint = targetPos + right * lateral;
            missPoint += forward * Rand.Range(-1f, 1f);

            if (float.IsNaN(missPoint.x) || float.IsNaN(missPoint.y) || float.IsNaN(missPoint.z) ||
                float.IsInfinity(missPoint.x) || float.IsInfinity(missPoint.y) || float.IsInfinity(missPoint.z) || float.IsNaN(distance))
                return;

            usedTarget = new LocalTargetInfo(missPoint.ToIntVec3());

            if (CombatTweaksMod.Settings.debugLogging)
            {
                Log.Message(
                    $"[CombatTweaks] Forced MISS via spread logic\n" +
                    $"- AimChance: {report.AimOnTargetChance:P1} | Deviation: {deviationAngle:F2}°" +
                    $"- | Final Target Cell: {usedTarget.Cell} | Max Angle: {maxAngle/2:F2}° | missPoint: {missPoint.ToString()}"
                 );
            }
        }
    }






}

