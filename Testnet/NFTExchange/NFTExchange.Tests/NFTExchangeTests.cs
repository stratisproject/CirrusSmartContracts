using Moq;
using NFTExchangeContract.Tests;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace NFTExchangeTestsContract.Tests
{
    public class NFTExchangeTests
    {
        private readonly IPersistentState persistentState;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address owner;
        private readonly Address contract;

        private readonly string name;
        private readonly string symbol;
        private readonly uint decimals;

        public NFTExchangeTests()
        {
            this.persistentState = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(this.persistentState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.name = "Test Token";
            this.symbol = "TST";
            this.decimals = 0;
        }

    }
}
