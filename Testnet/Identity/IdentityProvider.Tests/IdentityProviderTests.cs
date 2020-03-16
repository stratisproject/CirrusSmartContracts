using Moq;
using Stratis.SmartContracts;
using Xunit;

namespace IdentityProvider.Tests
{
    public class IdentityProviderTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Buyer;
        private readonly Address SellerOne;
        private readonly Address SellerTwo;
        private readonly Address Token;
        private readonly Address ContractAddress;
        private readonly ulong Amount;
        private readonly ulong Price;
        private readonly bool IsActive;
        private const ulong DefaultAmount = 10;
        private const ulong DefaultPrice = 10_000_000;
        private const ulong DefaultValue = 100_000_000;

        public IdentityProviderTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Buyer = "0x0000000000000000000000000000000000000001".HexToAddress();
            SellerOne = "0x0000000000000000000000000000000000000002".HexToAddress();
            SellerTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            Token = "0x0000000000000000000000000000000000000004".HexToAddress();
            ContractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        [Fact]
        public void OnlyOwnerCanCallPublicMethods()
        {

        }
    }
}
