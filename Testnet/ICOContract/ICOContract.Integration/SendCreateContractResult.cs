using NBitcoin;

namespace ICOContract.Integration
{
    public class SendCreateContractResult
    {
        public ulong Fee { get; set; }

        public string Hex { get; set; }

        public string Message { get; set; }

        public bool Success { get; set; }

        public uint256 TransactionId { get; set; }

        public Base58Address NewContractAddress { get; set; }
    }
}
