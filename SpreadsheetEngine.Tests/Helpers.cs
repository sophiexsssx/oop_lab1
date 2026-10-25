using SpreadsheetEngine;

namespace SpreadsheetEngine.Tests;

internal static class Helpers
{
    public static string Eval(string expression)
    {
        var sheet = new Spreadsheet(1, 1);
        sheet.SetExpression("A1", expression);
        var cell = sheet["A1"];
        Assert.Null(cell.Error);
        return cell.Value!.Value.ToString();
    }
}
