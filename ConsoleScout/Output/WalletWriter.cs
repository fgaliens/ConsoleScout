using ConsoleScout.Donate;
using Spectre.Console;

namespace ConsoleScout.Output;

/// <summary>--donate: the list of networks to donate on, and, for one network, its address with a scannable
/// QR code and what must not be sent to it - always under a warning that a wrong network loses the funds.</summary>
internal sealed class WalletWriter(ConsoleTheme theme)
{
    private const string NoWallets = "No donation wallets are set up in this build yet. Thank you for the thought!";

    /// <summary>The networks with their codes, and how to show one of them.</summary>
    public void WriteList(IReadOnlyList<Wallet> wallets)
    {
        if (wallets.Count == 0)
        {
            AnsiConsole.WriteLine(NoWallets);
            return;
        }

        AnsiConsole.WriteLine("Thank you for supporting ConsoleScout! Donations are accepted on these networks:");
        AnsiConsole.WriteLine();

        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = Style.Parse(theme.TableBorder),
        };
        table.AddColumn("Code");
        table.AddColumn("Network");

        foreach (var wallet in wallets)
        {
            var code = ConsoleMarkup.Styled(wallet.Code, theme.Code);
            var network = Markup.Escape(wallet.Network);
            table.AddRow(code, network);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var example = wallets[0].Code;
        AnsiConsole.MarkupLine($"Run [bold]csc --donate <code>[/] (e.g. [bold]csc --donate {example}[/]) for the address and QR code.");
    }

    /// <summary>One wallet: address, QR code and network warning.</summary>
    public void WriteDetails(Wallet wallet)
    {
        WriteBanner();
        AnsiConsole.WriteLine();

        var network = Markup.Escape(wallet.Network);
        var title = ConsoleMarkup.Styled(network, theme.Heading);
        AnsiConsole.Write(new Rule(title) { Justification = Justify.Left });

        var address = Markup.Escape(wallet.Address);
        AnsiConsole.MarkupLine($"[bold]{address}[/]");
        AnsiConsole.WriteLine();

        var qr = QrRenderer.Render(wallet.Address);
        AnsiConsole.Write(qr);
        AnsiConsole.WriteLine();

        var warning = Markup.Escape(wallet.Warning);
        AnsiConsole.MarkupLine($"[yellow]⚠ {warning}[/]");
    }

    private static void WriteBanner()
    {
        var banner = new Panel(
            "[bold yellow]Always check the network before sending.[/]\n"
            + "Funds sent on the wrong network are lost and cannot be recovered.")
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(Color.Yellow),
        };
        AnsiConsole.Write(banner);
    }
}
