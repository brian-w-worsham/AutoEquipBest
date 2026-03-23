using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AutoEquipBest
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;

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

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll("com.autoequipbest.bannerlord");
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            Patches.InventoryScreenHotkeyListener.Tick();
        }
    }
}
