using Stratis.SmartContracts;

namespace STOContractTests
{
    public class TransferResult : ITransferResult
    {
        public object ReturnValue { get; private set; }

        public bool Success { get; private set; }

        public static ITransferResult Failed() => new TransferResult { Success = false };


        public static ITransferResult Transferred(object returnValue) => new TransferResult { Success = true, ReturnValue = returnValue };
    }
}
