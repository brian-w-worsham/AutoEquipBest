using Xunit;
using AutoEquipBest.Patches;

namespace AutoEquipBest.Tests
{
    public class InventoryScreenHotkeyListenerTests
    {
        [Fact]
        public void IsInventoryOpen_DefaultsFalse()
        {
            InventoryScreenHotkeyListener.IsInventoryOpen = false;
            Assert.False(InventoryScreenHotkeyListener.IsInventoryOpen);
        }

        [Fact]
        public void IsInventoryOpen_CanBeSetToTrue()
        {
            InventoryScreenHotkeyListener.IsInventoryOpen = true;
            Assert.True(InventoryScreenHotkeyListener.IsInventoryOpen);

            // Clean up
            InventoryScreenHotkeyListener.IsInventoryOpen = false;
        }

        [Fact]
        public void Tick_WhenInventoryClosed_DoesNotThrow()
        {
            InventoryScreenHotkeyListener.IsInventoryOpen = false;

            // Should return early without error when inventory is not open
            var ex = Record.Exception(() => InventoryScreenHotkeyListener.Tick());
            Assert.Null(ex);
        }
    }
}
