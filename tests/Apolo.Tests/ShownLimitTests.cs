using System.Collections.Generic;
using System.Linq;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class ShownLimitTests
    {
        // items are "folder:number"; the first of each folder starts a group
        private static List<string> Items(params int[] counts)
        {
            List<string> list = new List<string>();
            for (int g = 0; g < counts.Length; g++)
            {
                for (int i = 0; i < counts[g]; i++)
                    list.Add("g" + g + ":" + i);
            }
            return list;
        }

        private static bool Starts(string item)
        {
            return item.EndsWith(":0");
        }

        [Fact]
        public void UnderTheLimit_NothingChanges()
        {
            List<List<string>> collapsed;
            int hidden;
            List<string> all = Items(10, 20);
            List<string> kept = ShownLimit.Apply(all, 200, Starts, out collapsed, out hidden);
            Assert.Equal(30, kept.Count);
            Assert.Empty(collapsed);
            Assert.Equal(0, hidden);
        }

        [Fact]
        public void MotherFolderWithManyPads_KeepsTheFirstGroups_AndCollapsesTheRest()
        {
            List<List<string>> collapsed;
            int hidden;
            List<string> kept = ShownLimit.Apply(Items(35, 35, 35, 35, 35, 35, 35, 35), 200, Starts, out collapsed, out hidden);
            Assert.Equal(175, kept.Count);                       // five groups of 35
            Assert.Equal(3, collapsed.Count);
            Assert.All(collapsed, g => Assert.Equal(35, g.Count));
            Assert.Equal("g5:0", collapsed[0][0]);
            Assert.Equal(0, hidden);
        }

        [Fact]
        public void OneHugeFolder_ShowsTheFirstButtons_AndCountsTheOthers()
        {
            List<List<string>> collapsed;
            int hidden;
            List<string> kept = ShownLimit.Apply(Items(1316), 200, Starts, out collapsed, out hidden);
            Assert.Equal(200, kept.Count);
            Assert.Equal(1116, hidden);
            Assert.Empty(collapsed);
        }

        [Fact]
        public void HugeFirstGroup_CollapsesTheGroupsAfterIt()
        {
            List<List<string>> collapsed;
            int hidden;
            List<string> kept = ShownLimit.Apply(Items(300, 5, 5), 200, Starts, out collapsed, out hidden);
            Assert.Equal(200, kept.Count);
            Assert.Equal(100, hidden);
            Assert.Equal(2, collapsed.Count);
        }

        [Fact]
        public void ABigGroupAfterTheLimit_IsCollapsed_EvenIfSmallOnesFollow()
        {
            List<List<string>> collapsed;
            int hidden;
            List<string> kept = ShownLimit.Apply(Items(150, 100, 10), 200, Starts, out collapsed, out hidden);
            Assert.Equal(150, kept.Count);
            Assert.Equal(2, collapsed.Count);                    // the order of the groups is kept: no group is shown after a collapsed one
            Assert.Equal(100, collapsed[0].Count);
        }
    }
}
