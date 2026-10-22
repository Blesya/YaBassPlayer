using System.Text.RegularExpressions;

namespace YamBassPlayer.Models;

/// <summary>
/// Идентификатор источника треков.
/// </summary>
public readonly record struct SourceId
{
    private const int MaxLength = 64;

    private static readonly Regex Format = new("^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant);

    public SourceId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || !Format.IsMatch(value))
        {
            throw new ArgumentException(
                "Идентификатор источника должен состоять из строчных латинских букв, цифр, точек и дефисов, " +
                "начинаться с буквы или цифры и иметь длину от 1 до 64 символов.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => Value is null;

    public override string ToString() => Value ?? string.Empty;
}
