using System.Reflection;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Service;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BetterGI.UnitTest." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class ConfigScope : IDisposable
{
    private static readonly PropertyInfo ConfigProperty =
        typeof(ConfigService).GetProperty("Config", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

    private readonly AllConfig? _previousConfig = (AllConfig?)ConfigProperty.GetValue(null);

    public ConfigScope(AllConfig config)
    {
        ConfigProperty.SetValue(null, config);
    }

    public void Dispose()
    {
        TaskContext.Instance().ClearRuntimeLaunchContext();
        ConfigProperty.SetValue(null, _previousConfig);
    }
}
