using ConsoleScout.Donate;
using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>--donate lists the networks from <see cref="DonationWallets"/>; --donate &lt;network&gt; shows
/// one of them, chosen by its ticker (btc, eth, usdt, ton, sol).</summary>
internal sealed class DonateCommand(IConsoleUi ui)
{
    /// <returns>The exit code: 1 when the network is unknown.</returns>
    public int Run(string? network)
    {
        if (network is null)
        {
            ui.WriteWalletList(DonationWallets.Configured);
            return 0;
        }

        var wallet = DonationWallets.Find(network);
        if (wallet is null)
        {
            var codes = DonationWallets.Configured.Select(configured => configured.Code);
            ui.WriteError($"Unknown network '{network}'. Available: {string.Join(", ", codes)} (run csc --donate for the list).");
            return 1;
        }

        ui.WriteWallet(wallet);
        return 0;
    }
}
