using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DiaEditCore.Tests
{
    public class SmokeTest
    {
        [Fact]
        public void Placeholder_ShouldPass()
        {
            // テスト基盤の疎通確認用。中身に意味はない。
            Assert.Equal(2, 1 + 1);
        }
    }
}
