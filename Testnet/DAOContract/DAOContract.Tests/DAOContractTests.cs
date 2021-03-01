using DAOContract;
using DividendTokenContract.Tests;
using Moq;
using Stratis.SmartContracts;
using Xunit;

namespace DAOContract.Tests
{
    public class DividendTokenTests
    {
        private readonly IPersistentState state;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address owner;


        public DividendTokenTests()
        {
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mContractLogger = new Mock<IContractLogger>();
            mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            mContractState.Setup(s => s.PersistentState).Returns(state);
            mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            owner = "0x0000000000000000000000000000000000000001".HexToAddress();
        }
    }
}
