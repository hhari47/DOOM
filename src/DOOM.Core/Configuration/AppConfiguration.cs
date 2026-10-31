namespace DOOM.Core.Configuration;

using System.Reflection;
using DOOM.Core.Domain;
using DOOM.Core.Errors;

/// <summary>
/// Immutable application metadata and launch configuration.
/// </summary>
public sealed record AppConfiguration
{
    public string AppName { get; init; } = "DOOM";
    public string Version { get; init; } = "1.0.0";
    public string EnvironmentName { get; init; } = "Production";
    public ModelId DefaultModel { get; init; } = ModelId.Default;

    public static AppConfiguration Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var envModel = Environment.GetEnvironmentVariable("DOOM_MODEL");

        ModelId model = ModelId.Default;
        if (!string.IsNullOrWhiteSpace(envModel))
        {
            if (!ModelId.TryCreate(envModel, out var parsedModel))
            {
                throw new ConfigurationException("DOOM_MODEL", $"Invalid environment model identifier: '{envModel}'");
            }
            model = parsedModel!;
        }

        return new AppConfiguration
        {
            AppName = "DOOM",
            Version = version,
            EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            DefaultModel = model
        };
    }
}

