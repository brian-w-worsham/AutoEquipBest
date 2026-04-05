using System.Collections.Generic;
using Xunit;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.Core.ItemObject;

namespace AutoEquipBest.Tests
{
    public class AutoEquipLogicScoreItemTests
    {
        [Fact]
        public void ScoreItem_EmptyElement_ReturnsNegativeOne()
        {
            var score = AutoEquipLogic.ScoreItem(default, ItemTypeEnum.HeadArmor);
            Assert.Equal(-1f, score);
        }

        [Fact]
        public void ScoreItem_HeadArmor_ReturnsHeadArmorValue()
        {
            var item = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 42);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.HeadArmor);

            Assert.Equal(42f, score);
        }

        [Fact]
        public void ScoreItem_BodyArmor_CombinesBodyArmAndLeg()
        {
            var item = TestItemFactory.CreateArmorItem(
                ItemTypeEnum.BodyArmor, bodyArmor: 40, armArmor: 20, legArmor: 10);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.BodyArmor);

            // 40 + 20*0.5 + 10*0.3 = 40 + 10 + 3 = 53
            Assert.Equal(53f, score);
        }

        [Fact]
        public void ScoreItem_LegArmor_ReturnsLegArmorValue()
        {
            var item = TestItemFactory.CreateArmorItem(ItemTypeEnum.LegArmor, legArmor: 25);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.LegArmor);

            Assert.Equal(25f, score);
        }

        [Fact]
        public void ScoreItem_HandArmor_ReturnsArmArmorValue()
        {
            var item = TestItemFactory.CreateArmorItem(ItemTypeEnum.HandArmor, armArmor: 18);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.HandArmor);

            Assert.Equal(18f, score);
        }

        [Fact]
        public void ScoreItem_Cape_CombinesBodyAndArm()
        {
            var item = TestItemFactory.CreateArmorItem(
                ItemTypeEnum.Cape, bodyArmor: 10, armArmor: 8);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.Cape);

            // BodyArmor + ArmArmor = 10 + 8 = 18
            Assert.True(score >= 18f, $"Expected >= 18 but got {score}");
        }

        [Fact]
        public void ScoreItem_HorseHarness_ReturnsBodyArmor()
        {
            var item = TestItemFactory.CreateArmorItem(ItemTypeEnum.HorseHarness, bodyArmor: 30);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.HorseHarness);

            Assert.Equal(300f, score);
        }

        [Fact]
        public void ScoreItem_UnknownType_ReturnsItemValue()
        {
            var item = TestItemFactory.CreateSimpleItem(ItemTypeEnum.Goods, value: 500);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.Goods);

            Assert.Equal(500f, score);
        }
    }

    public class AutoEquipLogicScoreWeaponTests
    {
        [Fact]
        public void ScoreWeapon_EmptyElement_ReturnsNegativeOne()
        {
            var score = AutoEquipLogic.ScoreWeapon(default);
            Assert.Equal(-1f, score);
        }

        [Fact]
        public void ScoreWeapon_ItemWithNoWeaponComponent_ReturnsZero()
        {
            var item = TestItemFactory.CreateSimpleItem(ItemTypeEnum.OneHandedWeapon);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreWeapon(element);

            Assert.Equal(0f, score);
        }

        [Fact]
        public void ScoreWeapon_MeleeWeapon_IncludesSwingAndThrustDamage()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon,
                WeaponClass.OneHandedSword,
                swingDamage: 100,
                thrustDamage: 80);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreWeapon(element);

            // SwingDamage*1.2 + ThrustDamage*1.0 + Tier*10 (Tier0 = 0)
            // 100*1.2 + 80*1.0 = 120 + 80 = 200 (plus other zero terms)
            Assert.True(score >= 200f, $"Expected >= 200 but got {score}");
        }

        [Fact]
        public void ScoreWeapon_Shield_UsesShieldFormula()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield,
                WeaponClass.SmallShield,
                maxDataValue: 200,  // hit points
                bodyArmor: 15);     // shield armor
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreWeapon(element);

            // Shield: MaxDataValue*1.0 + BodyArmor*2.0 + Tier*10
            // 200 + 30 = 230 (+ tier bonus)
            Assert.True(score >= 230f, $"Expected >= 230 but got {score}");
        }

        [Fact]
        public void ScoreWeapon_WeaponSpeedAndLength_Contribute()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon,
                WeaponClass.OneHandedSword,
                swingSpeed: 100,
                thrustSpeed: 90,
                weaponLength: 100,
                handling: 80);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreWeapon(element);

            // SwingSpeed*0.3 + ThrustSpeed*0.3 + Length*0.2 + Handling*0.2
            // 30 + 27 + 20 + 16 = 93
            Assert.True(score >= 93f, $"Expected >= 93 but got {score}");
        }

        [Fact]
        public void ScoreWeapon_NegativeModifier_ReducesScore()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon,
                WeaponClass.OneHandedSword,
                swingDamage: 100,
                thrustDamage: 50,
                swingSpeed: 90,
                thrustSpeed: 80,
                handling: 70);

            var baseScore = AutoEquipLogic.ScoreWeapon(TestItemFactory.ToElement(item));

            // Apply a negative modifier (e.g. "Rusty") that reduces damage and speed
            var rustyModifier = TestItemFactory.CreateModifier(damage: -20, speed: -10);
            var modifiedScore = AutoEquipLogic.ScoreWeapon(
                TestItemFactory.ToElementWithModifier(item, rustyModifier));

            Assert.True(modifiedScore < baseScore,
                $"Modified score ({modifiedScore}) should be less than base score ({baseScore})");
        }
    }

    public class AutoEquipLogicIsWeaponTypeTests
    {
        [Theory]
        [InlineData(ItemTypeEnum.OneHandedWeapon, true)]
        [InlineData(ItemTypeEnum.TwoHandedWeapon, true)]
        [InlineData(ItemTypeEnum.Polearm, true)]
        [InlineData(ItemTypeEnum.Bow, true)]
        [InlineData(ItemTypeEnum.Crossbow, true)]
        [InlineData(ItemTypeEnum.Arrows, true)]
        [InlineData(ItemTypeEnum.Bolts, true)]
        [InlineData(ItemTypeEnum.Shield, true)]
        [InlineData(ItemTypeEnum.Thrown, true)]
        [InlineData(ItemTypeEnum.HeadArmor, false)]
        [InlineData(ItemTypeEnum.BodyArmor, false)]
        [InlineData(ItemTypeEnum.LegArmor, false)]
        [InlineData(ItemTypeEnum.HandArmor, false)]
        [InlineData(ItemTypeEnum.Horse, false)]
        [InlineData(ItemTypeEnum.HorseHarness, false)]
        [InlineData(ItemTypeEnum.Cape, false)]
        [InlineData(ItemTypeEnum.Goods, false)]
        public void IsWeaponType_ReturnsExpected(ItemTypeEnum type, bool expected)
        {
            Assert.Equal(expected, AutoEquipLogic.IsWeaponType(type));
        }
    }

    public class AutoEquipLogicGetPrimaryWeaponClassTests
    {
        [Fact]
        public void GetPrimaryWeaponClass_NullItem_ReturnsUndefined()
        {
            Assert.Equal(WeaponClass.Undefined, AutoEquipLogic.GetPrimaryWeaponClass(null));
        }

        [Fact]
        public void GetPrimaryWeaponClass_ItemWithNoWeaponComponent_ReturnsUndefined()
        {
            var item = TestItemFactory.CreateSimpleItem(ItemTypeEnum.OneHandedWeapon);
            Assert.Equal(WeaponClass.Undefined, AutoEquipLogic.GetPrimaryWeaponClass(item));
        }

        [Fact]
        public void GetPrimaryWeaponClass_Sword_ReturnsSwordClass()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword);

            Assert.Equal(WeaponClass.OneHandedSword, AutoEquipLogic.GetPrimaryWeaponClass(item));
        }

        [Fact]
        public void GetPrimaryWeaponClass_Bow_ReturnsBowClass()
        {
            var item = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow);

            Assert.Equal(WeaponClass.Bow, AutoEquipLogic.GetPrimaryWeaponClass(item));
        }
    }

    public class AutoEquipLogicEquipBestForSlotTests
    {
        [Fact]
        public void EquipBestForSlot_BetterItemInRoster_EquipsIt()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Currently equipped: 20 head armor
            var currentHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 20);
            equipment[EquipmentIndex.Head] = TestItemFactory.ToElement(currentHelm);

            // Inventory has: 35 head armor
            var betterHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 35);
            roster.AddToCounts(betterHelm, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.Equal(betterHelm, equipment[EquipmentIndex.Head].Item);
        }

        [Fact]
        public void EquipBestForSlot_WorseItemInRoster_KeepsCurrent()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var currentHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 35);
            equipment[EquipmentIndex.Head] = TestItemFactory.ToElement(currentHelm);

            var worseHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 10);
            roster.AddToCounts(worseHelm, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.Equal(currentHelm, equipment[EquipmentIndex.Head].Item);
        }

        [Fact]
        public void EquipBestForSlot_EmptySlotWithItemInRoster_EquipsIt()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var helm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 20);
            roster.AddToCounts(helm, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.Equal(helm, equipment[EquipmentIndex.Head].Item);
        }

        [Fact]
        public void EquipBestForSlot_ReturnsOldItemToRoster()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var currentHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 10);
            equipment[EquipmentIndex.Head] = TestItemFactory.ToElement(currentHelm);

            var betterHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 30);
            roster.AddToCounts(betterHelm, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            // Old helm should be back in roster, better helm removed
            int oldHelmCount = roster.GetItemNumber(currentHelm);
            int newHelmCount = roster.GetItemNumber(betterHelm);
            Assert.Equal(1, oldHelmCount);
            Assert.Equal(0, newHelmCount);
        }

        [Fact]
        public void EquipBestForSlot_EmptyRoster_NoChange()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var currentHelm = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 20);
            equipment[EquipmentIndex.Head] = TestItemFactory.ToElement(currentHelm);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.Equal(currentHelm, equipment[EquipmentIndex.Head].Item);
        }

        [Fact]
        public void EquipBestForSlot_IgnoresWrongItemType()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Add body armor to inventory, but try to equip head slot
            var bodyArmor = TestItemFactory.CreateArmorItem(ItemTypeEnum.BodyArmor, bodyArmor: 50);
            roster.AddToCounts(bodyArmor, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.True(equipment[EquipmentIndex.Head].IsEmpty);
        }

        [Fact]
        public void EquipBestForSlot_PicksBestFromMultipleCandidates()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var weak = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 10);
            var medium = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 25);
            var strong = TestItemFactory.CreateArmorItem(ItemTypeEnum.HeadArmor, headArmor: 40);

            roster.AddToCounts(weak, 1);
            roster.AddToCounts(medium, 1);
            roster.AddToCounts(strong, 1);

            AutoEquipLogic.EquipBestForSlot(equipment, roster, EquipmentIndex.Head, ItemTypeEnum.HeadArmor);

            Assert.Equal(strong, equipment[EquipmentIndex.Head].Item);
        }
    }

    public class AutoEquipLogicMountPreferenceTests
    {
        [Theory]
        [InlineData(40, 80, true)]
        [InlineData(80, 40, false)]
        [InlineData(60, 60, false)]
        public void ShouldEquipMount_ReturnsExpected(int athleticsSkill, int ridingSkill, bool expected)
        {
            Assert.Equal(expected, AutoEquipLogic.ShouldEquipMount(athleticsSkill, ridingSkill));
        }

        [Fact]
        public void ApplyMountPreference_MountsNotPreferred_RemovesHorseAndHarness()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();
            var horse = TestItemFactory.CreateHorseItem(speed: 50, maneuver: 55, chargeDamage: 20);
            var harness = TestItemFactory.CreateArmorItem(ItemTypeEnum.HorseHarness, bodyArmor: 12);

            equipment[EquipmentIndex.Horse] = TestItemFactory.ToElement(horse);
            equipment[EquipmentIndex.HorseHarness] = TestItemFactory.ToElement(harness);

            AutoEquipLogic.ApplyMountPreference(equipment, roster, shouldEquipMount: false);

            Assert.True(equipment[EquipmentIndex.Horse].IsEmpty);
            Assert.True(equipment[EquipmentIndex.HorseHarness].IsEmpty);
            Assert.Equal(1, roster.GetItemNumber(horse));
            Assert.Equal(1, roster.GetItemNumber(harness));
        }

        [Fact]
        public void ApplyMountPreferenceViaInventory_MountsPreferred_QueuesHorseAndHarnessEquips()
        {
            var equipment = new Equipment();
            var commands = new List<TransferCommand>();
            var horse = TestItemFactory.CreateHorseItem(speed: 55, maneuver: 60, chargeDamage: 25);
            var harness = TestItemFactory.CreateArmorItem(ItemTypeEnum.HorseHarness, bodyArmor: 15);
            var available = new List<(ItemRosterElement element, int remaining)>
            {
                (new ItemRosterElement(TestItemFactory.ToElement(horse), 1), 1),
                (new ItemRosterElement(TestItemFactory.ToElement(harness), 1), 1)
            };

            AutoEquipLogic.ApplyMountPreferenceViaInventory(
                commands,
                available,
                equipment,
                character: null,
                shouldEquipMount: true);

            Assert.Equal(2, commands.Count);
            Assert.Equal(InventoryLogic.InventorySide.PlayerInventory, commands[0].FromSide);
            Assert.Equal(InventoryLogic.InventorySide.BattleEquipment, commands[0].ToSide);
            Assert.Equal(EquipmentIndex.Horse, commands[0].ToEquipmentIndex);
            Assert.Equal(horse, commands[0].ElementToTransfer.EquipmentElement.Item);

            Assert.Equal(InventoryLogic.InventorySide.PlayerInventory, commands[1].FromSide);
            Assert.Equal(InventoryLogic.InventorySide.BattleEquipment, commands[1].ToSide);
            Assert.Equal(EquipmentIndex.HorseHarness, commands[1].ToEquipmentIndex);
            Assert.Equal(harness, commands[1].ElementToTransfer.EquipmentElement.Item);
        }

        [Fact]
        public void ApplyMountPreferenceViaInventory_MountsNotPreferred_QueuesHorseAndHarnessUnequips()
        {
            var equipment = new Equipment();
            var commands = new List<TransferCommand>();
            var available = new List<(ItemRosterElement element, int remaining)>();
            var horse = TestItemFactory.CreateHorseItem(speed: 48, maneuver: 52, chargeDamage: 18);
            var harness = TestItemFactory.CreateArmorItem(ItemTypeEnum.HorseHarness, bodyArmor: 10);

            equipment[EquipmentIndex.Horse] = TestItemFactory.ToElement(horse);
            equipment[EquipmentIndex.HorseHarness] = TestItemFactory.ToElement(harness);

            AutoEquipLogic.ApplyMountPreferenceViaInventory(
                commands,
                available,
                equipment,
                character: null,
                shouldEquipMount: false);

            Assert.Equal(2, commands.Count);
            Assert.Equal(InventoryLogic.InventorySide.BattleEquipment, commands[0].FromSide);
            Assert.Equal(InventoryLogic.InventorySide.PlayerInventory, commands[0].ToSide);
            Assert.Equal(EquipmentIndex.HorseHarness, commands[0].FromEquipmentIndex);
            Assert.Equal(harness, commands[0].ElementToTransfer.EquipmentElement.Item);

            Assert.Equal(InventoryLogic.InventorySide.BattleEquipment, commands[1].FromSide);
            Assert.Equal(InventoryLogic.InventorySide.PlayerInventory, commands[1].ToSide);
            Assert.Equal(EquipmentIndex.Horse, commands[1].FromEquipmentIndex);
            Assert.Equal(horse, commands[1].ElementToTransfer.EquipmentElement.Item);
        }
    }

    public class AutoEquipLogicEquipBestWeaponsTests
    {
        [Fact]
        public void EquipBestWeapons_EquipsFromRoster()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 80);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            Assert.Equal(sword, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_MaxFourWeapons()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            for (int i = 0; i < 6; i++)
            {
                var weapon = TestItemFactory.CreateWeaponItem(
                    ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 50 + i * 10);
                roster.AddToCounts(weapon, 1);
            }

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            int equippedCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (!equipment[slot].IsEmpty)
                    equippedCount++;
            }

            Assert.True(equippedCount <= 4, $"Should equip at most 4 weapons, got {equippedCount}");
        }

        [Fact]
        public void EquipBestWeapons_FillsEmptySlotsWithBestAvailable()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 100);
            var polearm = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Polearm, WeaponClass.OneHandedPolearm, thrustDamage: 70);
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 60);

            roster.AddToCounts(sword, 1);
            roster.AddToCounts(polearm, 1);
            roster.AddToCounts(bow, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            var equipped = new System.Collections.Generic.List<ItemObject>();
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (!equipment[slot].IsEmpty)
                    equipped.Add(equipment[slot].Item);
            }

            Assert.Contains(sword, equipped);
            Assert.Contains(polearm, equipped);
            Assert.Contains(bow, equipped);
        }

        [Fact]
        public void EquipBestWeapons_ReturnsCurrentWeaponsToRoster()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Fill all 4 weapon slots so Phase 2 has nothing to fill
            var oldSword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 10);
            var shield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 80, bodyArmor: 5);
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 50);
            var arrows = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Arrows, WeaponClass.Arrow, maxDataValue: 20);

            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(oldSword);
            equipment[EquipmentIndex.Weapon1] = TestItemFactory.ToElement(shield);
            equipment[EquipmentIndex.Weapon2] = TestItemFactory.ToElement(bow);
            equipment[EquipmentIndex.Weapon3] = TestItemFactory.ToElement(arrows);

            // Inventory has a better one-handed weapon
            var betterSword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 95);
            roster.AddToCounts(betterSword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Old sword should be returned to roster after upgrade
            Assert.True(roster.GetItemNumber(oldSword) >= 1, "Old weapon should be returned to roster");
            Assert.Equal(betterSword, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_EmptyRoster_ClearsSlots()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                Assert.True(equipment[slot].IsEmpty, $"Slot {slot} should be empty with no weapons available");
            }
        }

        [Fact]
        public void EquipBestWeapons_DoesNotReplaceTwoHandedWithOneHanded()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character has a two-handed weapon equipped
            var twoHander = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.TwoHandedWeapon, WeaponClass.TwoHandedSword, swingDamage: 50);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(twoHander);

            // Roster has a much better one-handed weapon
            var betterOneHander = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 200);
            roster.AddToCounts(betterOneHander, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Two-handed weapon should NOT be replaced by the one-handed weapon
            Assert.Equal(twoHander, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_DoesNotReplaceShieldWithWeapon()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character has a shield equipped
            var shield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 100, bodyArmor: 10);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(shield);

            // Roster has a much better sword
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 200);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Shield should NOT be replaced by the sword
            Assert.Equal(shield, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_DoesNotEquipMoreThanOneShield()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var firstShield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 100, bodyArmor: 10);
            var secondShield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 120, bodyArmor: 12);
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 60);

            roster.AddToCounts(firstShield, 1);
            roster.AddToCounts(secondShield, 1);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            int shieldCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && equipment[slot].Item.ItemType == ItemTypeEnum.Shield)
                    shieldCount++;
            }

            Assert.Equal(1, shieldCount);
            Assert.True(roster.GetItemNumber(firstShield) + roster.GetItemNumber(secondShield) >= 1);
        }

        [Fact]
        public void EquipBestWeapons_DoesNotEquipSecondCrossbow()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var equippedCrossbow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Crossbow, WeaponClass.Crossbow, missileSpeed: 75);
            var spareCrossbow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Crossbow, WeaponClass.Crossbow, missileSpeed: 95);
            var bolts = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bolts, WeaponClass.Bolt, maxDataValue: 30);
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 65);

            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(equippedCrossbow);
            roster.AddToCounts(spareCrossbow, 1);
            roster.AddToCounts(bolts, 1);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            int crossbowCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && equipment[slot].Item.ItemType == ItemTypeEnum.Crossbow)
                    crossbowCount++;
            }

            Assert.Equal(1, crossbowCount);
            Assert.True(roster.GetItemNumber(equippedCrossbow) + roster.GetItemNumber(spareCrossbow) >= 1);
        }

        [Fact]
        public void EquipBestWeapons_DoesNotEquipSecondBow()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            var equippedBow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 75);
            var spareBow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 95);
            var arrows = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Arrows, WeaponClass.Arrow, maxDataValue: 30);
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 65);

            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(equippedBow);
            roster.AddToCounts(spareBow, 1);
            roster.AddToCounts(arrows, 1);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            int bowCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && equipment[slot].Item.ItemType == ItemTypeEnum.Bow)
                    bowCount++;
            }

            Assert.Equal(1, bowCount);
            Assert.True(roster.GetItemNumber(equippedBow) + roster.GetItemNumber(spareBow) >= 1);
        }

        [Fact]
        public void CanEquipAnotherShield_WithQueuedShieldEquip_ReturnsFalse()
        {
            var equipment = new Equipment();
            var shield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 120, bodyArmor: 12);
            var commands = new List<TransferCommand>
            {
                TransferCommand.Transfer(
                    1,
                    InventoryLogic.InventorySide.PlayerInventory,
                    InventoryLogic.InventorySide.BattleEquipment,
                    new ItemRosterElement(TestItemFactory.ToElement(shield), 1),
                    EquipmentIndex.None,
                    EquipmentIndex.WeaponItemBeginSlot,
                    null)
            };

            Assert.False(AutoEquipLogic.CanEquipAnotherShield(equipment, commands));
        }

        [Fact]
        public void CanEquipAnotherCrossbow_WithQueuedCrossbowEquip_ReturnsFalse()
        {
            var equipment = new Equipment();
            var crossbow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Crossbow, WeaponClass.Crossbow, missileSpeed: 95);
            var commands = new List<TransferCommand>
            {
                TransferCommand.Transfer(
                    1,
                    InventoryLogic.InventorySide.PlayerInventory,
                    InventoryLogic.InventorySide.BattleEquipment,
                    new ItemRosterElement(TestItemFactory.ToElement(crossbow), 1),
                    EquipmentIndex.None,
                    EquipmentIndex.WeaponItemBeginSlot,
                    null)
            };

            Assert.False(AutoEquipLogic.CanEquipAnotherCrossbow(equipment, commands));
        }

        [Fact]
        public void CanEquipAnotherBow_WithQueuedBowEquip_ReturnsFalse()
        {
            var equipment = new Equipment();
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 95);
            var commands = new List<TransferCommand>
            {
                TransferCommand.Transfer(
                    1,
                    InventoryLogic.InventorySide.PlayerInventory,
                    InventoryLogic.InventorySide.BattleEquipment,
                    new ItemRosterElement(TestItemFactory.ToElement(bow), 1),
                    EquipmentIndex.None,
                    EquipmentIndex.WeaponItemBeginSlot,
                    null)
            };

            Assert.False(AutoEquipLogic.CanEquipAnotherBow(equipment, commands));
        }

        [Fact]
        public void EquipBestWeapons_UpgradesWithSameWeaponClass()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character has a weak one-handed sword
            var weakSword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 30);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(weakSword);

            // Roster has a better one-handed sword (same class)
            var betterSword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 100);
            roster.AddToCounts(betterSword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Should upgrade to the better one-handed sword
            Assert.Equal(betterSword, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_DoesNotReplaceSwordWithAxe()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character has a one-handed sword equipped
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 40);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(sword);

            // Roster has a much better one-handed axe (same ItemType, different WeaponClass)
            var betterAxe = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedAxe, swingDamage: 200);
            roster.AddToCounts(betterAxe, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Sword should NOT be replaced by the axe — different weapon class
            Assert.Equal(sword, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_PreservesWeaponTypesAcrossSlots()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character's loadout: 1h sword, shield, bow, arrows
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 40);
            var shield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 80, bodyArmor: 5);
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 50);
            var arrows = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Arrows, WeaponClass.Arrow, maxDataValue: 20);

            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(sword);
            equipment[EquipmentIndex.Weapon1] = TestItemFactory.ToElement(shield);
            equipment[EquipmentIndex.Weapon2] = TestItemFactory.ToElement(bow);
            equipment[EquipmentIndex.Weapon3] = TestItemFactory.ToElement(arrows);

            // Roster has a better 2h weapon and a better polearm — should NOT displace anything
            var twoHander = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.TwoHandedWeapon, WeaponClass.TwoHandedSword, swingDamage: 200);
            var polearm = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Polearm, WeaponClass.OneHandedPolearm, thrustDamage: 150);
            roster.AddToCounts(twoHander, 1);
            roster.AddToCounts(polearm, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // All original weapon types should be preserved
            Assert.Equal(ItemTypeEnum.OneHandedWeapon, equipment[EquipmentIndex.WeaponItemBeginSlot].Item.ItemType);
            Assert.Equal(ItemTypeEnum.Shield, equipment[EquipmentIndex.Weapon1].Item.ItemType);
            Assert.Equal(ItemTypeEnum.Bow, equipment[EquipmentIndex.Weapon2].Item.ItemType);
            Assert.Equal(ItemTypeEnum.Arrows, equipment[EquipmentIndex.Weapon3].Item.ItemType);

            // And the mismatched items should remain in roster
            Assert.True(roster.GetItemNumber(twoHander) >= 1);
            Assert.True(roster.GetItemNumber(polearm) >= 1);
        }

        [Fact]
        public void EquipBestWeapons_UpgradesBetterShield()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character has a weak shield
            var weakShield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 50, bodyArmor: 5);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(weakShield);

            // Roster has a better shield
            var betterShield = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Shield, WeaponClass.SmallShield, maxDataValue: 200, bodyArmor: 15);
            roster.AddToCounts(betterShield, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            Assert.Equal(betterShield, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }

        [Fact]
        public void EquipBestWeapons_OnlyThrown_ReplaceOneWithMelee()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Roster only has thrown weapons and one sword
            var thrown1 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Thrown, WeaponClass.Javelin, thrustDamage: 80, missileSpeed: 40);
            var thrown2 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Thrown, WeaponClass.Javelin, thrustDamage: 70, missileSpeed: 35);
            var thrown3 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Thrown, WeaponClass.Javelin, thrustDamage: 60, missileSpeed: 30);
            var thrown4 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Thrown, WeaponClass.Javelin, thrustDamage: 50, missileSpeed: 25);
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 40);

            roster.AddToCounts(thrown1, 1);
            roster.AddToCounts(thrown2, 1);
            roster.AddToCounts(thrown3, 1);
            roster.AddToCounts(thrown4, 1);
            roster.AddToCounts(sword, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Should have at least one melee weapon equipped
            bool hasMelee = false;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && AutoEquipLogic.IsMeleeWeaponType(equipment[slot].Item.ItemType))
                {
                    hasMelee = true;
                    break;
                }
            }
            Assert.True(hasMelee, "Character should always have at least one melee weapon");
        }

        [Fact]
        public void EquipBestWeapons_OnlyRanged_ReplaceOneWithMelee()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Only ranged weapons and one melee available
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 80);
            var arrows = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Arrows, WeaponClass.Arrow, maxDataValue: 30);
            var crossbow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Crossbow, WeaponClass.Crossbow, missileSpeed: 70);
            var bolts = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bolts, WeaponClass.Bolt, maxDataValue: 25);
            var polearm = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Polearm, WeaponClass.OneHandedPolearm, thrustDamage: 60);

            roster.AddToCounts(bow, 1);
            roster.AddToCounts(arrows, 1);
            roster.AddToCounts(crossbow, 1);
            roster.AddToCounts(bolts, 1);
            roster.AddToCounts(polearm, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            bool hasMelee = false;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot <= EquipmentIndex.Weapon3; slot++)
            {
                if (!equipment[slot].IsEmpty && AutoEquipLogic.IsMeleeWeaponType(equipment[slot].Item.ItemType))
                {
                    hasMelee = true;
                    break;
                }
            }
            Assert.True(hasMelee, "Character should always have at least one melee weapon");
        }

        [Fact]
        public void EquipBestWeapons_NoMeleeAvailable_DoesNotCrash()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Only thrown weapons, no melee available at all
            var thrown = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Thrown, WeaponClass.Javelin, thrustDamage: 60, missileSpeed: 30);
            roster.AddToCounts(thrown, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Should still equip what's available, no crash
            Assert.False(equipment[EquipmentIndex.WeaponItemBeginSlot].IsEmpty);
        }

        [Fact]
        public void EquipBestWeapons_AlreadyHasMelee_NoChange()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Character already has a melee weapon and ranged weapons
            var sword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 60);
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 70);
            var arrows = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Arrows, WeaponClass.Arrow, maxDataValue: 25);

            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(sword);
            equipment[EquipmentIndex.Weapon1] = TestItemFactory.ToElement(bow);
            equipment[EquipmentIndex.Weapon2] = TestItemFactory.ToElement(arrows);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Melee weapon should still be there - no replacement
            Assert.Equal(sword, equipment[EquipmentIndex.WeaponItemBeginSlot].Item);
        }
    }

    public class AutoEquipLogicIsMeleeWeaponTypeTests
    {
        [Theory]
        [InlineData(ItemTypeEnum.OneHandedWeapon, true)]
        [InlineData(ItemTypeEnum.TwoHandedWeapon, true)]
        [InlineData(ItemTypeEnum.Polearm, true)]
        [InlineData(ItemTypeEnum.Bow, false)]
        [InlineData(ItemTypeEnum.Crossbow, false)]
        [InlineData(ItemTypeEnum.Arrows, false)]
        [InlineData(ItemTypeEnum.Bolts, false)]
        [InlineData(ItemTypeEnum.Shield, false)]
        [InlineData(ItemTypeEnum.Thrown, false)]
        [InlineData(ItemTypeEnum.HeadArmor, false)]
        public void IsMeleeWeaponType_ReturnsExpected(ItemTypeEnum type, bool expected)
        {
            Assert.Equal(expected, AutoEquipLogic.IsMeleeWeaponType(type));
        }
    }
}
