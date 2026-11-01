namespace ConsoleScout.Donate;

/// <summary>The addresses --donate shows. A template: fill in the constants below; a wallet whose address
/// is empty is simply not shown. Double-check every address - a typo sends donations nowhere.</summary>
internal static class DonationWallets
{
    private const string BitcoinAddress = "bc1q585qv40gtc5pju26eh0c6sy3shaey4gp9akenc";

    private const string EthereumAddress = "0x527a8c5F4ED5795aDA12e734a22d13BeD72455E7";

    private const string TronUsdtAddress = "TKdWdtTDxnYv6HQpeJ4oNfFfSsvdvJB6LL";

    private const string TonAddress = "UQBxaGM92Sco-lKjTRDn3Y_uR1cWYlZkqhiB5fyp_ZyPkWEK";

    private const string SolanaAddress = "4YYyTAzp6LAUC4rKMABLbJS6BHuP9hD8DA2TihQnQThU";

    public static IReadOnlyList<Wallet> All { get; } =
    [
        new("btc", "Bitcoin (BTC)", BitcoinAddress,
            "Send only BTC on the Bitcoin network."),
        new("eth", "Ethereum (ETH, ERC-20)", EthereumAddress,
            "Only ETH or ERC-20 tokens (USDT, USDC) on Ethereum mainnet. Other EVM networks (BNB Chain, Polygon, Arbitrum) are not monitored."),
        new("usdt", "USDT (TRC-20, Tron)", TronUsdtAddress,
            "Only USDT on the Tron network (TRC-20). Do not send ERC-20 or BEP-20 USDT here."),
        new("ton", "TON", TonAddress,
            "Only TON on The Open Network."),
        new("sol", "Solana (SOL, SPL)", SolanaAddress,
            "Only SOL or SPL tokens (e.g. USDC) on Solana."),
    ];

    /// <summary>The wallets that have an address filled in.</summary>
    public static IReadOnlyList<Wallet> Configured { get; } =
        All.Where(wallet => !string.IsNullOrWhiteSpace(wallet.Address)).ToList();

    /// <summary>The configured wallet whose <see cref="Wallet.Code"/> (its ticker) is <paramref name="network"/>,
    /// case-insensitively: "btc" and "BTC" find Bitcoin.</summary>
    public static Wallet? Find(string network)
    {
        var code = network.Trim();
        return Configured.FirstOrDefault(wallet => string.Equals(wallet.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
