using System.Runtime.InteropServices;
using Ard.AwardApp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // A WinExe normally has no console; attach the parent terminal so --screenshot logs are visible.
        AttachConsole(AttachParentProcess);

        AwardOptions opts;
        try { opts = AwardOptions.Parse(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); Console.WriteLine(AwardOptions.HelpText); return 1; }

        if (opts.ShowHelp) { Console.WriteLine(AwardOptions.HelpText); return 0; }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new AwardForm(opts);
        Application.Run(form);
        return form.ExitCode;
    }

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
