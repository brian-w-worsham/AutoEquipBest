using Xunit;
using TaleWorlds.Core;
using static TaleWorlds.Core.ItemObject;

namespace AutoEquipBest.Tests
{
    public class AutoEquipLogicCanCharacterUseItemTests
    {
        [Fact]
        public void CanCharacterUseItem_NoDifficulty_ReturnsTrue()
        {
            // Items with difficulty 0 should always be usable
            var item = TestItemFactory.CreateSimpleItem(ItemTypeEnum.OneHandedWeapon, difficulty: 0);

            // When Hero.MainHero is null (no campaign loaded), should return true
            var result = AutoEquipLogic.CanCharacterUseItem(item);

            Assert.True(result);
        }

        [Fact]
        public void CanCharacterUseItem_DifficultyButNoHero_ReturnsTrue()
        {
            // With difficulty but no MainHero loaded, defaults to true
            var item = TestItemFactory.CreateSimpleItem(ItemTypeEnum.Bow, difficulty: 150);

            var result = AutoEquipLogic.CanCharacterUseItem(item);

            Assert.True(result);
        }
    }
}
