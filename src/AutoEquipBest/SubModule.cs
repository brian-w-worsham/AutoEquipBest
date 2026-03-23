using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AutoEquipBest
{
    /// <summary>
    /// Entry point for the AutoEquipBest mod. Registers Harmony patches on load
    /// and polls for hotkey input each application tick.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        /// <summary>Harmony instance used to apply and revert all patches.</summary>
        private Harmony _harmony;

        /// <summary>
        /// Called when the module is first loaded. Applies all Harmony patches
        /// and displays a confirmation message.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony("com.autoequipbest.bannerlord");
                _harmony.PatchAll();
                InformationManager.DisplayMessage(
                    new InformationMessage("AutoEquipBest: Loaded successfully.", Colors.Green));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"AutoEquipBest load error: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Called when the module is unloaded. Reverts all Harmony patches
        /// applied by this mod.
        /// </summary>
        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll("com.autoequipbest.bannerlord");
        }

        /// <summary>
        /// Called every application frame. Delegates to the hotkey listener
        /// to check for the Ctrl+A auto-equip shortcut.
        /// </summary>
        /// <param name="dt">Elapsed time in seconds since the last tick.</param>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            Patches.InventoryScreenHotkeyListener.Tick();
        }
    }
}
