using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.Core.ItemObject;

namespace AutoEquipBest
{
    /// <summary>
    /// Scores inventory items and equips the best armor and weapons on the player character.
    /// </summary>
    public static class AutoEquipLogic
    {
        /// <summary>
        /// Equip the best available armor and weapons from the player's inventory.
        /// </summary>
        public static void EquipBestItems()
        {
            var hero = Hero.MainHero;
            if (hero == null)
                return;

            var partyInventory = MobileParty.MainParty?.ItemRoster;
            if (partyInventory == null)
                return;

            var equipment = hero.BattleEquipment;

            // --- Armor slots ---
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Body, ItemTypeEnum.BodyArmor);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Leg, ItemTypeEnum.LegArmor);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Gloves, ItemTypeEnum.HandArmor);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Cape, ItemTypeEnum.Cape);

            // --- Horse ---
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Horse, ItemTypeEnum.Horse);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.HorseHarness, ItemTypeEnum.HorseHarness);

            // --- Weapons (slots 0-3) ---
            EquipBestWeapons(equipment, partyInventory);

            InformationManager.DisplayMessage(
                new InformationMessage("Auto-equipped best items!", Colors.Green));
        }

        internal static void EquipBestForSlot(
            Equipment equipment,
            ItemRoster roster,
            EquipmentIndex slot,
            ItemTypeEnum itemType)
        {
            EquipmentElement currentElement = equipment[slot];
            float currentScore = ScoreItem(currentElement, itemType);

            int bestIndex = -1;
            float bestScore = currentScore;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement rosterElement = roster[i];
                if (rosterElement.Amount <= 0)
                    continue;

                ItemObject item = rosterElement.EquipmentElement.Item;
                if (item == null || item.ItemType != itemType)
                    continue;

                // Check usability by the hero
                if (!CanCharacterUseItem(item))
                    continue;

                float score = ScoreItem(rosterElement.EquipmentElement, itemType);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                // Return current item to inventory if something is equipped
                if (!currentElement.IsEmpty)
                {
                    roster.AddToCounts(currentElement, 1);
                }

                // Take the best item from inventory
                EquipmentElement bestElement = roster[bestIndex].EquipmentElement;
                equipment[slot] = bestElement;
                roster.AddToCounts(bestElement, -1);
            }
        }

        internal static void EquipBestWeapons(Equipment equipment, ItemRoster roster)
        {
            // Gather all weapon candidates from inventory
            var weaponCandidates = new List<(EquipmentElement element, int rosterIndex, float score)>();

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement rosterElement = roster[i];
                if (rosterElement.Amount <= 0)
                    continue;

                ItemObject item = rosterElement.EquipmentElement.Item;
                if (item == null)
                    continue;

                if (!IsWeaponType(item.ItemType))
                    continue;

                if (!CanCharacterUseItem(item))
                    continue;

                float score = ScoreWeapon(rosterElement.EquipmentElement);
                weaponCandidates.Add((rosterElement.EquipmentElement, i, score));
            }

            // Also consider currently equipped weapons
            var currentWeapons = new List<(EquipmentElement element, EquipmentIndex slot, float score)>();
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var el = equipment[slot];
                if (!el.IsEmpty)
                {
                    currentWeapons.Add((el, slot, ScoreWeapon(el)));
                }
            }

            // Merge all candidates, picking top 4 diverse weapons
            var allCandidates = new List<(EquipmentElement element, float score, int rosterIndex)>();

            foreach (var c in weaponCandidates)
                allCandidates.Add((c.element, c.score, c.rosterIndex));
            foreach (var c in currentWeapons)
                allCandidates.Add((c.element, c.score, -1)); // -1 = already equipped

            // Sort by score descending
            allCandidates.Sort((a, b) => b.score.CompareTo(a.score));

            // Pick up to 4 weapons, preferring diversity of weapon class
            var selectedWeapons = new List<EquipmentElement>();
            var usedClasses = new HashSet<WeaponClass>();
            var usedRosterIndices = new HashSet<int>();

            foreach (var candidate in allCandidates)
            {
                if (selectedWeapons.Count >= 4)
                    break;

                var weaponClass = GetPrimaryWeaponClass(candidate.element.Item);
                // Allow one of each class, but fill remaining slots with any best
                if (usedClasses.Contains(weaponClass) && selectedWeapons.Count < 3)
                    continue;

                selectedWeapons.Add(candidate.element);
                usedClasses.Add(weaponClass);
                if (candidate.rosterIndex >= 0)
                    usedRosterIndices.Add(candidate.rosterIndex);
            }

            // If we didn't fill 4 slots with diverse weapons, fill the rest with best remaining
            if (selectedWeapons.Count < 4)
            {
                foreach (var candidate in allCandidates)
                {
                    if (selectedWeapons.Count >= 4)
                        break;
                    if (selectedWeapons.Contains(candidate.element))
                        continue;

                    selectedWeapons.Add(candidate.element);
                    if (candidate.rosterIndex >= 0)
                        usedRosterIndices.Add(candidate.rosterIndex);
                }
            }

            // Return currently equipped weapons to inventory
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var el = equipment[slot];
                if (!el.IsEmpty)
                {
                    roster.AddToCounts(el, 1);
                    equipment[slot] = EquipmentElement.Invalid;
                }
            }

            // Equip selected weapons
            int slotIdx = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots && slotIdx < selectedWeapons.Count;
                 slot++, slotIdx++)
            {
                equipment[slot] = selectedWeapons[slotIdx];
                roster.AddToCounts(selectedWeapons[slotIdx], -1);
            }
        }

        // --- Scoring ---

        internal static float ScoreItem(EquipmentElement element, ItemTypeEnum itemType)
        {
            if (element.IsEmpty || element.Item == null)
                return -1f;

            var item = element.Item;

            switch (itemType)
            {
                case ItemTypeEnum.HeadArmor:
                    return item.ArmorComponent?.HeadArmor ?? 0;
                case ItemTypeEnum.BodyArmor:
                    return (item.ArmorComponent?.BodyArmor ?? 0) +
                           (item.ArmorComponent?.ArmArmor ?? 0) * 0.5f +
                           (item.ArmorComponent?.LegArmor ?? 0) * 0.3f;
                case ItemTypeEnum.LegArmor:
                    return item.ArmorComponent?.LegArmor ?? 0;
                case ItemTypeEnum.HandArmor:
                    return item.ArmorComponent?.ArmArmor ?? 0;
                case ItemTypeEnum.Cape:
                    return (item.ArmorComponent?.BodyArmor ?? 0) +
                           (item.ArmorComponent?.ArmArmor ?? 0) * 0.5f;
                case ItemTypeEnum.Horse:
                    return item.HorseComponent?.Speed ?? 0 +
                           (item.HorseComponent?.Maneuver ?? 0) * 0.5f +
                           (item.HorseComponent?.ChargeDamage ?? 0) * 0.3f;
                case ItemTypeEnum.HorseHarness:
                    return item.ArmorComponent?.BodyArmor ?? 0;
                default:
                    return item.Value;
            }
        }

        internal static float ScoreWeapon(EquipmentElement element)
        {
            if (element.IsEmpty || element.Item == null)
                return -1f;

            var item = element.Item;
            if (item.WeaponComponent == null)
                return 0f;

            var primary = item.WeaponComponent.PrimaryWeapon;
            if (primary == null)
                return 0f;

            float score = 0f;

            // Melee damage
            score += primary.SwingDamage * 1.2f;
            score += primary.ThrustDamage * 1.0f;
            score += primary.SwingSpeed * 0.3f;
            score += primary.ThrustSpeed * 0.3f;
            score += primary.WeaponLength * 0.2f;
            score += primary.Handling * 0.2f;

            // Ranged bonus
            score += primary.MissileSpeed * 0.5f;
            score += primary.MissileDamage * 1.0f;

            // Shield bonus
            if (item.ItemType == ItemTypeEnum.Shield)
            {
                score = primary.MaxDataValue * 1.0f + // hit points
                        primary.BodyArmor * 2.0f;     // shield armor
            }

            // Tier bonus
            try { score += (int)item.Tier * 10f; } catch { /* Tier unavailable outside game */ }

            return score;
        }

        internal static WeaponClass GetPrimaryWeaponClass(ItemObject item)
        {
            if (item?.WeaponComponent?.PrimaryWeapon == null)
                return WeaponClass.Undefined;
            return item.WeaponComponent.PrimaryWeapon.WeaponClass;
        }

        internal static bool IsWeaponType(ItemTypeEnum type)
        {
            switch (type)
            {
                case ItemTypeEnum.OneHandedWeapon:
                case ItemTypeEnum.TwoHandedWeapon:
                case ItemTypeEnum.Polearm:
                case ItemTypeEnum.Bow:
                case ItemTypeEnum.Crossbow:
                case ItemTypeEnum.Arrows:
                case ItemTypeEnum.Bolts:
                case ItemTypeEnum.Shield:
                case ItemTypeEnum.Thrown:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool CanCharacterUseItem(ItemObject item)
        {
            Hero hero;
            try
            {
                hero = Hero.MainHero;
            }
            catch
            {
                return true;
            }

            if (hero?.CharacterObject == null)
                return true;

            // Check if item has usage restrictions based on character
            if (item.Difficulty > 0)
            {
                // Compare against relevant skill
                if (item.RelevantSkill != null)
                {
                    int skillValue = hero.CharacterObject.GetSkillValue(item.RelevantSkill);
                    if (skillValue < item.Difficulty)
                        return false;
                }
            }

            return true;
        }
    }
}
