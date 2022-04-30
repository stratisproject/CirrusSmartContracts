using Moq;
using Stratis.SmartContracts;
using Xunit;

namespace KeyValueTests
{
    public class KeyValueTests
    {
        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mInternalExecutor;

        private readonly IPersistentState state;

        private readonly Address contractAddress;
        private readonly Address primaryAddress;
        private readonly Address secondaryAddress;

        public KeyValueTests()
        {
            mContractLogger = new Mock<IContractLogger>();
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mInternalExecutor = new Mock<IInternalTransactionExecutor>();
            mContractState.Setup(x => x.PersistentState).Returns(state);
            mContractState.Setup(x => x.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(x => x.InternalTransactionExecutor).Returns(mInternalExecutor.Object);
            contractAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
            primaryAddress = "0x0000000000000000000000000000000000000002".HexToAddress();
            secondaryAddress = "0x0000000000000000000000000000000000000003".HexToAddress();
        }

        [Fact]
        public void Init_Contract_Succeeds_And_Sets_MaxLength()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            Assert.True(contract.MaxLength == 32);
        }

        [Fact]
        public void Init_Contract_With_Zero_MaxLength_Fails()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => new KeyValueContract(mContractState.Object, 0));
        }

        [Fact]
        public void Set_Succeeds()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            contract.Set("TestKey", "TestValue");

            string setString = state.GetString($"{primaryAddress}:TestKey");

            Assert.Equal("TestValue", setString);
        }

        [Fact]
        public void Set_ForLongKey_Fails()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 1);

            Assert.ThrowsAny<SmartContractAssertException>(() => contract.Set("TestKey", "1"));
        }

        [Fact]
        public void Set_ForLongValue_Fails()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 1);

            Assert.ThrowsAny<SmartContractAssertException>(() => contract.Set("1", "TestValue"));
        }

        [Fact]
        public void Get_Succeeds()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            state.SetString($"{primaryAddress}:TestKey", "TestValue");

            string getString = contract.Get(primaryAddress, "TestKey");

            Assert.Equal("TestValue", getString);
        }

        [Fact]
        public void Get_For_Different_Address_Succeeds()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            // Set the value as primary address.
            contract.Set("TestKey", "TestValue");

            // Now retrieve it as the secondary address, specifying the primary address.
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, secondaryAddress, 0));

            string setString = state.GetString($"{primaryAddress}:TestKey");

            Assert.Equal("TestValue", setString);
        }

        [Fact]
        public void Get_ForNonExistentAddress_ReturnsNull()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            contract.Set("TestKey", "TestValue");

            // Execute the Get for a different address to what was set. 
            string getString = contract.Get(secondaryAddress, "TestKey");

            Assert.Null(getString);
        }

        [Fact]
        public void Get_ForNonExistentKey_ReturnsNull()
        {
            mContractState.Setup(x => x.Message).Returns(new Message(contractAddress, primaryAddress, 0));

            var contract = new KeyValueContract(mContractState.Object, 32);

            string getString = contract.Get(primaryAddress, "DummyKey");

            Assert.Null(getString);
        }
    }
}
