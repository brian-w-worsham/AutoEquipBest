using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.Core.ItemObject;

namespace AutoEquipBest
{
    /// <summary>
    /// Scores inventory items and equips the best armor and weapons on a character.
    /// </summary>
    public static class AutoEquipLogic
    {
        /// <summary>
        /// Equips the best available armor and weapons for the specified hero.
        /// Uses direct equipment modification (for non-inventory-screen contexts and tests).
        /// </summary>
        /// <param name="hero">The hero whose battle equipment will be upgraded from the party inventory.</param>
        public static void EquipBestItems(Hero hero)
        {
            if (hero == null)
                return;

            var partyInventory = MobileParty.MainParty?.ItemRoster;
            if (partyInventory == null)
                return;

            var equipment = hero.BattleEquipment;

            // --- Armor slots ---
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Head, ItemTypeEnum.HeadArmor, hero);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Body, ItemTypeEnum.BodyArmor, hero);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Leg, ItemTypeEnum.LegArmor, hero);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Gloves, ItemTypeEnum.HandArmor, hero);
            EquipBestForSlot(equipment, partyInventory, EquipmentIndex.Cape, ItemTypeEnum.Cape, hero);

            // --- Horse ---
            ApplyMountPreference(equipment, partyInventory, ShouldEquipMount(hero), hero);

            // --- Weapons (slots 0-3) ---
            EquipBestWeapons(equipment, partyInventory, hero);

            var name = hero.Name?.ToString() ?? "character";
            InformationManager.DisplayMessage(
                new InformationMessage($"Auto-equipped best items for {name}!", Colors.Green));
        }

        /// <summary>
        /// Equips the best items through the <see cref="InventoryLogic"/> transfer system.
        /// This properly integrates with the inventory screen state.
        /// </summary>
        /// <param name="inventoryLogic">The active inventory logic managing transfer commands.</param>
        /// <param name="character">The character whose equipment will be upgraded.</param>
        public static void EquipBestItemsViaInventory(
            InventoryLogic inventoryLogic, CharacterObject character)
        {
            Hero hero = character?.HeroObject;
            if (hero == null || inventoryLogic == null)
                return;

            var equipment = hero.BattleEquipment;
            if (equipment == null)
                return;

            var playerItems = inventoryLogic.GetElementsInRoster(
                InventoryLogic.InventorySide.PlayerInventory);
            if (playerItems == null || playerItems.Count == 0)
                return;

            // Snapshot available items with remaining amounts
            var available = new List<(ItemRosterElement element, int remaining)>();
            for (int i = 0; i < playerItems.Count; i++)
            {
                var el = playerItems[i];
                if (el.Amount > 0 && el.EquipmentElement.Item != null)
                    available.Add((el, el.Amount));
            }

            var commands = new List<TransferCommand>();

            // --- Armor + Horse slots ---
            CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Head, ItemTypeEnum.HeadArmor, hero);
            CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Body, ItemTypeEnum.BodyArmor, hero);
            CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Leg, ItemTypeEnum.LegArmor, hero);
            CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Gloves, ItemTypeEnum.HandArmor, hero);
            CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Cape, ItemTypeEnum.Cape, hero);
            ApplyMountPreferenceViaInventory(commands, available, equipment, character, ShouldEquipMount(hero), hero);

            // --- Weapons ---
            CollectWeaponCommands(commands, available, equipment, character, hero);

            if (commands.Count > 0)
                inventoryLogic.AddTransferCommands(commands);

            var name = hero.Name?.ToString() ?? "character";
            InformationManager.DisplayMessage(
                new InformationMessage($"Auto-equipped best items for {name}!", Colors.Green));
        }

        /// <summary>
        /// Builds transfer commands to equip the best item of a given type into a single equipment slot.
        /// If a better item is found in the available pool, the current item is unequipped first.
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="equipment">The character's current battle equipment.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        /// <param name="slot">The equipment slot to evaluate.</param>
        /// <param name="itemType">The item type expected in this slot (e.g., HeadArmor, BodyArmor).</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        private static void CollectSlotCommands(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            Equipment equipment,
            CharacterObject character,
            EquipmentIndex slot,
            ItemTypeEnum itemType,
            Hero hero)
        {
            EquipmentElement current = equipment[slot];
            int bestIdx = -1;
            float bestScore = ScoreItem(current, itemType);

            for (int i = 0; i < available.Count; i++)
            {
                var (el, remaining) = available[i];
                if (remaining <= 0) continue;

                ItemObject item = el.EquipmentElement.Item;
                if (item.ItemType != itemType) continue;
                if (!CanCharacterUseItem(item, hero)) continue;

                var baseline = bestIdx >= 0
                    ? available[bestIdx].element.EquipmentElement
                    : current;

                if (!IsBetterSlotCandidate(itemType, el.EquipmentElement, baseline, bestScore, out var candidateScore))
                    continue;

                bestScore = candidateScore;
                bestIdx = i;
            }

            if (bestIdx >= 0)
            {
                // Unequip current if occupied
                if (!current.IsEmpty)
                {
                    commands.Add(TransferCommand.Transfer(
                        1,
                        InventoryLogic.InventorySide.BattleEquipment,
                        InventoryLogic.InventorySide.PlayerInventory,
                        new ItemRosterElement(current, 1),
                        slot, EquipmentIndex.None, character));
                }

                // Equip best from inventory
                var best = available[bestIdx];
                commands.Add(TransferCommand.Transfer(
                    1,
                    InventoryLogic.InventorySide.PlayerInventory,
                    InventoryLogic.InventorySide.BattleEquipment,
                    best.element,
                    EquipmentIndex.None, slot, character));

                // Mark as claimed
                available[bestIdx] = (best.element, best.remaining - 1);
            }
        }

        /// <summary>
        /// Applies the mount preference in the inventory screen.
        /// When mounts are preferred, equips the best horse and harness; otherwise removes both.
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="equipment">The character's current battle equipment.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        /// <param name="shouldEquipMount">Whether the character should remain mounted.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        internal static void ApplyMountPreferenceViaInventory(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            Equipment equipment,
            CharacterObject character,
            bool shouldEquipMount,
            Hero hero = null)
        {
            if (commands == null || available == null || equipment == null)
                return;

            if (shouldEquipMount)
            {
                CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.Horse, ItemTypeEnum.Horse, hero);
                CollectSlotCommands(commands, available, equipment, character, EquipmentIndex.HorseHarness, ItemTypeEnum.HorseHarness, hero);
                return;
            }

            CollectUnequipSlotCommand(commands, equipment, character, EquipmentIndex.HorseHarness);
            CollectUnequipSlotCommand(commands, equipment, character, EquipmentIndex.Horse);
        }

        /// <summary>
        /// Builds a transfer command that removes the current item from a single equipment slot.
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="equipment">The character's current battle equipment.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        /// <param name="slot">The equipment slot to clear.</param>
        private static void CollectUnequipSlotCommand(
            List<TransferCommand> commands,
            Equipment equipment,
            CharacterObject character,
            EquipmentIndex slot)
        {
            EquipmentElement current = equipment[slot];
            if (current.IsEmpty)
                return;

            commands.Add(TransferCommand.Transfer(
                1,
                InventoryLogic.InventorySide.BattleEquipment,
                InventoryLogic.InventorySide.PlayerInventory,
                new ItemRosterElement(current, 1),
                slot, EquipmentIndex.None, character));
        }

        /// <summary>
        /// Builds transfer commands to equip the best weapons across all four weapon slots.
        /// Phase 1 upgrades occupied slots with the same weapon type, Phase 2 fills empty slots,
        /// and Phase 3 guarantees at least one melee weapon is equipped.
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="equipment">The character's current battle equipment.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        private static void CollectWeaponCommands(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            Equipment equipment,
            CharacterObject character,
            Hero hero)
        {
            // Phase 1: For occupied slots, upgrade with same weapon class
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                var current = equipment[slot];
                if (current.IsEmpty) continue;

                var weaponClass = GetPrimaryWeaponClass(current.Item);
                int bestIdx = FindBestAvailableWeapon(
                    available, hero, ScoreWeapon(current), weaponClass);
                if (bestIdx >= 0)
                    TransferSwapWeapon(commands, available, current, bestIdx, slot, character);
            }

            // Phase 2: Fill empty weapon slots with best available
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty) continue;

                bool allowShield = CanEquipAnotherShield(equipment, commands);
                    bool allowCrossbow = CanEquipAnotherCrossbow(equipment, commands);
                    bool allowBow = CanEquipAnotherBow(equipment, commands);
                    int bestIdx = FindBestAvailableWeapon(available, hero, -1f, null, allowShield, allowCrossbow, allowBow);
                if (bestIdx >= 0)
                    TransferEquipWeapon(commands, available, bestIdx, slot, character);
            }

            // Phase 3: Guarantee at least one melee weapon.
            EnsureMeleeWeaponViaTransfer(commands, available, equipment, character, hero);
        }

        /// <summary>
        /// Searches the available inventory snapshot for the highest-scoring weapon that exceeds
        /// <paramref name="minScore"/>. Optionally filters by a specific weapon class.
        /// </summary>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <param name="minScore">The minimum score a candidate must exceed to be selected.</param>
        /// <param name="requiredClass">If set, only weapons with this primary weapon class are considered; otherwise any weapon type is accepted.</param>
        /// <returns>The index into <paramref name="available"/> of the best weapon, or -1 if none qualifies.</returns>
        private static int FindBestAvailableWeapon(
            List<(ItemRosterElement element, int remaining)> available,
            Hero hero,
            float minScore,
            WeaponClass? requiredClass,
            bool allowShield = true,
            bool allowCrossbow = true,
            bool allowBow = true)
        {
            int bestIdx = -1;
            float bestScore = minScore;

            foreach (var candidate in EnumerateAvailableWeaponCandidates(available, hero, requiredClass, allowShield, allowCrossbow, allowBow))
            {
                float score = ScoreWeapon(candidate.element.EquipmentElement);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestIdx = candidate.index;
            }

            return bestIdx;
        }

        /// <summary>
        /// Enumerates usable weapon candidates from the available inventory snapshot.
        /// </summary>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <param name="requiredClass">Optional weapon class restriction for the slot.</param>
        /// <param name="allowShield">Whether shield items are allowed for this selection.</param>
        /// <returns>Candidate roster elements paired with their source index.</returns>
        private static IEnumerable<(int index, ItemRosterElement element)> EnumerateAvailableWeaponCandidates(
            List<(ItemRosterElement element, int remaining)> available,
            Hero hero,
            WeaponClass? requiredClass,
            bool allowShield,
            bool allowCrossbow,
            bool allowBow)
        {
            for (int i = 0; i < available.Count; i++)
            {
                var candidate = available[i];
                ItemObject item = candidate.element.EquipmentElement.Item;
                if (!IsUsableWeaponCandidate(candidate.remaining, item, hero, requiredClass, allowShield, allowCrossbow, allowBow))
                    continue;

                yield return (i, candidate.element);
            }
        }

        /// <summary>
        /// Generates transfer commands to unequip the current weapon from a slot and equip
        /// a better weapon from the available pool in its place.
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="current">The equipment element currently in the slot being replaced.</param>
        /// <param name="bestIdx">Index into <paramref name="available"/> of the replacement weapon.</param>
        /// <param name="slot">The equipment slot being swapped.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        private static void TransferSwapWeapon(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            EquipmentElement current,
            int bestIdx,
            EquipmentIndex slot,
            CharacterObject character)
        {
            commands.Add(TransferCommand.Transfer(
                1,
                InventoryLogic.InventorySide.BattleEquipment,
                InventoryLogic.InventorySide.PlayerInventory,
                new ItemRosterElement(current, 1),
                slot, EquipmentIndex.None, character));

            TransferEquipWeapon(commands, available, bestIdx, slot, character);
        }

        /// <summary>
        /// Generates a transfer command to equip a weapon from the available pool into a slot
        /// and decrements the item's remaining count.
        /// </summary>
        /// <param name="commands">The list to append the transfer command to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="bestIdx">Index into <paramref name="available"/> of the weapon to equip.</param>
        /// <param name="slot">The target equipment slot.</param>
        /// <param name="character">The character to generate the transfer command for.</param>
        private static void TransferEquipWeapon(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            int bestIdx,
            EquipmentIndex slot,
            CharacterObject character)
        {
            var best = available[bestIdx];
            commands.Add(TransferCommand.Transfer(
                1,
                InventoryLogic.InventorySide.PlayerInventory,
                InventoryLogic.InventorySide.BattleEquipment,
                best.element,
                EquipmentIndex.None, slot, character));

            available[bestIdx] = (best.element, best.remaining - 1);
        }

        /// <summary>
        /// Guarantees that at least one melee weapon is equipped by generating transfer commands.
        /// If no melee weapon is currently equipped, the best available melee weapon replaces the
        /// lowest-scoring weapon slot (or fills an empty slot).
        /// </summary>
        /// <param name="commands">The list to append generated transfer commands to.</param>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="equipment">The character's current battle equipment.</param>
        /// <param name="character">The character to generate transfer commands for.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        private static void EnsureMeleeWeaponViaTransfer(
            List<TransferCommand> commands,
            List<(ItemRosterElement element, int remaining)> available,
            Equipment equipment,
            CharacterObject character,
            Hero hero)
        {
            if (HasEquippedMeleeWeapon(equipment))
                return;

            int bestIdx = FindBestMeleeInAvailable(available, hero);
            if (bestIdx < 0)
                return;

            EquipmentIndex worstSlot = FindWorstWeaponSlot(equipment);
            if (worstSlot == EquipmentIndex.None)
                return;

            if (!equipment[worstSlot].IsEmpty)
            {
                commands.Add(TransferCommand.Transfer(
                    1,
                    InventoryLogic.InventorySide.BattleEquipment,
                    InventoryLogic.InventorySide.PlayerInventory,
                    new ItemRosterElement(equipment[worstSlot], 1),
                    worstSlot, EquipmentIndex.None, character));
            }

            TransferEquipWeapon(commands, available, bestIdx, worstSlot, character);
        }

        /// <summary>
        /// Checks whether any of the four weapon slots contains a melee weapon.
        /// </summary>
        /// <param name="equipment">The equipment to inspect.</param>
        /// <returns><c>true</c> if at least one weapon slot holds a melee weapon; otherwise <c>false</c>.</returns>
        private static bool HasEquippedMeleeWeapon(Equipment equipment)
        {
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && IsMeleeWeaponType(equipment[slot].Item.ItemType))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Searches the available inventory snapshot for the highest-scoring melee weapon.
        /// </summary>
        /// <param name="available">Snapshot of available inventory items with remaining counts.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <returns>The index into <paramref name="available"/> of the best melee weapon, or -1 if none found.</returns>
        private static int FindBestMeleeInAvailable(
            List<(ItemRosterElement element, int remaining)> available, Hero hero)
        {
            int bestIdx = -1;
            float bestScore = -1f;
            for (int i = 0; i < available.Count; i++)
            {
                var (el, remaining) = available[i];
                if (remaining <= 0) continue;
                var item = el.EquipmentElement.Item;
                if (!IsMeleeWeaponType(item.ItemType)) continue;
                if (!CanCharacterUseItem(item, hero)) continue;

                float score = ScoreWeapon(el.EquipmentElement);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        /// <summary>
        /// Finds the weapon slot with the lowest score, preferring empty slots.
        /// Used to determine which slot to replace when forcing a melee weapon.
        /// </summary>
        /// <param name="equipment">The equipment to inspect.</param>
        /// <returns>The index of the worst weapon slot, or <see cref="EquipmentIndex.None"/> if no weapon slots exist.</returns>
        private static EquipmentIndex FindWorstWeaponSlot(Equipment equipment)
        {
            EquipmentIndex worstSlot = EquipmentIndex.None;
            float worstScore = float.MaxValue;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (equipment[slot].IsEmpty)
                    return slot;

                float score = ScoreWeapon(equipment[slot]);
                if (score < worstScore)
                {
                    worstScore = score;
                    worstSlot = slot;
                }
            }
            return worstSlot;
        }

        /// <summary>
        /// Directly equips the best item of a given type into a single equipment slot.
        /// If a better item exists in the roster, the current item is returned to the roster and replaced.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to search and update.</param>
        /// <param name="slot">The equipment slot to evaluate.</param>
        /// <param name="itemType">The item type expected in this slot.</param>
        /// <param name="hero">Optional hero for skill-based usability checks.</param>
        internal static void EquipBestForSlot(
            Equipment equipment,
            ItemRoster roster,
            EquipmentIndex slot,
            ItemTypeEnum itemType,
            Hero hero = null)
        {
            EquipmentElement currentElement = equipment[slot];
            int bestIndex = -1;
            float bestScore = ScoreItem(currentElement, itemType);

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement rosterElement = roster[i];
                if (rosterElement.Amount <= 0)
                    continue;

                ItemObject item = rosterElement.EquipmentElement.Item;
                if (item == null || item.ItemType != itemType)
                    continue;

                // Check usability by the hero
                if (!CanCharacterUseItem(item, hero))
                    continue;

                var baseline = bestIndex >= 0
                    ? roster[bestIndex].EquipmentElement
                    : currentElement;

                if (!IsBetterSlotCandidate(itemType, rosterElement.EquipmentElement, baseline, bestScore, out var candidateScore))
                    continue;

                bestScore = candidateScore;
                bestIndex = i;
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

        /// <summary>
        /// Applies the mount preference for direct equipment modification.
        /// When mounts are preferred, equips the best horse and harness; otherwise removes both.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to search and update.</param>
        /// <param name="shouldEquipMount">Whether the character should remain mounted.</param>
        /// <param name="hero">Optional hero for skill-based usability checks.</param>
        internal static void ApplyMountPreference(
            Equipment equipment,
            ItemRoster roster,
            bool shouldEquipMount,
            Hero hero = null)
        {
            if (equipment == null || roster == null)
                return;

            if (shouldEquipMount)
            {
                EquipBestForSlot(equipment, roster, EquipmentIndex.Horse, ItemTypeEnum.Horse, hero);
                EquipBestForSlot(equipment, roster, EquipmentIndex.HorseHarness, ItemTypeEnum.HorseHarness, hero);
                return;
            }

            UnequipSlot(equipment, roster, EquipmentIndex.HorseHarness);
            UnequipSlot(equipment, roster, EquipmentIndex.Horse);
        }

        /// <summary>
        /// Removes the currently equipped item from a slot and returns it to the roster.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to update.</param>
        /// <param name="slot">The equipment slot to clear.</param>
        private static void UnequipSlot(Equipment equipment, ItemRoster roster, EquipmentIndex slot)
        {
            EquipmentElement currentElement = equipment[slot];
            if (currentElement.IsEmpty)
                return;

            roster.AddToCounts(currentElement, 1);
            equipment[slot] = default;
        }

        /// <summary>
        /// Directly equips the best weapons across all four weapon slots.
        /// Phase 1 upgrades occupied slots with the same weapon type, Phase 2 fills empty slots,
        /// and Phase 3 guarantees at least one melee weapon is equipped.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to search and update.</param>
        /// <param name="hero">Optional hero for skill-based usability checks.</param>
        internal static void EquipBestWeapons(Equipment equipment, ItemRoster roster, Hero hero = null)
        {
            // Phase 1: For occupied slots, upgrade only with the same weapon class.
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                var current = equipment[slot];
                if (current.IsEmpty) continue;

                var weaponClass = GetPrimaryWeaponClass(current.Item);
                int bestIndex = FindBestInRoster(
                    roster, hero, ScoreWeapon(current), weaponClass);
                if (bestIndex >= 0)
                    DirectSwapWeapon(equipment, roster, current, bestIndex, slot);
            }

            // Phase 2: Fill empty slots with the best available weapons.
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty) continue;

                bool allowShield = CanEquipAnotherShield(equipment);
                bool allowCrossbow = CanEquipAnotherCrossbow(equipment);
                bool allowBow = CanEquipAnotherBow(equipment);
                int bestIndex = FindBestInRoster(roster, hero, -1f, null, allowShield, allowCrossbow, allowBow);
                if (bestIndex >= 0)
                    DirectEquipWeapon(equipment, roster, bestIndex, slot);
            }

            // Phase 3: Guarantee at least one melee weapon.
            EnsureMeleeWeapon(equipment, roster, hero);
        }

        /// <summary>
        /// Searches the item roster for the highest-scoring weapon that exceeds
        /// <paramref name="minScore"/>. Optionally filters by a specific weapon class.
        /// </summary>
        /// <param name="roster">The party item roster to search.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <param name="minScore">The minimum score a candidate must exceed to be selected.</param>
        /// <param name="requiredClass">If set, only weapons with this primary weapon class are considered; otherwise any weapon type is accepted.</param>
        /// <returns>The index into <paramref name="roster"/> of the best weapon, or -1 if none qualifies.</returns>
        private static int FindBestInRoster(
            ItemRoster roster,
            Hero hero,
            float minScore,
            WeaponClass? requiredClass,
            bool allowShield = true,
            bool allowCrossbow = true,
            bool allowBow = true)
        {
            int bestIndex = -1;
            float bestScore = minScore;

            foreach (var candidate in EnumerateRosterWeaponCandidates(roster, hero, requiredClass, allowShield, allowCrossbow, allowBow))
            {
                float score = ScoreWeapon(candidate.element.EquipmentElement);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestIndex = candidate.index;
            }

            return bestIndex;
        }

        /// <summary>
        /// Enumerates usable weapon candidates from the party roster.
        /// </summary>
        /// <param name="roster">The party item roster to search.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <param name="requiredClass">Optional weapon class restriction for the slot.</param>
        /// <param name="allowShield">Whether shield items are allowed for this selection.</param>
        /// <returns>Candidate roster elements paired with their source index.</returns>
        private static IEnumerable<(int index, ItemRosterElement element)> EnumerateRosterWeaponCandidates(
            ItemRoster roster,
            Hero hero,
            WeaponClass? requiredClass,
            bool allowShield,
            bool allowCrossbow,
            bool allowBow)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement candidate = roster[i];
                ItemObject item = candidate.EquipmentElement.Item;
                if (!IsUsableWeaponCandidate(candidate.Amount, item, hero, requiredClass, allowShield, allowCrossbow, allowBow))
                    continue;

                yield return (i, candidate);
            }
        }

        /// <summary>
        /// Determines whether a weapon candidate can be considered for equipping.
        /// </summary>
        /// <param name="availableCount">The remaining count available to equip.</param>
        /// <param name="item">The item to inspect.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <param name="requiredClass">Optional weapon class restriction for the slot.</param>
        /// <param name="allowShield">Whether shield items are allowed for this selection.</param>
        /// <returns><c>true</c> if the candidate is usable; otherwise <c>false</c>.</returns>
        private static bool IsUsableWeaponCandidate(
            int availableCount,
            ItemObject item,
            Hero hero,
            WeaponClass? requiredClass,
            bool allowShield,
            bool allowCrossbow,
            bool allowBow)
        {
            if (availableCount <= 0)
                return false;

            if (!IsEligibleWeaponCandidate(item, requiredClass, allowShield, allowCrossbow, allowBow))
                return false;

            return CanCharacterUseItem(item, hero);
        }

        /// <summary>
        /// Determines whether an item is eligible to be chosen as a weapon candidate for a slot.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <param name="requiredClass">Optional weapon class restriction for the slot.</param>
        /// <param name="allowShield">Whether shield items are allowed for this selection.</param>
        /// <returns><c>true</c> if the item can be considered; otherwise <c>false</c>.</returns>
        private static bool IsEligibleWeaponCandidate(
            ItemObject item,
            WeaponClass? requiredClass,
            bool allowShield,
            bool allowCrossbow,
            bool allowBow)
        {
            if (item == null)
                return false;

            if (requiredClass.HasValue)
                return GetPrimaryWeaponClass(item) == requiredClass.Value;

            if (!IsWeaponType(item.ItemType))
                return false;

            if (!allowShield && item.ItemType == ItemTypeEnum.Shield)
                return false;

            if (!allowCrossbow && item.ItemType == ItemTypeEnum.Crossbow)
                return false;

            if (!allowBow && item.ItemType == ItemTypeEnum.Bow)
                return false;

            return true;
        }

        /// <summary>
        /// Returns the current weapon to the roster and equips a better weapon in its place.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to update.</param>
        /// <param name="current">The equipment element currently in the slot being replaced.</param>
        /// <param name="bestIndex">Index into <paramref name="roster"/> of the replacement weapon.</param>
        /// <param name="slot">The equipment slot being swapped.</param>
        private static void DirectSwapWeapon(
            Equipment equipment, ItemRoster roster,
            EquipmentElement current, int bestIndex, EquipmentIndex slot)
        {
            roster.AddToCounts(current, 1);
            DirectEquipWeapon(equipment, roster, bestIndex, slot);
        }

        /// <summary>
        /// Equips a weapon from the roster into the specified slot, removing it from the roster.
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to update.</param>
        /// <param name="bestIndex">Index into <paramref name="roster"/> of the weapon to equip.</param>
        /// <param name="slot">The target equipment slot.</param>
        private static void DirectEquipWeapon(
            Equipment equipment, ItemRoster roster,
            int bestIndex, EquipmentIndex slot)
        {
            var bestElement = roster[bestIndex].EquipmentElement;
            equipment[slot] = bestElement;
            roster.AddToCounts(bestElement, -1);
        }

        /// <summary>
        /// Guarantees that at least one melee weapon is equipped via direct equipment modification.
        /// If no melee weapon is currently equipped, the best available melee weapon replaces the
        /// lowest-scoring weapon slot (or fills an empty slot).
        /// </summary>
        /// <param name="equipment">The character's battle equipment to modify.</param>
        /// <param name="roster">The party item roster to search and update.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        private static void EnsureMeleeWeapon(Equipment equipment, ItemRoster roster, Hero hero)
        {
            if (HasEquippedMeleeWeapon(equipment))
                return;

            int bestIdx = FindBestMeleeInRoster(roster, hero);
            if (bestIdx < 0)
                return;

            EquipmentIndex worstSlot = FindWorstWeaponSlot(equipment);
            if (worstSlot == EquipmentIndex.None)
                return;

            if (!equipment[worstSlot].IsEmpty)
                roster.AddToCounts(equipment[worstSlot], 1);

            DirectEquipWeapon(equipment, roster, bestIdx, worstSlot);
        }

        /// <summary>
        /// Searches the item roster for the highest-scoring melee weapon the hero can use.
        /// </summary>
        /// <param name="roster">The party item roster to search.</param>
        /// <param name="hero">The hero used for skill-based usability checks.</param>
        /// <returns>The index into <paramref name="roster"/> of the best melee weapon, or -1 if none found.</returns>
        private static int FindBestMeleeInRoster(ItemRoster roster, Hero hero)
        {
            int bestIdx = -1;
            float bestScore = -1f;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster[i];
                if (el.Amount <= 0) continue;
                var item = el.EquipmentElement.Item;
                if (item == null || !IsMeleeWeaponType(item.ItemType)) continue;
                if (!CanCharacterUseItem(item, hero)) continue;

                float score = ScoreWeapon(el.EquipmentElement);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        /// <summary>
        /// Compares two body armor items using deterministic tie-break rules.
        /// Priority order is: higher total armor (body + leg + arm), lower weight,
        /// higher armor tier, then higher item value.
        /// </summary>
        /// <param name="candidate">The candidate body armor item being evaluated.</param>
        /// <param name="baseline">The current best body armor item to compare against.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="candidate"/> is better than <paramref name="baseline"/>;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool IsBodyArmorBetter(EquipmentElement candidate, EquipmentElement baseline)
        {
            if (candidate.IsEmpty || candidate.Item == null)
                return false;
            if (baseline.IsEmpty || baseline.Item == null)
                return true;

            int candidateTotal = GetBodyArmorTotal(candidate);
            int baselineTotal = GetBodyArmorTotal(baseline);
            if (candidateTotal != baselineTotal)
                return candidateTotal > baselineTotal;

            float candidateWeight = GetItemWeightSafe(candidate);
            float baselineWeight = GetItemWeightSafe(baseline);
            if (System.Math.Abs(candidateWeight - baselineWeight) > 0.0001f)
                return candidateWeight < baselineWeight;

            int candidateTier = GetItemTierSafe(candidate);
            int baselineTier = GetItemTierSafe(baseline);
            if (candidateTier != baselineTier)
                return candidateTier > baselineTier;

            return candidate.ItemValue > baseline.ItemValue;
        }

        /// <summary>
        /// Determines whether a candidate equipment element is better than the current baseline
        /// for the specified slot type.
        /// For body armor, delegates to body-armor tie-break rules; otherwise compares score values.
        /// </summary>
        /// <param name="itemType">The slot item type being evaluated.</param>
        /// <param name="candidate">The candidate equipment element.</param>
        /// <param name="baseline">The current best equipment element for comparison.</param>
        /// <param name="bestScore">The current best score for non-body-armor comparisons.</param>
        /// <param name="candidateScore">
        /// Output score of <paramref name="candidate"/> for non-body-armor comparisons.
        /// For body armor, this remains equal to <paramref name="bestScore"/>.
        /// </param>
        /// <returns><c>true</c> if the candidate should replace the baseline; otherwise <c>false</c>.</returns>
        private static bool IsBetterSlotCandidate(
            ItemTypeEnum itemType,
            EquipmentElement candidate,
            EquipmentElement baseline,
            float bestScore,
            out float candidateScore)
        {
            if (itemType == ItemTypeEnum.BodyArmor)
            {
                candidateScore = bestScore;
                return IsBodyArmorBetter(candidate, baseline);
            }

            candidateScore = ScoreItem(candidate, itemType);
            return candidateScore > bestScore;
        }

        /// <summary>
        /// Calculates total body-armor protection from modified armor values
        /// as body + leg + arm armor.
        /// </summary>
        /// <param name="element">The equipment element to evaluate.</param>
        /// <returns>The summed armor value used as the primary comparison factor.</returns>
        private static int GetBodyArmorTotal(EquipmentElement element)
        {
            return element.GetModifiedBodyArmor() +
                   element.GetModifiedLegArmor() +
                   element.GetModifiedArmArmor();
        }

        /// <summary>
        /// Gets item weight with a safe fallback for null values.
        /// </summary>
        /// <param name="element">The equipment element to read weight from.</param>
        /// <returns>The element weight, or <see cref="float.MaxValue"/> when the item is null.</returns>
        private static float GetItemWeightSafe(EquipmentElement element)
        {
            return element.Item == null ? float.MaxValue : element.Weight;
        }

        /// <summary>
        /// Gets item tier with a safe fallback if tier access is unavailable.
        /// </summary>
        /// <param name="element">The equipment element to read tier from.</param>
        /// <returns>The numeric item tier, or 0 when tier cannot be resolved.</returns>
        private static int GetItemTierSafe(EquipmentElement element)
        {
            try { return (int)element.Item.Tier; }
            catch { return 0; }
        }

        // --- Scoring ---

        /// <summary>
        /// Scores a non-weapon equipment element based on its armor or horse stats.
        /// Higher scores indicate better items for the given slot type.
        /// </summary>
        /// <param name="element">The equipment element to score.</param>
        /// <param name="itemType">The item type determining which stats are evaluated.</param>
        /// <returns>A numeric score, or -1 if the element is empty.</returns>
        internal static float ScoreItem(EquipmentElement element, ItemTypeEnum itemType)
        {
            if (element.IsEmpty || element.Item == null)
                return -1f;

            var item = element.Item;
            float score;

            switch (itemType)
            {
                case ItemTypeEnum.HeadArmor:
                    score = element.GetModifiedHeadArmor();
                    break;
                case ItemTypeEnum.BodyArmor:
                    score = element.GetModifiedBodyArmor() +
                            element.GetModifiedArmArmor() * 0.5f +
                            element.GetModifiedLegArmor() * 0.3f;
                    break;
                case ItemTypeEnum.LegArmor:
                    score = element.GetModifiedLegArmor();
                    break;
                case ItemTypeEnum.HandArmor:
                    score = element.GetModifiedArmArmor();
                    break;
                case ItemTypeEnum.Cape:
                    score = element.GetModifiedBodyArmor() +
                            element.GetModifiedArmArmor();
                    try { score += (int)item.Tier * 0.1f; } catch { /* Tier unavailable outside game */ }
                    break;
                case ItemTypeEnum.Horse:
                    return item.HorseComponent?.Speed ?? 0 +
                           (item.HorseComponent?.Maneuver ?? 0) * 0.5f +
                           (item.HorseComponent?.ChargeDamage ?? 0) * 0.3f;
                case ItemTypeEnum.HorseHarness:
                    score = element.GetModifiedMountBodyArmor() * 10f;
                    try { score += (int)item.Tier * 2f; } catch { /* Tier unavailable outside game */ }
                    score -= element.Weight * 0.1f;
                    return score;
                default:
                    return item.Value;
            }

            // Weight penalty — lighter armor scores higher
            score -= element.Weight * 0.5f;

            return score;
        }

        /// <summary>
        /// Scores a weapon element based on damage, speed, range, and tier.
        /// Uses modifier-aware stats (<c>GetModified*ForUsage</c>) so item prefixes
        /// like "Rusty" or "Fine" are reflected in the score.
        /// One Handed Polearms use a thrust-focused formula. Shields use hit points and armor.
        /// A weight penalty is applied so lighter weapons score higher.
        /// </summary>
        /// <param name="element">The weapon equipment element to score.</param>
        /// <returns>A numeric score, or -1 if the element is empty.</returns>
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

            // One Handed Polearm — thrust-only, no swing or missile stats
            if (primary.WeaponClass == WeaponClass.OneHandedPolearm)
            {
                score += element.GetModifiedThrustDamageForUsage(0) * 1.0f;
                score += element.GetModifiedThrustSpeedForUsage(0) * 0.75f;
                score += primary.WeaponLength * 0.6f;
                score += element.GetModifiedHandlingForUsage(0) * 0.5f;
                try { score += (int)item.Tier * 0.1f; } catch { /* Tier unavailable outside game */ }
                score -= element.Weight * 0.5f;
                return score;
            }

            // Shield — hit points and armor
            if (item.ItemType == ItemTypeEnum.Shield)
            {
                score = element.GetModifiedMaximumHitPointsForUsage(0) * 1.0f +
                        primary.BodyArmor * 2.0f;
                try { score += (int)item.Tier * 10f; } catch { /* Tier unavailable outside game */ }
                score -= element.Weight * 0.5f;
                return score;
            }

            // General weapon formula
            score += element.GetModifiedSwingDamageForUsage(0) * 1.2f;
            score += element.GetModifiedThrustDamageForUsage(0) * 1.0f;
            score += element.GetModifiedSwingSpeedForUsage(0) * 0.3f;
            score += element.GetModifiedThrustSpeedForUsage(0) * 0.3f;
            score += primary.WeaponLength * 0.2f;
            score += element.GetModifiedHandlingForUsage(0) * 0.2f;

            // Ranged bonus
            score += element.GetModifiedMissileSpeedForUsage(0) * 0.5f;
            score += element.GetModifiedMissileDamageForUsage(0) * 1.0f;

            // Tier bonus
            try { score += (int)item.Tier * 10f; } catch { /* Tier unavailable outside game */ }

            // Weight penalty — lighter weapons score higher
            score -= element.Weight * 0.5f;

            return score;
        }

        /// <summary>
        /// Gets the primary weapon class of an item.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <returns>The <see cref="WeaponClass"/> of the item's primary weapon, or <see cref="WeaponClass.Undefined"/> if unavailable.</returns>
        internal static WeaponClass GetPrimaryWeaponClass(ItemObject item)
        {
            if (item?.WeaponComponent?.PrimaryWeapon == null)
                return WeaponClass.Undefined;
            return item.WeaponComponent.PrimaryWeapon.WeaponClass;
        }

        /// <summary>
        /// Determines whether the given item type is any kind of weapon (melee, ranged, ammo, or shield).
        /// </summary>
        /// <param name="type">The item type to check.</param>
        /// <returns><c>true</c> if the type is a weapon category; otherwise <c>false</c>.</returns>
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

        /// <summary>
        /// Determines whether the given item type is a melee weapon (one-handed, two-handed, or polearm).
        /// </summary>
        /// <param name="type">The item type to check.</param>
        /// <returns><c>true</c> if the type is a melee weapon; otherwise <c>false</c>.</returns>
        internal static bool IsMeleeWeaponType(ItemTypeEnum type)
        {
            switch (type)
            {
                case ItemTypeEnum.OneHandedWeapon:
                case ItemTypeEnum.TwoHandedWeapon:
                case ItemTypeEnum.Polearm:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether another shield can be added to the current weapon loadout.
        /// </summary>
        /// <param name="equipment">The current weapon loadout.</param>
        /// <param name="commands">Queued transfer commands for the current auto-equip operation.</param>
        /// <returns><c>true</c> if no shield is equipped or already queued to equip; otherwise <c>false</c>.</returns>
        internal static bool CanEquipAnotherShield(Equipment equipment, IEnumerable<TransferCommand> commands = null)
        {
            return CanEquipAnotherWeaponType(equipment, commands, ItemTypeEnum.Shield);
        }

        /// <summary>
        /// Determines whether another crossbow can be added to the current weapon loadout.
        /// </summary>
        /// <param name="equipment">The current weapon loadout.</param>
        /// <param name="commands">Queued transfer commands for the current auto-equip operation.</param>
        /// <returns><c>true</c> if no crossbow is equipped or already queued to equip; otherwise <c>false</c>.</returns>
        internal static bool CanEquipAnotherCrossbow(Equipment equipment, IEnumerable<TransferCommand> commands = null)
        {
            return CanEquipAnotherWeaponType(equipment, commands, ItemTypeEnum.Crossbow);
        }

        /// <summary>
        /// Determines whether another bow can be added to the current weapon loadout.
        /// </summary>
        /// <param name="equipment">The current weapon loadout.</param>
        /// <param name="commands">Queued transfer commands for the current auto-equip operation.</param>
        /// <returns><c>true</c> if no bow is equipped or already queued to equip; otherwise <c>false</c>.</returns>
        internal static bool CanEquipAnotherBow(Equipment equipment, IEnumerable<TransferCommand> commands = null)
        {
            return CanEquipAnotherWeaponType(equipment, commands, ItemTypeEnum.Bow);
        }

        /// <summary>
        /// Determines whether another item of a constrained weapon type can be added when inventory-transfer commands are already queued.
        /// </summary>
        /// <param name="equipment">The current weapon loadout.</param>
        /// <param name="commands">Queued transfer commands for the current auto-equip operation.</param>
        /// <param name="itemType">The weapon item type to constrain.</param>
        /// <returns><c>true</c> if no matching item is equipped or already queued to equip; otherwise <c>false</c>.</returns>
        private static bool CanEquipAnotherWeaponType(
            Equipment equipment,
            IEnumerable<TransferCommand> commands,
            ItemTypeEnum itemType)
        {
            if (HasEquippedWeaponType(equipment, itemType))
                return false;

            if (commands == null)
                return true;

            foreach (TransferCommand command in commands)
            {
                if (command.ToSide != InventoryLogic.InventorySide.BattleEquipment)
                    continue;

                if (command.ToEquipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                    command.ToEquipmentIndex > EquipmentIndex.Weapon3)
                    continue;

                if (command.ElementToTransfer.EquipmentElement.Item?.ItemType == itemType)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether any equipped weapon slot currently contains the specified item type.
        /// </summary>
        /// <param name="equipment">The weapon loadout to inspect.</param>
        /// <param name="itemType">The weapon item type to look for.</param>
        /// <returns><c>true</c> if a matching item is equipped; otherwise <c>false</c>.</returns>
        private static bool HasEquippedWeaponType(Equipment equipment, ItemTypeEnum itemType)
        {
            if (equipment == null)
                return false;

            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && equipment[slot].Item?.ItemType == itemType)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a character should equip a mount based on Athletics and Riding.
        /// Riding must be strictly higher than Athletics to keep a horse and harness equipped.
        /// </summary>
        /// <param name="athleticsSkill">The Athletics skill value.</param>
        /// <param name="ridingSkill">The Riding skill value.</param>
        /// <returns><c>true</c> if the character should equip a horse and harness; otherwise <c>false</c>.</returns>
        internal static bool ShouldEquipMount(int athleticsSkill, int ridingSkill)
        {
            return ridingSkill > athleticsSkill;
        }

        /// <summary>
        /// Determines whether a hero should equip a mount based on Athletics and Riding.
        /// If the required skill objects are unavailable, preserves the previous behavior and allows mounts.
        /// </summary>
        /// <param name="hero">The hero to inspect.</param>
        /// <returns><c>true</c> if the hero should equip a horse and harness; otherwise <c>false</c>.</returns>
        private static bool ShouldEquipMount(Hero hero)
        {
            if (hero == null)
                return true;

            if (!TryGetHeroSkill(hero, GetAthleticsSkillSafe(), out int athleticsSkill))
                return true;

            if (!TryGetHeroSkill(hero, GetRidingSkillSafe(), out int ridingSkill))
                return true;

            return ShouldEquipMount(athleticsSkill, ridingSkill);
        }

        /// <summary>
        /// Safely resolves the Athletics skill object when the default skill registry is initialized.
        /// </summary>
        /// <returns>The Athletics skill object, or <c>null</c> if it is unavailable.</returns>
        private static SkillObject GetAthleticsSkillSafe()
        {
            try { return DefaultSkills.Athletics; }
            catch { return null; }
        }

        /// <summary>
        /// Safely resolves the Riding skill object when the default skill registry is initialized.
        /// </summary>
        /// <returns>The Riding skill object, or <c>null</c> if it is unavailable.</returns>
        private static SkillObject GetRidingSkillSafe()
        {
            try { return DefaultSkills.Riding; }
            catch { return null; }
        }

        /// <summary>
        /// Reads a hero skill value without assuming the Bannerlord skill registry is initialized.
        /// </summary>
        /// <param name="hero">The hero whose skill value should be read.</param>
        /// <param name="skill">The skill object to query.</param>
        /// <param name="skillValue">The resolved skill value when available.</param>
        /// <returns><c>true</c> if the skill value could be read; otherwise <c>false</c>.</returns>
        private static bool TryGetHeroSkill(Hero hero, SkillObject skill, out int skillValue)
        {
            skillValue = 0;
            if (hero == null || skill == null)
                return false;

            try
            {
                skillValue = hero.GetSkillValue(skill);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether a hero meets the skill requirements to use an item.
        /// Returns <c>true</c> if the hero is null, has no character object, or the item has no difficulty requirement.
        /// </summary>
        /// <param name="item">The item to check usability for.</param>
        /// <param name="hero">The hero to check against. Falls back to <see cref="Hero.MainHero"/> if null.</param>
        /// <returns><c>true</c> if the hero can use the item; otherwise <c>false</c>.</returns>
        internal static bool CanCharacterUseItem(ItemObject item, Hero hero = null)
        {
            if (hero == null)
            {
                try
                {
                    hero = Hero.MainHero;
                }
                catch
                {
                    return true;
                }
            }

            if (hero?.CharacterObject == null)
                return true;

            // Check if item has usage restrictions based on character
            if (item.Difficulty > 0 && item.RelevantSkill != null)
            {
                int skillValue = hero.CharacterObject.GetSkillValue(item.RelevantSkill);
                if (skillValue < item.Difficulty)
                    return false;
            }

            return true;
        }
    }
}
