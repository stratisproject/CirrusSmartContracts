using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static AddressMapper;

namespace Tests
{
    public class AddressMapperTests
    {
        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IPersistentState> mPersistentState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mInternalExecutor;

        private readonly Address contractAddress;
        private readonly Address primaryAddress;
        private readonly Address secondaryAddress;

        public AddressMapperTests()
        {
            mContractLogger = new Mock<IContractLogger>();
            mPersistentState = new Mock<IPersistentState>();
            mContractState = new Mock<ISmartContractState>();
            mInternalExecutor = new Mock<IInternalTransactionExecutor>();
            mContractState.Setup(x => x.PersistentState).Returns(mPersistentState.Object);
            mContractState.Setup(x => x.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(x => x.InternalTransactionExecutor).Returns(mInternalExecutor.Object);
            contractAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            primaryAddress = "0x0000000000000000000000000000000000000002".HexToAddress();
            secondaryAddress = "0x0000000000000000000000000000000000000003".HexToAddress();
        }

        [Fact]
        public void MappAddress_Fails_If_SecondaryAddressInUse_Is_True()
        {
            mPersistentState.Setup(x => x.GetBool($"SecondaryAddressInUse:{secondaryAddress}")).Returns(true);

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object);

            // Assert that an exception is thrown if the total supply is set to 0
            Assert.False(contract.MapAddress(secondaryAddress));
        }

        [Fact]
        public void MappAddress_Maps_PrimaryAddres()
        {
            mPersistentState.Setup(x => x.GetBool($"SecondaryAddressInUse:{secondaryAddress}")).Returns(false);

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object);

            // Assert that an exception is thrown if the total supply is set to 0
            Assert.True(contract.MapAddress(secondaryAddress));

            mContractLogger.Verify(m => m.Log(mContractState.Object, new AddressMappedLog { Primary = primaryAddress, Secondary = secondaryAddress }));

            mPersistentState.Verify(x => x.SetBool($"SecondaryAddressInUse:{secondaryAddress}", true));
            mPersistentState.Verify(m => m.SetAddress($"SecondaryAddress:{primaryAddress}", secondaryAddress));

        }
    }
}