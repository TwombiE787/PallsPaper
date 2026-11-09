namespace PallsPaper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var launchInBackground = Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(launchInBackground));
    }
}
