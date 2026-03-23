using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Library;

namespace AutoEquipBest
{
    /// <summary>
    /// Mixin that adds auto-equip command binding to the inventory ViewModel.
    /// The Gauntlet XML references "AutoEquipBest" as a command.
    /// </summary>
    public class SPInventoryMixin : ViewModel
    {
        private readonly SPInventoryVM _inventoryVM;

        private static readonly FieldInfo CurrentCharacterField =
            typeof(SPInventoryVM).GetField("_currentCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo InventoryLogicField =
            typeof(SPInventoryVM).GetField("_inventoryLogic",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public SPInventoryVM InventoryVM => _inventoryVM;

        public SPInventoryMixin(SPInventoryVM inventoryVM)
        {
            _inventoryVM = inventoryVM;
        }

        /// <summary>
        /// Gets the Hero currently displayed in the inventory screen.
        /// </summary>
        public Hero GetCurrentHero()
        {
            try
            {
                var characterObject = CurrentCharacterField?.GetValue(_inventoryVM)
                    as CharacterObject;
                return characterObject?.HeroObject;
            }
            catch
            {
                return null;
            }
        }

        public void ExecuteAutoEquipBest()
        {
            try
            {
                var characterObject = CurrentCharacterField?.GetValue(_inventoryVM)
                    as CharacterObject;
                var hero = characterObject?.HeroObject ?? Hero.MainHero;
                var inventoryLogic = InventoryLogicField?.GetValue(_inventoryVM)
                    as InventoryLogic;

                if (inventoryLogic != null && characterObject != null)
                {
                    // Use the InventoryLogic transfer system so changes are
                    // properly tracked by the inventory screen
                    AutoEquipLogic.EquipBestItemsViaInventory(inventoryLogic, characterObject);
                }
                else
                {
                    // Fallback to direct modification
                    AutoEquipLogic.EquipBestItems(hero);
                }

                _inventoryVM.ExecuteRemoveZeroCounts();
                _inventoryVM.RefreshValues();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"Auto-Equip error: {ex.Message}", Colors.Red));
            }
        }
    }
}
