using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace AutoEquipBest.Patches
{
    /// <summary>
    /// Patches SPInventoryVM.OnFinalize to clean up state when inventory closes.
    /// </summary>
    [HarmonyPatch(typeof(SPInventoryVM))]
    [HarmonyPatch("OnFinalize")]
    public static class SPInventoryVMFinalizePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SPInventoryVMPatch.Cleanup();
        }
    }

    /// <summary>
    /// Monitors the inventory screen and listens for hotkey Ctrl+E to auto-equip.
    /// Called from SubModule.OnApplicationTick.
    /// </summary>
    public static class InventoryScreenHotkeyListener
    {
        public static bool IsInventoryOpen { get; set; }

        public static void Tick()
        {
            if (!IsInventoryOpen)
                return;

            if (Input.IsKeyDown(InputKey.LeftControl) && Input.IsKeyPressed(InputKey.A))
            {
                try
                {
                    var mixin = SPInventoryVMPatch.CurrentMixin;
                    if (mixin != null)
                    {
                        mixin.ExecuteAutoEquipBest();
                    }
                    else
                    {
                        AutoEquipLogic.EquipBestItems(TaleWorlds.CampaignSystem.Hero.MainHero);
                    }
                }
                catch (System.Exception ex)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage($"AutoEquip hotkey error: {ex.Message}", Colors.Red));
                }
            }
        }
    }
}
