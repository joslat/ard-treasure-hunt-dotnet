namespace Ard.AwardApp;

/// <summary>Command-line options for the award renderer.</summary>
public sealed class AwardOptions
{
    public const string DefaultName = "your name?";
    public const string DefaultDomain = "nullpointer.se";

    /// <summary>When set, render headlessly, save a PNG here, and exit.</summary>
    public string? ScreenshotPath { get; private set; }
    public string Name { get; private set; } = DefaultName;
    public string Domain { get; private set; } = DefaultDomain;

    /// <summary>Connect straight to this MCP endpoint instead of discovering it.</summary>
    public string? Endpoint { get; private set; }
    /// <summary>Walk the entire ARD trail to discover the challenge-3 endpoint.</summary>
    public bool Walk { get; private set; }
    /// <summary>Render a local award HTML file (offline) instead of fetching one.</summary>
    public string? HtmlPath { get; private set; }

    public int Width { get; private set; } = 940;
    public int Height { get; private set; } = 1200;
    public int Scale { get; private set; } = 2;
    public bool ShowHelp { get; private set; }

    public bool Headless => ScreenshotPath is not null;

    public static AwardOptions Parse(string[] args)
    {
        var o = new AwardOptions();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");
            int PosInt(string flag) { var v = Next(); return int.TryParse(v, out var n) && n > 0 ? n : throw new ArgumentException($"{flag} expects a positive integer, got '{v}'"); }
            switch (args[i].ToLowerInvariant())
            {
                case "--screenshot" or "-s": o.ScreenshotPath = Path.GetFullPath(Next()); break;
                case "--name" or "-n": o.Name = Next(); break;
                case "--domain": o.Domain = Next(); break;
                case "--endpoint" or "-e": o.Endpoint = Next(); break;
                case "--walk": o.Walk = true; break;
                case "--html": o.HtmlPath = Path.GetFullPath(Next()); break;
                case "--width": o.Width = PosInt("--width"); break;
                case "--height": o.Height = PosInt("--height"); break;
                case "--scale": o.Scale = PosInt("--scale"); break;
                case "-h" or "--help": o.ShowHelp = true; break;
                default: throw new ArgumentException($"Unknown argument '{args[i]}'");
            }
        }
        return o;
    }

    public const string HelpText = """
        ARD Award App — renders the challenge-3 MCP App and saves it as PNG.

          (no args)                 Open the interactive window (discovers the award via ARD).
          --screenshot <file.png>   Render headlessly, save a PNG, and exit.
          --name <name>             Name shown on the award (default: your name?).
          --endpoint <url>          Connect directly to this MCP endpoint.
          --walk                    Discover by walking the whole ARD trail.
          --html <file>             Render a local award HTML file (offline).
          --domain <d>              Seed domain for discovery (default: nullpointer.se).
          --width/--height <n>      Render size (default 940 x 1200).
          --scale <n>               Pixel scale for the PNG (default 2 = crisp).

        Examples
          dotnet run --project src/Ard.AwardApp
          dotnet run --project src/Ard.AwardApp -- --screenshot ard-output/award.png --name "your name?"
          dotnet run --project src/Ard.AwardApp -- --html artifacts/captured/award.html --screenshot ard-output/award.png
        """;
}
