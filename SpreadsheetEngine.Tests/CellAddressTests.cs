using SpreadsheetEngine;

namespace SpreadsheetEngine.Tests;

public class CellAddressTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ColumnName(int index, string name) => Assert.Equal(name, CellAddress.ColumnName(index));

    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("b12", 11, 1)]
    [InlineData("AA3", 2, 26)]
    public void TryParse_Valid(string text, int row, int column)
    {
        Assert.True(CellAddress.TryParse(text, out var a));
        Assert.Equal(new CellAddress(row, column), a);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("1A")]
    [InlineData("A1B")]
    public void TryParse_Invalid(string text) => Assert.False(CellAddress.TryParse(text, out _));

    [Fact]
    public void RoundTrip()
    {
        for (int c = 0; c < 1000; c += 37)
        {
            var a = new CellAddress(c, c);
            Assert.True(CellAddress.TryParse(a.Name, out var back));
            Assert.Equal(a, back);
        }
    }
}
