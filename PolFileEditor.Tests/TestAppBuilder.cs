using Avalonia;
using Avalonia.Headless;
using PolFileEditor;

namespace PolFileEditor.Tests;

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
