using System.Text.Json;
using System.Text;
using CommunityToolkit.Maui.Storage;
using SpreadsheetEngine;

namespace SpreadsheetApp;

public static class SpreadsheetFileService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly FilePickerFileType JsonType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.MacCatalyst] = ["public.json"],
        [DevicePlatform.iOS] = ["public.json"],
        [DevicePlatform.WinUI] = [".json"],
        [DevicePlatform.Android] = ["application/json"],
    });

    public static async Task<string?> SaveAsync(Spreadsheet sheet, string suggestedName)
    {
        string json = JsonSerializer.Serialize(sheet.Export(), Options);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await FileSaver.Default.SaveAsync(suggestedName, stream, CancellationToken.None);
        if (result.IsSuccessful) return result.FilePath;
        if (result.Exception is null or TaskCanceledException or OperationCanceledException) return null;
        if (result.Exception.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase)) return null;
        throw new IOException($"Не вдалося зберегти файл: {result.Exception.Message}", result.Exception);
    }

    public static async Task<(Spreadsheet Sheet, string Path)?> OpenAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Відкрити таблицю",
            FileTypes = JsonType
        });
        if (file == null) return null;

        await using var stream = await file.OpenReadAsync();
        SpreadsheetData? data;
        try
        {
            data = await JsonSerializer.DeserializeAsync<SpreadsheetData>(stream, Options);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Файл не є таблицею цієї програми або пошкоджений.", e);
        }
        if (data == null || data.Rows < 1 || data.Columns < 1 || data.Cells == null)
            throw new InvalidDataException("Файл не містить коректних даних таблиці.");

        return (Spreadsheet.Import(data), file.FileName);
    }
}
