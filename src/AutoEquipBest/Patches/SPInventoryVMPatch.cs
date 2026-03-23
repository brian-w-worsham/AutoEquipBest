using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
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

        /// <summary>
        /// Gets the currently active inventory mixin instance, if one is attached.
        /// </summary>
        public static SPInventoryMixin CurrentMixin => _currentMixin;

        /// <summary>
        /// Harmony postfix for <c>SPInventoryVM.RefreshInformationValues</c>.
        /// Marks the inventory as open for hotkey handling and ensures the
        /// Auto Equip Best button overlay is attached to the active inventory VM.
        /// </summary>
        /// <param name="__instance">The inventory ViewModel instance being refreshed.</param>
        [HarmonyPostfix]
        public static void Postfix(SPInventoryVM __instance)
        {
            // Mark inventory as open for hotkey listener
            InventoryScreenHotkeyListener.IsInventoryOpen = true;

            // Create mixin if needed
            if (_currentMixin == null || _currentMixin.InventoryVM != __instance)
            {
                _currentMixin = new SPInventoryMixin(__instance);
                AddButtonLayer();
            }
        }

        /// <summary>
        /// Creates and attaches the Auto Equip Best overlay layer and button movie
        /// to the current top screen.
        /// </summary>
        private static void AddButtonLayer()
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
                _overlayLayer.LoadMovie("AutoEquipBestButton", _currentMixin);
                screen.AddLayer(_overlayLayer);

                // Wire up click handler programmatically as a reliable fallback
                WireButtonClickHandler();
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"AutoEquipBest button error: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Wires the button click handler programmatically as a fallback when
        /// declarative binding does not attach reliably.
        /// </summary>
        private static void WireButtonClickHandler()
        {
            try
            {
                var root = _overlayLayer?.UIContext?.Root;
                if (root == null) return;

                var button = root.GetFirstInChildrenAndThisRecursive(
                    w => w is ButtonWidget) as ButtonWidget;
                if (button == null) return;

                // Access the ClickEventHandlers field via reflection
                var field = typeof(ButtonWidget).GetField("ClickEventHandlers",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) return;

                var handlers = field.GetValue(button) as List<System.Action<Widget>>;
                if (handlers == null)
                {
                    handlers = new List<System.Action<Widget>>();
                    field.SetValue(button, handlers);
                }

                handlers.Add(_ => _currentMixin?.ExecuteAutoEquipBest());
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"AutoEquipBest wire error: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Removes the overlay layer from the current top screen and clears
        /// the stored layer reference.
        /// </summary>
        public static void RemoveLayer()
        {
            if (_overlayLayer != null)
            {
                try
                {
                    var screen = ScreenManager.TopScreen;
                    screen?.RemoveLayer(_overlayLayer);
                }
                catch (System.Exception)
                {
                    // Screen/layer teardown can race during UI transitions; ignoring
                    // this is safe because we still clear local references below.
                }
                _overlayLayer = null;
            }
        }

        /// <summary>
        /// Cleans up patch state when inventory context is no longer active.
        /// Resets hotkey listener state, removes overlay UI, and drops mixin reference.
        /// </summary>
        public static void Cleanup()
        {
            InventoryScreenHotkeyListener.IsInventoryOpen = false;
            RemoveLayer();
            _currentMixin = null;
        }
    }
}
