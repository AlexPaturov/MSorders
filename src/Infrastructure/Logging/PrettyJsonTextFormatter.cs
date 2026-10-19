using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace Orders.Infrastructure.Logging;

/// <summary>
/// Форматтер Serilog: сериализует событие в компактный JSON с готовой строкой
/// сообщения (<c>@m</c>) и переформатирует его в многострочный (с отступами)
/// JSON для читаемости файловых логов.
/// </summary>
public sealed class PrettyJsonTextFormatter : ITextFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly RenderedCompactJsonFormatter _inner = new();

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter();
        _inner.Format(logEvent, buffer);

        using var document = JsonDocument.Parse(buffer.ToString());
        var pretty = JsonSerializer.Serialize(document.RootElement, SerializerOptions);

        output.WriteLine(pretty);
    }
}
