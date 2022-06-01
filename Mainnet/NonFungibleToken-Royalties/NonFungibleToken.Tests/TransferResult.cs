using Stratis.SmartContracts;

namespace NonFungibleTokenContract.Tests
{
    public class TransferResult : ITransferResult
    {
        public object ReturnValue { get; private set; }

        public bool Success { get; private set; }

        public static TransferResult Failed() => new TransferResult { Success = false };


        public static TransferResult Transferred(object returnValue) => new TransferResult { Success = true, ReturnValue = returnValue };
    }
}
