using SpreadsheetEngine;

namespace SpreadsheetApp;

public sealed class SheetView : ContentView
{
    private const double CellWidth = 120;
    private const double CellHeight = 34;
    private const double HeaderWidth = 46;

    private readonly Grid _grid = new() { ColumnSpacing = 0, RowSpacing = 0 };
    private Border[,] _borders = new Border[0, 0];
    private Label[,] _labels = new Label[0, 0];
    private Label[] _columnHeaders = [];
    private Label[] _rowHeaders = [];

    private Spreadsheet? _sheet;
    private DisplayMode _mode = DisplayMode.Value;
    private CellAddress _selected = new(0, 0);

    public event EventHandler<CellAddress>? CellSelected;

    public SheetView()
    {
        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = _grid
        };
    }

    public CellAddress Selected => _selected;

    public DisplayMode Mode
    {
        get => _mode;
        set { _mode = value; Refresh(); }
    }

    public void Bind(Spreadsheet sheet)
    {
        _sheet = sheet;
        if (!sheet.Contains(_selected))
            _selected = new CellAddress(Math.Min(_selected.Row, sheet.RowCount - 1), Math.Min(_selected.Column, sheet.ColumnCount - 1));
        Rebuild();
    }

    public void Select(CellAddress address)
    {
        if (_sheet == null || !_sheet.Contains(address)) return;
        var old = _selected;
        _selected = address;
        StyleCell(old);
        StyleCell(address);
        StyleHeaders();
        CellSelected?.Invoke(this, address);
    }

    public void Refresh()
    {
        if (_sheet == null) return;
        for (int r = 0; r < _sheet.RowCount; r++)
            for (int c = 0; c < _sheet.ColumnCount; c++)
                StyleCell(new CellAddress(r, c));
        StyleHeaders();
    }

    private void Rebuild()
    {
        var sheet = _sheet!;
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();

        _grid.ColumnDefinitions.Add(new ColumnDefinition(HeaderWidth));
        for (int c = 0; c < sheet.ColumnCount; c++) _grid.ColumnDefinitions.Add(new ColumnDefinition(CellWidth));
        _grid.RowDefinitions.Add(new RowDefinition(CellHeight));
        for (int r = 0; r < sheet.RowCount; r++) _grid.RowDefinitions.Add(new RowDefinition(CellHeight));

        _grid.Add(Wrap(Header("")), 0, 0);

        _columnHeaders = new Label[sheet.ColumnCount];
        for (int c = 0; c < sheet.ColumnCount; c++)
        {
            _columnHeaders[c] = Header(CellAddress.ColumnName(c));
            _grid.Add(Wrap(_columnHeaders[c]), c + 1, 0);
        }

        _rowHeaders = new Label[sheet.RowCount];
        for (int r = 0; r < sheet.RowCount; r++)
        {
            _rowHeaders[r] = Header((r + 1).ToString());
            _grid.Add(Wrap(_rowHeaders[r]), 0, r + 1);
        }

        _borders = new Border[sheet.RowCount, sheet.ColumnCount];
        _labels = new Label[sheet.RowCount, sheet.ColumnCount];
        for (int r = 0; r < sheet.RowCount; r++)
        {
            for (int c = 0; c < sheet.ColumnCount; c++)
            {
                var address = new CellAddress(r, c);
                var label = new Label
                {
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    Padding = new Thickness(6, 0),
                    FontSize = 14
                };
                var border = new Border
                {
                    Content = label,
                    StrokeThickness = 1,
                    Padding = 0
                };
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => Select(address);
                border.GestureRecognizers.Add(tap);

                _labels[r, c] = label;
                _borders[r, c] = border;
                _grid.Add(border, c + 1, r + 1);
            }
        }
        Refresh();
    }

    private void StyleCell(CellAddress a)
    {
        if (_sheet == null || !_sheet.Contains(a) || a.Row >= _labels.GetLength(0) || a.Column >= _labels.GetLength(1)) return;

        var cell = _sheet[a];
        var label = _labels[a.Row, a.Column];
        var border = _borders[a.Row, a.Column];
        bool selected = a == _selected;
        bool error = cell.Error != null;

        label.Text = cell.Display(_mode);
        label.HorizontalTextAlignment = _mode == DisplayMode.Value && cell.Value is { IsBoolean: false }
            ? TextAlignment.End
            : TextAlignment.Start;
        bool truthy = _mode == DisplayMode.Value && cell.Value is { IsBoolean: true } v && v.AsBool();
        bool falsy = _mode == DisplayMode.Value && cell.Value is { IsBoolean: true } f && !f.AsBool();
        label.FontAttributes = truthy ? FontAttributes.Bold : FontAttributes.None;

        if (error)
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B3261E"), Color.FromArgb("#FFB4AB"));
        else if (_mode == DisplayMode.Expression)
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#A3245A"), Color.FromArgb("#F4B6CC"));
        else if (truthy)
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#A3245A"), Color.FromArgb("#FFD6E5"));
        else if (falsy)
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B89AA8"), Color.FromArgb("#7E6470"));
        else
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#3B1F2B"), Color.FromArgb("#F3E6EC"));

        if (error)
            border.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#FDECEA"), Color.FromArgb("#3B1F1D"));
        else if (truthy)
            border.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#FCE4EC"), Color.FromArgb("#4A2436"));
        else
            border.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1F161A"));

        border.StrokeThickness = selected ? 2 : 0.5;
        if (selected)
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#A3245A"), Color.FromArgb("#F4B6CC"));
        else
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#F4D7E2"), Color.FromArgb("#3E2A33"));

        ToolTipProperties.SetText(border, cell.Error ?? (cell.IsEmpty ? "" : $"{a.Name}: {cell.Expression}"));
        border.ZIndex = selected ? 1 : 0;
    }

    private void StyleHeaders()
    {
        for (int c = 0; c < _columnHeaders.Length; c++)
            HighlightHeader(_columnHeaders[c], c == _selected.Column);
        for (int r = 0; r < _rowHeaders.Length; r++)
            HighlightHeader(_rowHeaders[r], r == _selected.Row);
    }

    private static void HighlightHeader(Label header, bool active)
    {
        header.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        if (active)
            header.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#A3245A"), Color.FromArgb("#F4B6CC"));
        else
            header.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#8A6878"), Color.FromArgb("#C9A9B7"));
    }

    private static Label Header(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 13
        };
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#8A6878"), Color.FromArgb("#C9A9B7"));
        return label;
    }

    private static Border Wrap(Label header)
    {
        var border = new Border { Content = header, StrokeThickness = 1, Padding = 0 };
        border.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F7F3F5"), Color.FromArgb("#2A1F24"));
        border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#F4D7E2"), Color.FromArgb("#3E2A33"));
        return border;
    }
}
