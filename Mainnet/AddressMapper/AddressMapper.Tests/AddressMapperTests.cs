using AddressMapperTests;
using Moq;
using Stratis.SmartContracts;
using Xunit;
using static AddressMapper;

namespace AddressMapperTests
{
    public class AddressMapperTests
    {
        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mInternalExecutor;

        private readonly IPersistentState state;

        private readonly Address contractAddress;
        private readonly Address primaryAddress;
        private readonly Address secondaryAddress;
        private readonly Address ownerAddress;

        public AddressMapperTests()
        {
            mContractLogger = new Mock<IContractLogger>();
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mInternalExecutor = new Mock<IInternalTransactionExecutor>();
            mContractState.Setup(x => x.PersistentState).Returns(state);
            mContractState.Setup(x => x.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(x => x.InternalTransactionExecutor).Returns(mInternalExecutor.Object);
            contractAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            ownerAddress = "0x0000000000000000000000000000000000000002".HexToAddress();
            primaryAddress = "0x0000000000000000000000000000000000000003".HexToAddress();
            secondaryAddress = "0x0000000000000000000000000000000000000004".HexToAddress();
        }

        [Theory]
        [InlineData(Status.Approved)]
        [InlineData(Status.Pending)]
        public void MappAddress_Fails_If_SecondaryAddress_Is_Mapped_Already(Status status)
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)status });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(() => contract.MapAddress(secondaryAddress));
        }

        [Fact]
        public void MappAddress_Maps_PrimaryAddres()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            contract.MapAddress(secondaryAddress);

            Assert.Equal(new MappingInfo { Primary = primaryAddress, Status = (int)Status.Pending }, state.GetStruct<MappingInfo>($"MappingInfo:{secondaryAddress}"));
        }

        [Fact]
        public void Approve_Fails_If_Called_By_None_Admin()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(() => contract.Approve(secondaryAddress));
        }

        [Theory]
        [InlineData(Status.Approved)]
        [InlineData(Status.NoStatus)]
        public void Approve_Fails_If_State_Is_Not_InPending_State(Status status)
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)status });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(() => contract.Approve(secondaryAddress));
        }

        [Fact]
        public void Approve_Approves_Mapping()
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)Status.Pending });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            contract.Approve(secondaryAddress);

            Assert.Equal(secondaryAddress, state.GetAddress($"SecondaryAddress:{primaryAddress}"));

            Assert.Equal(new MappingInfo { Primary = primaryAddress, Status = (int)Status.Approved }, state.GetStruct<MappingInfo>($"MappingInfo:{secondaryAddress}"));

            mContractLogger.Verify(m => m.Log(mContractState.Object, new AddressMappedLog { Primary = primaryAddress, Secondary = secondaryAddress }));
        }

        [Fact]
        public void Reject_Fails_If_Called_By_None_Admin()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(() => contract.Reject(secondaryAddress));
        }

        [Theory]
        [InlineData(Status.Approved)]
        [InlineData(Status.NoStatus)]
        public void Reject_Fails_If_State_Is_Not_InPending_State(Status status)
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)status });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(() => contract.Reject(secondaryAddress));
        }

        [Fact]
        public void Reject_Rejects_Mapping()
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)Status.Pending });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            contract.Reject(secondaryAddress);

            Assert.Equal(default(MappingInfo), state.GetStruct<MappingInfo>($"MappingInfo:{secondaryAddress}"));
        }

        [Fact]
        public void GetPrimaryAddress_Get_None_Approved_PrimaryAddres_Fails()
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)Status.Pending });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            Assert.Throws<SmartContractAssertException>(()=> contract.GetPrimaryAddress(secondaryAddress));
        }

        [Fact]
        public void GetPrimaryAddress_Get_Approved_PrimaryAddres_Success()
        {
            state.SetStruct($"MappingInfo:{secondaryAddress}", new MappingInfo { Primary = primaryAddress, Status = (int)Status.Approved });

            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, ownerAddress, 0));

            var contract = new AddressMapper(mContractState.Object, ownerAddress);

            var result = contract.GetPrimaryAddress(secondaryAddress);

            Assert.Equal(primaryAddress, result);
        }
    }
}