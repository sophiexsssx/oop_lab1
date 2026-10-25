using static SpreadsheetEngine.Tests.Helpers;

namespace SpreadsheetEngine.Tests;

public class EvaluationTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("42", "42")]
    [InlineData("12345678901234567890123456789", "12345678901234567890123456789")]
    [InlineData("true", "ІСТИНА")]
    [InlineData("false", "ХИБНІСТЬ")]
    [InlineData("((7))", "7")]
    public void Constants(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("max(3, 7)", "7")]
    [InlineData("max(7, 3)", "7")]
    [InlineData("min(3, 7)", "3")]
    [InlineData("min(5, 5)", "5")]
    [InlineData("max(min(10, 4), 2)", "4")]
    [InlineData("max(99999999999999999999, 1)", "99999999999999999999")]
    [InlineData("max(true, 0)", "1")]
    public void MaxMin(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("5 = 5", "ІСТИНА")]
    [InlineData("5 = 6", "ХИБНІСТЬ")]
    [InlineData("3 < 5", "ІСТИНА")]
    [InlineData("5 < 3", "ХИБНІСТЬ")]
    [InlineData("5 < 5", "ХИБНІСТЬ")]
    [InlineData("5 > 3", "ІСТИНА")]
    [InlineData("99999999999999999999 > 99999999999999999998", "ІСТИНА")]
    [InlineData("true = (2 > 1)", "ІСТИНА")]
    [InlineData("true = 1", "ІСТИНА")]
    public void Comparisons(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("not true", "ХИБНІСТЬ")]
    [InlineData("not false", "ІСТИНА")]
    [InlineData("not 0", "ІСТИНА")]
    [InlineData("not 5", "ХИБНІСТЬ")]
    [InlineData("not not 5", "ІСТИНА")]
    public void Not(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("false", "false", "ХИБНІСТЬ", "ХИБНІСТЬ", "ІСТИНА")]
    [InlineData("false", "true", "ХИБНІСТЬ", "ІСТИНА", "ХИБНІСТЬ")]
    [InlineData("true", "false", "ХИБНІСТЬ", "ІСТИНА", "ХИБНІСТЬ")]
    [InlineData("true", "true", "ІСТИНА", "ІСТИНА", "ІСТИНА")]
    public void TruthTables_And_Or_Eqv(string a, string b, string and, string or, string eqv)
    {
        Assert.Equal(and, Eval($"{a} and {b}"));
        Assert.Equal(or, Eval($"{a} or {b}"));
        Assert.Equal(eqv, Eval($"{a} eqv {b}"));
    }

    [Theory]
    [InlineData("true or false and false", "ІСТИНА")]
    [InlineData("not 1 = 2", "ІСТИНА")]
    [InlineData("not true and false", "ХИБНІСТЬ")]
    [InlineData("false or true eqv true", "ІСТИНА")]
    [InlineData("true eqv false eqv false", "ІСТИНА")]
    [InlineData("(true or false) and false", "ХИБНІСТЬ")]
    public void Precedence(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("  max ( 1 ,   2 )   =   2  ")]
    [InlineData("NOT FALSE AND Max(1,2) = 2")]
    [InlineData("mіn(4, 9) = 4")]
    public void WhitespaceCaseAndCyrillic(string expr) => Assert.Equal("ІСТИНА", Eval(expr));
}
