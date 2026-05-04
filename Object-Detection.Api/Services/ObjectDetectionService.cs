using Microsoft.Extensions.Options;
using Object_Detection;
using Object_Detection.Api.Infrastructure.ErrorHandling;
using Object_Detection.Api.Options;

namespace Object_Detection.Api.Services;

public sealed class ObjectDetectionService : IObjectDetectionService
{
    private readonly object _syncRoot = new();
    private readonly ILogger<ObjectDetectionService> _logger;
    private readonly IOptionsMonitor<ObjectDetectionApiOptions> _options;
    private ObjectDetectionRuntime? _runtime;
    private string? _loadError;
    private DateTimeOffset? _loadedAtUtc;

    public ObjectDetectionService(
        ILogger<ObjectDetectionService> logger,
        IOptionsMonitor<ObjectDetectionApiOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public Task<ObjectDetectionServiceHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = EnsureRuntime();
        var isLoaded = runtime is not null;

        return Task.FromResult(new ObjectDetectionServiceHealth(
            IsHealthy: isLoaded,
            ModelLoaded: isLoaded,
            Message: isLoaded ? "Model is ready." : _loadError ?? "Model is not loaded.",
            ModelFilePath: ResolveModelPath(),
            ModelLoadedAtUtc: _loadedAtUtc));
    }

    public Task<ObjectDetectionModelInfo> GetModelInfoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runtime = EnsureRuntime();
        if (runtime is null)
        {
            throw new ApiException("MODEL_NOT_READY", _loadError ?? "Model is not loaded.", StatusCodes.Status503ServiceUnavailable);
        }

        return Task.FromResult(runtime.GetModelInfo());
    }

    public Task<ObjectDetectionInferenceResult> DetectAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runtime = EnsureRuntime();
        if (runtime is null)
        {
            throw new ApiException("MODEL_NOT_READY", _loadError ?? "Model is not loaded.", StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            return Task.FromResult(runtime.Detect(imageBytes));
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiException("INVALID_IMAGE", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private ObjectDetectionRuntime? EnsureRuntime()
    {
        if (_runtime is not null)
        {
            return _runtime;
        }

        lock (_syncRoot)
        {
            if (_runtime is not null)
            {
                return _runtime;
            }

            var modelPath = ResolveModelPath();
            if (!ObjectDetectionRuntime.TryLoadFromFile(modelPath, out var runtime, out var error))
            {
                _loadError = error;
                _logger.LogWarning("Cannot load model from {ModelPath}: {Error}", modelPath, error);
                return null;
            }

            _runtime = runtime;
            _loadedAtUtc = DateTimeOffset.UtcNow;
            _loadError = null;

            _logger.LogInformation("Model loaded successfully from {ModelPath}", modelPath);
            return _runtime;
        }
    }

    private string ResolveModelPath()
    {
        var configuredPath = _options.CurrentValue.ModelFilePath;
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", configuredPath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath))
        };

        var matched = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            return matched;
        }

        return candidates[0];
    }
}
