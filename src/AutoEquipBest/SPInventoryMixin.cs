using System;
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

        public SPInventoryVM InventoryVM => _inventoryVM;

        public SPInventoryMixin(SPInventoryVM inventoryVM)
        {
            _inventoryVM = inventoryVM;
        }

        public void ExecuteAutoEquipBest()
        {
            try
            {
                AutoEquipLogic.EquipBestItems();
                // Refresh the inventory screen to reflect changes
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
