using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RevEx.Workers;

public sealed class BackendErrorQueue(IConfiguration configuration)
{
    internal string DirectoryPath { get; } = configuration["Kafka:ErrorQueuePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "error-queue");

    public async Task EnqueueAsync(BackendErrorEvent error)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, $"{error.EventId:N}.json");
        var temporaryPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(error, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            File.Move(temporaryPath, path);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
