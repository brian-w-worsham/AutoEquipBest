using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.Core.ItemObject;

namespace AutoEquipBest.Tests
{
    /// <summary>
    /// Helper methods to construct Bannerlord game objects for unit testing.
    /// Uses reflection to set read-only properties on sealed game types.
    /// </summary>
    internal static class TestItemFactory
    {
        private static readonly BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static int _idCounter;

        public static ItemObject CreateArmorItem(
            ItemTypeEnum itemType,
            int headArmor = 0,
            int bodyArmor = 0,
            int legArmor = 0,
            int armArmor = 0,
            int difficulty = 0,
            int value = 100)
        {
            var item = new ItemObject($"test_armor_{_idCounter++}");
            SetField(item, "Type", itemType);
            SetBackingField(item, "Difficulty", difficulty);
            SetBackingField(item, "Value", value);
            SetBackingField(item, "TierfOverride", 0.1f);

            var armor = new ArmorComponent(item);
            SetProperty(armor, "HeadArmor", headArmor);
            SetProperty(armor, "BodyArmor", bodyArmor);
            SetProperty(armor, "LegArmor", legArmor);
            SetProperty(armor, "ArmArmor", armArmor);

            SetBackingField(item, "ItemComponent", armor);

            return item;
        }

        public static ItemObject CreateWeaponItem(
            ItemTypeEnum itemType,
            WeaponClass weaponClass,
            int swingDamage = 0,
            int thrustDamage = 0,
            int swingSpeed = 0,
            int thrustSpeed = 0,
            int weaponLength = 0,
            int handling = 0,
            int missileSpeed = 0,
            int maxDataValue = 0,
            int bodyArmor = 0,
            int difficulty = 0,
            int value = 100)
        {
            var item = new ItemObject($"test_weapon_{_idCounter++}");
            SetField(item, "Type", itemType);
            SetBackingField(item, "Difficulty", difficulty);
            SetBackingField(item, "Value", value);
            SetBackingField(item, "TierfOverride", 0.1f);

            var weapon = new WeaponComponent(item);
            var weaponData = new WeaponComponentData(item, weaponClass, default);
            SetProperty(weaponData, "SwingDamage", swingDamage);
            SetProperty(weaponData, "ThrustDamage", thrustDamage);
            SetProperty(weaponData, "SwingSpeed", swingSpeed);
            SetProperty(weaponData, "ThrustSpeed", thrustSpeed);
            SetProperty(weaponData, "WeaponLength", weaponLength);
            SetProperty(weaponData, "Handling", handling);
            SetProperty(weaponData, "MissileSpeed", missileSpeed);
            SetProperty(weaponData, "MaxDataValue", maxDataValue);
            SetProperty(weaponData, "BodyArmor", bodyArmor);

            weapon.AddWeapon(weaponData, null);
            SetBackingField(item, "ItemComponent", weapon);

            return item;
        }

        public static ItemObject CreateHorseItem(
            int speed = 0,
            int maneuver = 0,
            int chargeDamage = 0,
            int difficulty = 0,
            int value = 100)
        {
            var item = new ItemObject($"test_horse_{_idCounter++}");
            SetField(item, "Type", ItemTypeEnum.Horse);
            SetBackingField(item, "Difficulty", difficulty);
            SetBackingField(item, "Value", value);
            SetBackingField(item, "TierfOverride", 0.1f);

            var horse = new HorseComponent();
            SetProperty(horse, "Item", item);
            SetProperty(horse, "Speed", speed);
            SetProperty(horse, "Maneuver", maneuver);
            SetProperty(horse, "ChargeDamage", chargeDamage);
            SetProperty(horse, "IsRideable", true);
            SetProperty(horse, "IsPackAnimal", false);

            SetBackingField(item, "ItemComponent", horse);

            return item;
        }

        public static ItemObject CreateSimpleItem(ItemTypeEnum itemType, int value = 100, int difficulty = 0)
        {
            var item = new ItemObject($"test_item_{_idCounter++}");
            SetField(item, "Type", itemType);
            SetBackingField(item, "Value", value);
            SetBackingField(item, "Difficulty", difficulty);
            SetBackingField(item, "TierfOverride", 0.1f);
            return item;
        }

        public static EquipmentElement ToElement(ItemObject item)
        {
            return new EquipmentElement(item, null, null, false);
        }

        /// <summary>
        /// Wraps an <see cref="ItemObject"/> into an <see cref="EquipmentElement"/>
        /// with the specified <see cref="ItemModifier"/> applied (e.g. "Rusty", "Fine").
        /// </summary>
        /// <param name="item">The base item.</param>
        /// <param name="modifier">The item modifier to apply.</param>
        /// <returns>An equipment element carrying the modifier.</returns>
        public static EquipmentElement ToElementWithModifier(ItemObject item, ItemModifier modifier)
        {
            return new EquipmentElement(item, modifier, null, false);
        }

        /// <summary>
        /// Creates an <see cref="ItemModifier"/> with optional damage and speed percentage offsets.
        /// Positive values boost stats; negative values reduce them (e.g. -15 means 85% of base).
        /// </summary>
        /// <param name="damage">Damage percentage modifier (default 0 = no change).</param>
        /// <param name="speed">Speed percentage modifier (default 0 = no change).</param>
        /// <returns>A configured <see cref="ItemModifier"/>.</returns>
        public static ItemModifier CreateModifier(int damage = 0, int speed = 0)
        {
            var modifier = new ItemModifier();
            SetProperty(modifier, "Damage", damage);
            SetProperty(modifier, "Speed", speed);
            return modifier;
        }

        /// <summary>Set a public field by name (e.g. "Type" on ItemObject).</summary>
        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, AllInstance);
            field.SetValue(obj, value);
        }

        /// <summary>Set a compiler-generated backing field for an auto-property.</summary>
        private static void SetBackingField(object obj, string propertyName, object value)
        {
            var field = obj.GetType().GetField(
                $"<{propertyName}>k__BackingField", AllInstance);
            field.SetValue(obj, value);
        }

        /// <summary>Set a property via its setter using reflection (bypasses C# access checks).</summary>
        private static void SetProperty(object obj, string propertyName, object value)
        {
            var prop = obj.GetType().GetProperty(propertyName, AllInstance);
            if (prop != null && prop.GetSetMethod(true) != null)
            {
                // Convert value to the property's actual type to handle int->short etc.
                var converted = System.Convert.ChangeType(value, prop.PropertyType);
                prop.GetSetMethod(true).Invoke(obj, new[] { converted });
            }
            else
            {
                // Fallback to backing field
                SetBackingField(obj, propertyName, value);
            }
        }
    }
}
