using System.Text.Json;
using SpreadsheetEngine;

namespace SpreadsheetEngine.Tests;

public class SpreadsheetTests
{
    private static string V(Spreadsheet s, string cell) => s[cell].Display(DisplayMode.Value);

    [Fact]
    public void References_AreEvaluated()
    {
        var s = new Spreadsheet(3, 3);
        s.SetExpression("A1", "10");
        s.SetExpression("B1", "max(A1, 25)");
        s.SetExpression("C1", "B1 > A1 and not (A1 = 0)");
        Assert.Equal("25", V(s, "B1"));
        Assert.Equal("ІСТИНА", V(s, "C1"));
    }

    [Fact]
    public void ChangingCell_RecalculatesDependents()
    {
        var s = new Spreadsheet(3, 3);
        s.SetExpression("A1", "10");
        s.SetExpression("B1", "max(A1, 25)");
        s.SetExpression("C1", "B1 > A1");
        s.SetExpression("A1", "30");
        Assert.Equal("30", V(s, "B1"));
        Assert.Equal("ХИБНІСТЬ", V(s, "C1"));
    }

    [Fact]
    public void ReferenceOrder_DoesNotMatter()
    {
        var s = new Spreadsheet(3, 3);
        s.SetExpression("A1", "C3 = 5");
        s.SetExpression("C3", "5");
        Assert.Equal("ІСТИНА", V(s, "A1"));
    }

    [Fact]
    public void EmptyCell_IsZero()
    {
        var s = new Spreadsheet(2, 2);
        s.SetExpression("A1", "B2 = 0");
        s.SetExpression("A2", "not B2");
        Assert.Equal("ІСТИНА", V(s, "A1"));
        Assert.Equal("ІСТИНА", V(s, "A2"));
        Assert.Equal("", V(s, "B2"));
    }

    [Fact]
    public void CyrillicCellName_Works()
    {
        var s = new Spreadsheet(2, 2);
        s.SetExpression("A1", "7");
        s.SetExpression("B1", "А1 = 7");
        Assert.Equal("ІСТИНА", V(s, "B1"));
    }

    [Fact]
    public void DisplayModes()
    {
        var s = new Spreadsheet(1, 1);
        s.SetExpression("A1", "max(1, 2) > 1");
        Assert.Equal("max(1, 2) > 1", s["A1"].Display(DisplayMode.Expression));
        Assert.Equal("ІСТИНА", s["A1"].Display(DisplayMode.Value));
    }

    [Fact]
    public void Cycle_IsDetected_ForAllMembers()
    {
        var s = new Spreadsheet(1, 4);
        s.SetExpression("A1", "B1");
        s.SetExpression("B1", "C1");
        s.SetExpression("C1", "A1 or true");
        s.SetExpression("D1", "A1");

        const string cycle = "Циклічне посилання: A1 → B1 → C1 → A1";
        Assert.Equal(cycle, s["A1"].Error);
        Assert.Equal(cycle, s["B1"].Error);
        Assert.Equal(cycle, s["C1"].Error);
        Assert.Equal("Клітинка A1 містить помилку", s["D1"].Error);
        Assert.Equal("#ПОМИЛКА", V(s, "D1"));
    }

    [Fact]
    public void SelfReference_IsCycle()
    {
        var s = new Spreadsheet(1, 1);
        s.SetExpression("A1", "not A1");
        Assert.Equal("Циклічне посилання: A1 → A1", s["A1"].Error);
    }

    [Fact]
    public void BreakingCycle_RestoresValues()
    {
        var s = new Spreadsheet(1, 2);
        s.SetExpression("A1", "B1");
        s.SetExpression("B1", "A1");
        s.SetExpression("B1", "1");
        Assert.Null(s["A1"].Error);
        Assert.Equal("1", V(s, "A1"));
    }

    [Fact]
    public void SyntaxError_PropagatesToDependents()
    {
        var s = new Spreadsheet(1, 2);
        s.SetExpression("A1", "max(1,");
        s.SetExpression("B1", "A1 = 1");
        Assert.NotNull(s["A1"].SyntaxError);
        Assert.Equal(7, s["A1"].SyntaxError!.Position);
        Assert.Equal("Клітинка A1 містить помилку", s["B1"].Error);
    }

    [Fact]
    public void ReferenceOutsideTable_IsError()
    {
        var s = new Spreadsheet(2, 2);
        s.SetExpression("A1", "Z99");
        Assert.Equal("Посилання на клітинку Z99 за межами таблиці", s["A1"].Error);
    }

    [Fact]
    public void AddAndRemoveColumn()
    {
        var s = new Spreadsheet(2, 2);
        s.SetExpression("A1", "C1 = 0");
        Assert.NotNull(s["A1"].Error);

        s.AddColumn();
        Assert.Equal(3, s.ColumnCount);
        Assert.Equal("ІСТИНА", V(s, "A1"));
        Assert.Equal(["A1"], s.CellsReferencing(a => a.Column == 2).Select(a => a.Name));

        s.RemoveLastColumn();
        Assert.Equal(2, s.ColumnCount);
        Assert.NotNull(s["A1"].Error);
    }

    [Fact]
    public void AddAndRemoveRow()
    {
        var s = new Spreadsheet(1, 1);
        s.AddRow();
        s.SetExpression("A2", "5");
        s.SetExpression("A1", "A2 > 1");
        Assert.Equal("ІСТИНА", V(s, "A1"));
        s.RemoveLastRow();
        Assert.Equal(1, s.RowCount);
        Assert.NotNull(s["A1"].Error);
    }

    [Fact]
    public void CannotRemoveLastRowOrColumn()
    {
        var s = new Spreadsheet(1, 1);
        Assert.Throws<InvalidOperationException>(s.RemoveLastRow);
        Assert.Throws<InvalidOperationException>(s.RemoveLastColumn);
    }

    [Fact]
    public void ExportImport_ThroughJson()
    {
        var s = new Spreadsheet(3, 2);
        s.SetExpression("A1", "10");
        s.SetExpression("B3", "max(A1, 3) = 10");

        string json = JsonSerializer.Serialize(s.Export());
        var copy = Spreadsheet.Import(JsonSerializer.Deserialize<SpreadsheetData>(json)!);

        Assert.Equal(3, copy.RowCount);
        Assert.Equal(2, copy.ColumnCount);
        Assert.Equal("max(A1, 3) = 10", copy["B3"].Expression);
        Assert.Equal("ІСТИНА", V(copy, "B3"));
    }

    [Fact]
    public void ClearingCell_RemovesValueAndError()
    {
        var s = new Spreadsheet(1, 1);
        s.SetExpression("A1", "max(");
        s.SetExpression("A1", "   ");
        Assert.True(s["A1"].IsEmpty);
        Assert.Null(s["A1"].Error);
        Assert.Null(s["A1"].Value);
    }
}
