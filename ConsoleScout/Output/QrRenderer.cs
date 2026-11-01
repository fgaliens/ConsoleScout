using System.Text;
using QRCoder;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleScout.Output;

/// <summary>Draws a QR code with half-block characters, two modules per character cell. Colors are fixed to
/// black on white rather than taken from the theme, so phone cameras read it on dark and light terminals alike.</summary>
internal static class QrRenderer
{
    /// <summary>QRCoder adds a 4-module quiet zone; 2 is enough on screen and keeps the code compact.</summary>
    private const int TrimmedQuietModules = 2;

    private const string Colors = "black on white";

    public static IRenderable Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        var matrix = data.ModuleMatrix;
        var first = TrimmedQuietModules;
        var last = matrix.Count - TrimmedQuietModules;

        var lines = new List<IRenderable>();
        for (var row = first; row < last; row += 2)
        {
            var line = new StringBuilder();
            for (var column = first; column < last; column++)
            {
                var top = matrix[row][column];
                var bottom = row + 1 < last && matrix[row + 1][column];
                var cell = (top, bottom) switch
                {
                    (true, true) => '█',
                    (true, false) => '▀',
                    (false, true) => '▄',
                    _ => ' ',
                };
                line.Append(cell);
            }

            var cells = line.ToString();
            lines.Add(new Markup($"[{Colors}]{cells}[/]"));
        }

        return new Rows(lines);
    }
}
