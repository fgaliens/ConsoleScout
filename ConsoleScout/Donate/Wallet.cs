namespace ConsoleScout.Donate;

/// <param name="Code">The ticker --donate &lt;network&gt; takes, e.g. "btc"; unique among the wallets.</param>
/// <param name="Network">What to send and on which chain, as shown to the user.</param>
/// <param name="Warning">What must not be sent to this address; funds sent on the wrong network are lost.</param>
internal sealed record Wallet(string Code, string Network, string Address, string Warning);
