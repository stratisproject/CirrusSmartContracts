using NBitcoin;

namespace ICOContract.Regression
{
    public class SendCallContractResult
    {
        public ulong Fee { get; set; }
        public string Hex { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
        public uint256 TransactionId { get; set; }
    }
}
