using System.Text.Json;

namespace SpreadsheetEngine.Tests;

public class ImportAndLimitTests
{
    private static SpreadsheetData Data(int rows, int columns, Dictionary<string, string> cells) => new(rows, columns, cells);

    [Fact]
    public void Import_InvalidAddress_IsRejected()
    {
        var e = Assert.Throws<InvalidDataException>(() =>
            Spreadsheet.Import(Data(3, 3, new() { ["A1"] = "1", ["hello"] = "2" })));
        Assert.Contains("\"hello\" не є адресою клітинки", e.Message);
    }

    [Fact]
    public void Import_CellOutsideTable_IsRejected()
    {
        var e = Assert.Throws<InvalidDataException>(() =>
            Spreadsheet.Import(Data(2, 2, new() { ["A1"] = "1", ["C5"] = "2" })));
        Assert.Contains("C5 поза таблицею 2 × 2", e.Message);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(Spreadsheet.MaxRows + 1, 5)]
    [InlineData(5, Spreadsheet.MaxColumns + 1)]
    [InlineData(1000000, 1000000)]
    public void Import_BadDimensions_AreRejected(int rows, int columns) =>
        Assert.Throws<InvalidDataException>(() => Spreadsheet.Import(Data(rows, columns, new())));

    [Fact]
    public void Import_MissingCells_IsRejected() =>
        Assert.Throws<InvalidDataException>(() => Spreadsheet.Import(new SpreadsheetData(2, 2, null!)));

    [Fact]
    public void Import_NullExpression_BecomesEmptyCell()
    {
        var sheet = Spreadsheet.Import(Data(2, 2, new() { ["A1"] = null! }));
        Assert.True(sheet["A1"].IsEmpty);
    }

    [Fact]
    public void Import_MalformedJson_FailsToDeserialize() =>
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<SpreadsheetData>("{ \"Rows\": 2, \"Columns\": "));

    [Fact]
    public void ExportImport_KeepsEveryCell()
    {
        var s = new Spreadsheet(4, 3);
        s.SetExpression("A1", "5");
        s.SetExpression("C4", "A1 > 1");
        s.SetExpression("B2", "max(1,");
        var copy = Spreadsheet.Import(JsonSerializer.Deserialize<SpreadsheetData>(JsonSerializer.Serialize(s.Export()))!);
        Assert.Equal(s.Export().Cells, copy.Export().Cells);
        Assert.NotNull(copy["B2"].SyntaxError);
    }

    [Fact]
    public void Constructor_RejectsTooLargeTable() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spreadsheet(Spreadsheet.MaxRows + 1, 1));

    [Fact]
    public void AddRowAndColumn_StopAtLimit()
    {
        var s = new Spreadsheet(Spreadsheet.MaxRows, Spreadsheet.MaxColumns);
        Assert.Throws<InvalidOperationException>(s.AddRow);
        Assert.Throws<InvalidOperationException>(s.AddColumn);
        Assert.Equal(Spreadsheet.MaxRows, s.RowCount);
        Assert.Equal(Spreadsheet.MaxColumns, s.ColumnCount);
    }

    [Fact]
    public void DeeplyNestedParentheses_AreSyntaxError()
    {
        string expr = new string('(', 5000) + "1" + new string(')', 5000);
        var error = Parser.Validate(expr);
        Assert.NotNull(error);
        Assert.Contains("занадто глибоко", error.Message);
    }

    [Fact]
    public void LongNotChain_IsSyntaxError()
    {
        string expr = string.Concat(Enumerable.Repeat("not ", 5000)) + "1";
        Assert.Contains("занадто глибоко", Parser.Validate(expr)!.Message);
    }

    [Fact]
    public void NestingWithinLimit_IsAllowed()
    {
        string expr = new string('(', 50) + "1" + new string(')', 50);
        Assert.Null(Parser.Validate(expr));
    }

    [Fact]
    public void LongReferenceChain_DoesNotOverflow()
    {
        var cells = new Dictionary<string, string>();
        int total = Spreadsheet.MaxRows * Spreadsheet.MaxColumns;
        for (int i = 0; i < total - 1; i++)
        {
            var here = new CellAddress(i / Spreadsheet.MaxColumns, i % Spreadsheet.MaxColumns);
            var next = new CellAddress((i + 1) / Spreadsheet.MaxColumns, (i + 1) % Spreadsheet.MaxColumns);
            cells[here.Name] = next.Name;
        }
        cells[new CellAddress(Spreadsheet.MaxRows - 1, Spreadsheet.MaxColumns - 1).Name] = "1 = 1";
        var sheet = Spreadsheet.Import(new SpreadsheetData(Spreadsheet.MaxRows, Spreadsheet.MaxColumns, cells));
        Assert.Equal("ІСТИНА", sheet["A1"].Display(DisplayMode.Value));
    }
}
