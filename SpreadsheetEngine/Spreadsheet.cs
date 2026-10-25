namespace SpreadsheetEngine;

public enum DisplayMode { Expression, Value }

public sealed class Cell
{
    public string Expression { get; internal set; } = "";
    internal Node? Ast { get; set; }

    public SyntaxErrorException? SyntaxError { get; internal set; }

    public Value? Value { get; internal set; }

    public string? Error { get; internal set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Expression);

    public string Display(DisplayMode mode) => mode switch
    {
        DisplayMode.Expression => Expression,
        _ when IsEmpty => "",
        _ when Error != null => "#ПОМИЛКА",
        _ => Value?.ToString() ?? ""
    };
}

public sealed class Spreadsheet : ICellValueProvider
{
    public const int MaxRows = 200;
    public const int MaxColumns = 100;

    private readonly List<List<Cell>> _cells = new();

    public int RowCount => _cells.Count;
    public int ColumnCount { get; private set; }

    public Spreadsheet(int rows = 10, int columns = 10)
    {
        if (rows < 1 || columns < 1) throw new ArgumentOutOfRangeException(nameof(rows), "Таблиця має містити хоча б одну клітинку");
        if (rows > MaxRows || columns > MaxColumns)
            throw new ArgumentOutOfRangeException(nameof(rows), $"Таблиця не може бути більшою за {MaxRows} × {MaxColumns}");
        ColumnCount = columns;
        for (int r = 0; r < rows; r++) _cells.Add(NewRow());
    }

    public Cell this[int row, int column] => _cells[row][column];
    public Cell this[CellAddress a] => _cells[a.Row][a.Column];
    public Cell this[string name] =>
        CellAddress.TryParse(name, out var a) && Contains(a) ? this[a]
            : throw new ArgumentException($"Немає клітинки {name}");

    public bool Contains(CellAddress a) => a.Row >= 0 && a.Row < RowCount && a.Column >= 0 && a.Column < ColumnCount;

    public void SetExpression(int row, int column, string expression)
    {
        SetWithoutRecalc(_cells[row][column], expression);
        Recalculate();
    }

    public void SetExpression(string cellName, string expression)
    {
        if (!CellAddress.TryParse(cellName, out var a) || !Contains(a))
            throw new ArgumentException($"Немає клітинки {cellName}");
        SetExpression(a.Row, a.Column, expression);
    }

    public void AddRow()
    {
        if (RowCount >= MaxRows) throw new InvalidOperationException($"Рядків не може бути більше за {MaxRows}");
        _cells.Add(NewRow());
        Recalculate();
    }

    public void AddColumn()
    {
        if (ColumnCount >= MaxColumns) throw new InvalidOperationException($"Стовпчиків не може бути більше за {MaxColumns}");
        ColumnCount++;
        foreach (var row in _cells) row.Add(new Cell());
        Recalculate();
    }

    public void RemoveLastRow()
    {
        if (RowCount <= 1) throw new InvalidOperationException("Не можна видалити єдиний рядок");
        _cells.RemoveAt(RowCount - 1);
        Recalculate();
    }

    public void RemoveLastColumn()
    {
        if (ColumnCount <= 1) throw new InvalidOperationException("Не можна видалити єдиний стовпчик");
        ColumnCount--;
        foreach (var row in _cells) row.RemoveAt(ColumnCount);
        Recalculate();
    }

    public IEnumerable<CellAddress> CellsReferencing(Func<CellAddress, bool> target)
    {
        for (int r = 0; r < RowCount; r++)
            for (int c = 0; c < ColumnCount; c++)
                if (_cells[r][c].Ast is { } ast && ast.References().Any(target))
                    yield return new CellAddress(r, c);
    }

    public SpreadsheetData Export()
    {
        var cells = new Dictionary<string, string>();
        for (int r = 0; r < RowCount; r++)
            for (int c = 0; c < ColumnCount; c++)
                if (!_cells[r][c].IsEmpty)
                    cells[new CellAddress(r, c).Name] = _cells[r][c].Expression;
        return new SpreadsheetData(RowCount, ColumnCount, cells);
    }

    public static Spreadsheet Import(SpreadsheetData data)
    {
        if (data.Rows < 1 || data.Columns < 1 || data.Rows > MaxRows || data.Columns > MaxColumns)
            throw new InvalidDataException(
                $"Файл пошкоджений: розмір {data.Rows} × {data.Columns} має бути від 1 × 1 до {MaxRows} × {MaxColumns}.");
        if (data.Cells == null)
            throw new InvalidDataException("Файл пошкоджений: немає списку клітинок.");

        var sheet = new Spreadsheet(data.Rows, data.Columns);
        foreach (var (name, expr) in data.Cells)
        {
            if (!CellAddress.TryParse(name, out var a))
                throw new InvalidDataException($"Файл пошкоджений: \"{name}\" не є адресою клітинки.");
            if (!sheet.Contains(a))
                throw new InvalidDataException(
                    $"Файл пошкоджений: клітинка {a.Name} поза таблицею {data.Rows} × {data.Columns}.");
            SetWithoutRecalc(sheet[a], expr ?? "");
        }
        sheet.Recalculate();
        return sheet;
    }

    private enum State { NotVisited, InProgress, Done }
    private State[,] _state = new State[0, 0];
    private readonly Stack<CellAddress> _path = new();
    private readonly Dictionary<CellAddress, string> _cycleMessages = new();

    public void Recalculate()
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try { RecalculateAll(); }
            catch (Exception e) { failure = e; }
        }, 256 * 1024 * 1024);
        worker.Start();
        worker.Join();
        if (failure != null) throw failure;
    }

    private void RecalculateAll()
    {
        _state = new State[RowCount, ColumnCount];
        _path.Clear();
        _cycleMessages.Clear();
        for (int r = 0; r < RowCount; r++)
            for (int c = 0; c < ColumnCount; c++)
                EvaluateCell(new CellAddress(r, c));
    }

    Value ICellValueProvider.GetCellValue(CellAddress a)
    {
        if (!Contains(a))
            throw new EvaluationException($"Посилання на клітинку {a.Name} за межами таблиці");

        EvaluateCell(a);
        var cell = this[a];
        if (cell.Error != null)
            throw new EvaluationException($"Клітинка {a.Name} містить помилку");
        return cell.Value ?? SpreadsheetEngine.Value.Zero;
    }

    private void EvaluateCell(CellAddress a)
    {
        var cell = this[a];
        switch (_state[a.Row, a.Column])
        {
            case State.Done:
                return;
            case State.InProgress:
                var members = _path.TakeWhile(p => p != a).Reverse().Prepend(a).ToList();
                string message = "Циклічне посилання: " + string.Join(" → ", members.Append(a).Select(p => p.Name));
                foreach (var m in members) _cycleMessages.TryAdd(m, message);
                throw new EvaluationException(message);
        }

        _state[a.Row, a.Column] = State.InProgress;
        _path.Push(a);
        try
        {
            if (cell.IsEmpty)
            {
                cell.Value = null;
                cell.Error = null;
            }
            else if (cell.SyntaxError != null)
            {
                cell.Value = null;
                cell.Error = cell.SyntaxError.Message;
            }
            else
            {
                try
                {
                    cell.Value = cell.Ast!.Evaluate(this);
                    cell.Error = null;
                }
                catch (EvaluationException e)
                {
                    cell.Value = null;
                    cell.Error = _cycleMessages.TryGetValue(a, out var cycleMessage) ? cycleMessage : e.Message;
                }
            }
        }
        finally
        {
            _path.Pop();
            _state[a.Row, a.Column] = State.Done;
        }
    }

    private static void SetWithoutRecalc(Cell cell, string expression)
    {
        cell.Expression = expression ?? "";
        cell.Ast = null;
        cell.SyntaxError = null;
        if (cell.IsEmpty) return;
        try { cell.Ast = Parser.Parse(cell.Expression); }
        catch (SyntaxErrorException e) { cell.SyntaxError = e; }
    }

    private List<Cell> NewRow() => Enumerable.Range(0, ColumnCount).Select(_ => new Cell()).ToList();
}

public sealed record SpreadsheetData(int Rows, int Columns, Dictionary<string, string> Cells);
