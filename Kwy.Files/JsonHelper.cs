using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kwy.Files;

/// <summary>
/// JSON serialization helper based on System.Text.Json.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Default options for JSON files. It is readable and tolerant of comments/trailing commas.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Default options for HTTP/MQTT payloads. It follows common web JSON conventions.
    /// </summary>
    public static JsonSerializerOptions WebOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    public static void Write<T>(string filePath, T obj, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        }

        FileSystemHelper.CreateDirectoryIfNotExists(filePath);
        string json = JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        File.WriteAllText(filePath, json, DefaultEncoding);
    }

    public static void Write<T>(string directory, string fileName, T obj, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        }

        Write(Path.Combine(directory, fileName), obj, options);
    }

    public static T? Read<T>(string filePath, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);
        }

        string json = File.ReadAllText(filePath, DefaultEncoding);
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
    }

    public static T? Read<T>(string directory, string fileName, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        }

        return Read<T>(Path.Combine(directory, fileName), options);
    }

    public static async Task WriteAsync<T>(string filePath, T obj, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        }

        FileSystemHelper.CreateDirectoryIfNotExists(filePath);
        string json = JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        await File.WriteAllTextAsync(filePath, json, DefaultEncoding).ConfigureAwait(false);
    }

    public static Task WriteAsync<T>(string directory, string fileName, T obj, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        }

        return WriteAsync(Path.Combine(directory, fileName), obj, options);
    }

    public static async Task<T?> ReadAsync<T>(string filePath, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);
        }

        string json = await File.ReadAllTextAsync(filePath, DefaultEncoding).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
    }

    public static Task<T?> ReadAsync<T>(string directory, string fileName, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        }

        return ReadAsync<T>(Path.Combine(directory, fileName), options);
    }

    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(obj, options ?? DefaultOptions);

    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
    }

    public static ValueTask<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return JsonSerializer.DeserializeAsync<T>(stream, options ?? DefaultOptions, cancellationToken);
    }
}
