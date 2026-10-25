using SpreadsheetEngine;

namespace SpreadsheetApp;

public partial class MainPage : ContentPage
{
    private const int DefaultRows = 10;
    private const int DefaultColumns = 8;

    private Spreadsheet _sheet = new(DefaultRows, DefaultColumns);
    private string _fileName = "Нова таблиця";
    private bool _dirty;
    private CellAddress? _editingAddress;

    private static readonly Color ErrorColor = Color.FromArgb("#B3261E");
    private static readonly Color OkColor = Color.FromArgb("#188038");

    public MainPage()
    {
        InitializeComponent();
        Sheet.CellSelected += (_, _) =>
        {
            CommitPendingEdit();
            ShowSelectedCell();
        };
        LoadSheet(_sheet, "Нова таблиця");
    }


    private async void OnNewClicked(object? sender, EventArgs e)
    {
        if (!await ResolvePendingEditAsync()) return;
        if (!await ConfirmDiscardChangesAsync()) return;
        LoadSheet(new Spreadsheet(DefaultRows, DefaultColumns), "Нова таблиця");
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (!await ResolvePendingEditAsync()) return;
        if (!await ConfirmDiscardChangesAsync()) return;
        try
        {
            var opened = await SpreadsheetFileService.OpenAsync();
            if (opened is { } o) LoadSheet(o.Sheet, Path.GetFileName(o.Path));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Не вдалося відкрити файл", ex.Message, "OK");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e) => await SaveAsync();

    private async Task<bool> SaveAsync()
    {
        if (!await ResolvePendingEditAsync()) return false;
        try
        {
            string suggested = _fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? _fileName : _fileName + ".json";
            string? path = await SpreadsheetFileService.SaveAsync(_sheet, suggested);
            if (path == null) return false;
            _fileName = Path.GetFileName(path);
            _dirty = false;
            UpdateTitleAndStatus();
            StatusLabel.Text = $"Збережено: {path}";
            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Помилка збереження", ex.Message, "OK");
            return false;
        }
    }

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!_dirty) return true;
        string? choice = await DisplayActionSheetAsync(
            "У таблиці є незбережені зміни. Зберегти їх?", "Скасувати", null, "Зберегти", "Не зберігати");
        return choice switch
        {
            "Зберегти" => await SaveAsync(),
            "Не зберігати" => true,
            _ => false
        };
    }

    private bool HasPendingEdit(out CellAddress address, out string text)
    {
        address = _editingAddress ?? default;
        text = ExpressionEntry.Text ?? "";
        return _editingAddress is { } a && _sheet.Contains(a) && text != _sheet[a].Expression;
    }

    private void CommitPendingEdit()
    {
        if (!HasPendingEdit(out var address, out var text)) return;
        _sheet.SetExpression(address.Row, address.Column, text);
        _dirty = true;
        Sheet.Refresh();
    }

    private async Task<bool> ResolvePendingEditAsync()
    {
        if (!HasPendingEdit(out var address, out var text)) return true;
        if (string.IsNullOrWhiteSpace(text) || Parser.Validate(text) == null)
        {
            CommitPendingEdit();
            UpdateTitleAndStatus();
            return true;
        }

        string? choice = await DisplayActionSheetAsync(
            $"У клітинці {address.Name} є незастосований вираз із синтаксичною помилкою.",
            "Скасувати", null, "Застосувати з помилкою", "Відкинути зміни");
        switch (choice)
        {
            case "Застосувати з помилкою":
                CommitPendingEdit();
                Sheet.Refresh();
                UpdateTitleAndStatus();
                return true;
            case "Відкинути зміни":
                ExpressionEntry.Text = _sheet[address].Expression;
                return true;
            default:
                return false;
        }
    }

    private void LoadSheet(Spreadsheet sheet, string fileName)
    {
        _editingAddress = null;
        _sheet = sheet;
        _fileName = fileName;
        _dirty = false;
        Sheet.Bind(_sheet);
        Sheet.Select(new CellAddress(0, 0));
        UpdateTitleAndStatus();
    }

    private void OnApplyClicked(object? sender, EventArgs e)
    {
        var address = Sheet.Selected;
        string text = ExpressionEntry.Text ?? "";
        if (text == _sheet[address].Expression)
        {
            MoveSelectionDown();
            return;
        }

        _sheet.SetExpression(address.Row, address.Column, text);
        _dirty = true;
        Sheet.Refresh();
        UpdateTitleAndStatus();

        var cell = _sheet[address];
        if (cell.SyntaxError is { } syntax)
        {
            ShowCellInfo();
            HighlightError(syntax);
        }
        else if (cell.Error != null)
        {
            ShowCellInfo();
        }
        else
        {
            MoveSelectionDown();
        }
    }

    private void OnCancelEditClicked(object? sender, EventArgs e) => ShowSelectedCell();

    private void OnExpressionTextChanged(object? sender, TextChangedEventArgs e)
    {
        string text = e.NewTextValue ?? "";
        if (text == _sheet[Sheet.Selected].Expression)
        {
            ShowCellInfo();
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowMessage("Порожній вираз — клітинку буде очищено.", null);
            return;
        }
        var error = Parser.Validate(text);
        if (error == null)
            ShowMessage("✓ Синтаксис правильний. Натисніть Enter, щоб застосувати.", OkColor);
        else
            ShowMessage(error.Message + Environment.NewLine + Pointer(text, error), ErrorColor);
    }

    private void ShowSelectedCell()
    {
        var cell = _sheet[Sheet.Selected];
        _editingAddress = Sheet.Selected;
        CellNameLabel.Text = Sheet.Selected.Name;
        ExpressionEntry.Text = cell.Expression;
        ShowCellInfo();
        UpdateTitleAndStatus();
        ExpressionEntry.Focus();
    }

    private void ShowCellInfo()
    {
        var cell = _sheet[Sheet.Selected];
        InspectorCellLabel.Text = Sheet.Selected.Name;
        InspectorValueLabel.Text = cell.IsEmpty ? "порожня" : cell.Error != null ? "помилка" : cell.Value?.ToString() ?? "";
        if (cell.SyntaxError is { } syntax)
            ShowMessage(syntax.Message + Environment.NewLine + Pointer(cell.Expression, syntax), ErrorColor);
        else if (cell.Error != null)
            ShowMessage("Помилка обчислення: " + cell.Error, ErrorColor);
        else if (cell.IsEmpty)
            ShowMessage("Клітинка порожня (значення 0 / ХИБНІСТЬ).", null);
        else
            ShowMessage($"Значення {Sheet.Selected.Name} = {cell.Value}", null);
    }

    private void ShowMessage(string text, Color? color)
    {
        MessageLabel.Text = text;
        if (color != null)
            MessageLabel.TextColor = color;
        else
            MessageLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#9C6A80"), Color.FromArgb("#C9A0B3"));
    }

    private static string Pointer(string text, SyntaxErrorException e)
    {
        int start = Math.Clamp(e.Position - 1, 0, text.Length);
        return text + Environment.NewLine + new string(' ', start) + new string('^', e.Length);
    }

    private void HighlightError(SyntaxErrorException e)
    {
        ExpressionEntry.Focus();
        string text = ExpressionEntry.Text ?? "";
        int start = Math.Clamp(e.Position - 1, 0, text.Length);
        ExpressionEntry.CursorPosition = start;
        ExpressionEntry.SelectionLength = Math.Min(e.Length, text.Length - start);
    }

    private void MoveSelectionDown()
    {
        var s = Sheet.Selected;
        Sheet.Select(s.Row + 1 < _sheet.RowCount ? s with { Row = s.Row + 1 } : s);
    }

    private async void OnAddRowClicked(object? sender, EventArgs e) => await ResizeAsync(_sheet.AddRow);

    private async void OnAddColumnClicked(object? sender, EventArgs e) => await ResizeAsync(_sheet.AddColumn);

    private async Task ResizeAsync(Action resize)
    {
        CommitPendingEdit();
        try
        {
            resize();
            AfterResize();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Неможливо", ex.Message + ".", "OK");
        }
    }

    private async void OnRemoveRowClicked(object? sender, EventArgs e)
    {
        CommitPendingEdit();
        if (_sheet.RowCount <= 1)
        {
            await DisplayAlertAsync("Неможливо", "У таблиці має залишитися хоча б один рядок.", "OK");
            return;
        }
        int last = _sheet.RowCount - 1;
        if (!await ConfirmRemovalAsync($"рядок {last + 1}", a => a.Row == last)) return;
        _sheet.RemoveLastRow();
        AfterResize();
    }

    private async void OnRemoveColumnClicked(object? sender, EventArgs e)
    {
        CommitPendingEdit();
        if (_sheet.ColumnCount <= 1)
        {
            await DisplayAlertAsync("Неможливо", "У таблиці має залишитися хоча б один стовпчик.", "OK");
            return;
        }
        int last = _sheet.ColumnCount - 1;
        if (!await ConfirmRemovalAsync($"стовпчик {CellAddress.ColumnName(last)}", a => a.Column == last)) return;
        _sheet.RemoveLastColumn();
        AfterResize();
    }

    private async Task<bool> ConfirmRemovalAsync(string what, Func<CellAddress, bool> isRemoved)
    {
        var nonEmpty = new List<string>();
        for (int r = 0; r < _sheet.RowCount; r++)
            for (int c = 0; c < _sheet.ColumnCount; c++)
                if (isRemoved(new CellAddress(r, c)) && !_sheet[r, c].IsEmpty)
                    nonEmpty.Add(new CellAddress(r, c).Name);

        var referencing = _sheet.CellsReferencing(isRemoved)
            .Where(a => !isRemoved(a))
            .Select(a => a.Name)
            .ToList();

        if (nonEmpty.Count == 0 && referencing.Count == 0) return true;

        var lines = new List<string>();
        if (nonEmpty.Count > 0) lines.Add($"Буде втрачено вміст клітинок: {string.Join(", ", nonEmpty)}.");
        if (referencing.Count > 0) lines.Add($"На них посилаються клітинки {string.Join(", ", referencing)} — вони отримають помилку.");
        lines.Add("Продовжити?");

        return await DisplayAlertAsync($"Видалити {what}?", string.Join(Environment.NewLine, lines), "Видалити", "Скасувати");
    }

    private void AfterResize()
    {
        _dirty = true;
        Sheet.Bind(_sheet);
        ShowSelectedCell();
    }

    private void OnModeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value || Sheet == null) return;
        Sheet.Mode = ExpressionModeRadio.IsChecked ? DisplayMode.Expression : DisplayMode.Value;
        UpdateTitleAndStatus();
    }

    private async void OnHelpClicked(object? sender, EventArgs e) =>
        await Navigation.PushModalAsync(new HelpPage());

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await DisplayAlertAsync("Про програму",
            "Міні-таблиця — лабораторна робота № 1.\n\n" +
            "Електронна таблиця, клітинки якої містять логічні вирази з цілими числами довільної довжини, " +
            "посиланнями на клітинки (A1, B2, …), дужками, функціями max, min, " +
            "порівняннями =, <, > та операціями not, and, or, eqv.\n\n" +
            "Обчислення відбувається у два етапи: синтаксична перевірка з локалізацією помилок " +
            "і власне обчислення значення. Підтримуються режими ВИРАЗ / ЗНАЧЕННЯ, " +
            "зміна кількості рядків і стовпчиків, збереження й відкриття таблиць.\n\n" +
            "Детальніше — у розділі «Довідка».",
            "OK");

    private void UpdateTitleAndStatus()
    {
        string title = $"Міні-таблиця — {_fileName}{(_dirty ? " *" : "")}";
        Title = title;
        if (Window != null) Window.Title = title;

        RowCountLabel.Text = _sheet.RowCount.ToString();
        ColumnCountLabel.Text = _sheet.ColumnCount.ToString();
        StatusLabel.Text = _dirty ? $"{_fileName}: є незбережені зміни" : _fileName;
    }
}
