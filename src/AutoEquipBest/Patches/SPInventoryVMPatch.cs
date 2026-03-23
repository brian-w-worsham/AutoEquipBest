using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace AutoEquipBest.Patches
{
    /// <summary>
    /// Patches SPInventoryVM.RefreshInformationValues to:
    ///   1) Create/track the SPInventoryMixin
    ///   2) Mark inventory as open for hotkey listener
    ///   3) Add the auto-equip button overlay layer
    /// </summary>
    [HarmonyPatch(typeof(SPInventoryVM))]
    [HarmonyPatch("RefreshInformationValues")]
    public static class SPInventoryVMPatch
    {
        private static SPInventoryMixin _currentMixin;
        private static GauntletLayer _overlayLayer;
        private static GauntletMovieIdentifier _overlayMovie;

        public static SPInventoryMixin CurrentMixin => _currentMixin;

        [HarmonyPostfix]
        public static void Postfix(SPInventoryVM __instance)
        {
            // Mark inventory as open for hotkey listener
            InventoryScreenHotkeyListener.IsInventoryOpen = true;

            // Create mixin if needed
            if (_currentMixin == null || _currentMixin.InventoryVM != __instance)
            {
                _currentMixin = new SPInventoryMixin(__instance);
                AddButtonLayer(__instance);
            }
        }

        private static void AddButtonLayer(SPInventoryVM inventoryVM)
        {
            try
            {
                var screen = ScreenManager.TopScreen;
                if (screen == null)
                    return;

                // Remove old layer if present
                RemoveLayer();

                _overlayLayer = new GauntletLayer("AutoEquipOverlay", 200, false);
                _overlayLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
                _overlayMovie = _overlayLayer.LoadMovie("AutoEquipBestButton", _currentMixin);
                screen.AddLayer(_overlayLayer);
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"AutoEquipBest button error: {ex.Message}", Colors.Red));
            }
        }

        public static void RemoveLayer()
        {
            if (_overlayLayer != null)
            {
                try
                {
                    var screen = ScreenManager.TopScreen;
                    screen?.RemoveLayer(_overlayLayer);
                }
                catch { }
                _overlayLayer = null;
                _overlayMovie = null;
            }
        }

        public static void Cleanup()
        {
            InventoryScreenHotkeyListener.IsInventoryOpen = false;
            RemoveLayer();
            _currentMixin = null;
        }
    }
}
