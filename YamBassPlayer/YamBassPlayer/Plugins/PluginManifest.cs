using System.Text.Json;
using System.Text.RegularExpressions;

namespace YamBassPlayer.Plugins;

/// <summary>Манифест <c>plugins/&lt;id&gt;/plugin.json</c>.</summary>
public sealed partial class PluginManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public required string ContractVersion { get; init; }
    public string? EntryAssembly { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$")]
    private static partial Regex IdPattern();

    public static bool TryLoad(string manifestPath, out PluginManifest? manifest, out string? error)
    {
        manifest = null;
        error = null;

        try
        {
            var dto = JsonSerializer.Deserialize<ManifestDto>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dto is null)
            {
                error = "пустой манифест";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.Id) || !IdPattern().IsMatch(dto.Id))
            {
                error = $"некорректный id «{dto.Id}»";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                error = "не указано имя плагина";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.ContractVersion))
            {
                error = "не указана версия контракта";
                return false;
            }

            manifest = new PluginManifest
            {
                Id = dto.Id,
                Name = dto.Name,
                Version = dto.Version ?? "1.0.0",
                ContractVersion = dto.ContractVersion,
                EntryAssembly = dto.EntryAssembly,
                Description = dto.Description,
                Author = dto.Author
            };
            return true;
        }
        catch (Exception ex)
        {
            error = $"не удалось прочитать манифест: {ex.Message}";
            return false;
        }
    }

    private sealed class ManifestDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? ContractVersion { get; set; }
        public string? EntryAssembly { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
    }
}

/// <summary>Версия контракта, которую понимает хост.</summary>
public static class PluginContract
{
    public const int Major = 1;
    public const int Minor = 0;

    public static bool IsCompatible(string version)
    {
        string[] parts = version.Split('.');
        if (parts.Length < 2
            || !int.TryParse(parts[0], out int major)
            || !int.TryParse(parts[1], out int minor))
        {
            return false;
        }

        return major == Major && minor <= Minor;
    }
}
