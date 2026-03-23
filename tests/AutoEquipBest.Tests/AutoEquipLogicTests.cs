using Xunit;
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

            // 10 + 8*0.5 = 14
            Assert.Equal(14f, score);
        }

        [Fact]
        public void ScoreItem_HorseHarness_ReturnsBodyArmor()
        {
            var item = TestItemFactory.CreateArmorItem(ItemTypeEnum.HorseHarness, bodyArmor: 30);
            var element = TestItemFactory.ToElement(item);

            var score = AutoEquipLogic.ScoreItem(element, ItemTypeEnum.HorseHarness);

            Assert.Equal(30f, score);
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

            // Count equipped weapons
            int equippedCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (!equipment[slot].IsEmpty)
                    equippedCount++;
            }

            Assert.True(equippedCount <= 4, $"Should equip at most 4 weapons, got {equippedCount}");
        }

        [Fact]
        public void EquipBestWeapons_PrefersWeaponClassDiversity()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Two swords with high damage, a polearm and bow with lower damage
            var sword1 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 100);
            var sword2 = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 95);
            var polearm = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Polearm, WeaponClass.OneHandedPolearm, thrustDamage: 70);
            var bow = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.Bow, WeaponClass.Bow, missileSpeed: 60);

            roster.AddToCounts(sword1, 1);
            roster.AddToCounts(sword2, 1);
            roster.AddToCounts(polearm, 1);
            roster.AddToCounts(bow, 1);

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Gather equipped items
            var equipped = new System.Collections.Generic.List<ItemObject>();
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (!equipment[slot].IsEmpty)
                    equipped.Add(equipment[slot].Item);
            }

            // Should include sword1 (best), polearm, and bow for diversity
            Assert.Contains(sword1, equipped);
            Assert.Contains(polearm, equipped);
            Assert.Contains(bow, equipped);
        }

        [Fact]
        public void EquipBestWeapons_ReturnsCurrentWeaponsToRoster()
        {
            var equipment = new Equipment();
            var roster = new ItemRoster();

            // Pre-equip a weak weapon
            var oldSword = TestItemFactory.CreateWeaponItem(
                ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 10);
            equipment[EquipmentIndex.WeaponItemBeginSlot] = TestItemFactory.ToElement(oldSword);

            // Inventory has 4 better weapons to fill all slots and displace the old one
            for (int i = 0; i < 4; i++)
            {
                var w = TestItemFactory.CreateWeaponItem(
                    ItemTypeEnum.OneHandedWeapon, WeaponClass.OneHandedSword, swingDamage: 80 + i * 5);
                roster.AddToCounts(w, 1);
            }

            AutoEquipLogic.EquipBestWeapons(equipment, roster);

            // Old sword should be in roster since 4 better weapons displaced it
            Assert.True(roster.GetItemNumber(oldSword) >= 1, "Old weapon should be returned to roster");
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
    }
}
