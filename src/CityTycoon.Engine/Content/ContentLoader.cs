using System.Reflection;
using System.Text.Json;

namespace CityTycoon.Engine.Content;

/// <summary>
/// Reads the JSON files from the <c>/content</c> folder.
///
/// The files are "embedded" into the Engine when it is built (see the
/// <c>EmbeddedResource</c> line in CityTycoon.Engine.csproj). That means the
/// Engine always carries its own copy of the content, so it works the same in
/// tests, in the Sim console app, and inside the web browser, where there is
/// no normal file system to read from.
/// </summary>
public static class ContentLoader
{
    /// <summary>
    /// JSON settings shared by every content file: property names in the files
    /// are camelCase ("startingCash"), while C# uses PascalCase ("StartingCash").
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RulesConfig LoadRules() => Load<RulesConfig>("rules.json");

    /// <summary>Reads one embedded content file and turns it into a C# object.</summary>
    public static T Load<T>(string fileName)
    {
        string json = ReadText(fileName);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new ContentException($"{fileName} is empty.");
        }
        catch (JsonException ex)
        {
            throw new ContentException($"{fileName} could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>Returns the raw text of one embedded content file.</summary>
    public static string ReadText(string fileName)
    {
        Assembly assembly = typeof(ContentLoader).Assembly;
        string resourceName = "CityTycoon.Content." + fileName;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ContentException($"Content file '{fileName}' was not found. Is it in the /content folder?");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Thrown when a content file is missing or has a mistake in it.</summary>
public sealed class ContentException(string message, Exception? inner = null) : Exception(message, inner);
