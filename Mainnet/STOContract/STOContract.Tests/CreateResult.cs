using Stratis.SmartContracts;

namespace STOContractTests
{
    public class CreateResult : ICreateResult
    {
        public Address NewContractAddress { get; private set; }

        public bool Success { get; private set; }

        public static ICreateResult Failed() => new CreateResult { Success = false };


        public static ICreateResult Succeed(Address address) => new CreateResult { Success = true, NewContractAddress = address };
    }
}
