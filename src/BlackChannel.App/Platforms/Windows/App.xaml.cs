using Microsoft.UI.Xaml;

namespace BlackChannel.App.WinUI;

/// <summary>Windows entry point — boots the shared MauiApp (same DI, same Razor UI).</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
