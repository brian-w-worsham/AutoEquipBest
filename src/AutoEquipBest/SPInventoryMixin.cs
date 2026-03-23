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

        /// <summary>
        /// Reflected handle to <c>SPInventoryVM._currentCharacter</c>.
        /// Read-only access; no public API exists for this field.
        /// </summary>
        private static readonly FieldInfo CurrentCharacterField =
            typeof(SPInventoryVM).GetField("_currentCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Reflected handle to <c>SPInventoryVM._inventoryLogic</c>.
        /// Read-only access; no public API exists for this field.
        /// </summary>
        private static readonly FieldInfo InventoryLogicField =
            typeof(SPInventoryVM).GetField("_inventoryLogic",
                BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Gets the underlying <see cref="SPInventoryVM"/> this mixin is attached to.
        /// </summary>
        public SPInventoryVM InventoryVM => _inventoryVM;

        /// <summary>
        /// Initializes a new instance of the <see cref="SPInventoryMixin"/> class.
        /// </summary>
        /// <param name="inventoryVM">The inventory ViewModel to attach auto-equip functionality to.</param>
        public SPInventoryMixin(SPInventoryVM inventoryVM)
        {
            _inventoryVM = inventoryVM;
        }

        /// <summary>
        /// Gets the <see cref="Hero"/> currently displayed in the inventory screen
        /// by reading the private <c>_currentCharacter</c> field via reflection.
        /// </summary>
        /// <returns>The current hero, or <c>null</c> if the field is missing or unset.</returns>
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

        /// <summary>
        /// Command handler invoked by the Auto Equip Best button or Ctrl+A hotkey.
        /// Uses <see cref="InventoryLogic"/> transfer commands when available so changes are
        /// properly reflected in the inventory screen; falls back to direct equipment modification.
        /// </summary>
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
