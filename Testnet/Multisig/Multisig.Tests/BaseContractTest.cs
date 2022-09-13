using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

namespace Multisig.Tests
{
    public class BaseContractTest
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly InMemoryState PersistentState;
        protected readonly ISerializer Serializer;
        protected readonly Address Contract;
        protected readonly Address Owner;
        protected readonly Address AddressOne;
        protected readonly Address AddressTwo;
        protected readonly Address AddressThree;
        protected readonly Address AddressFour;
        protected readonly Address AddressFive;
        protected readonly Address AddressSix;

        protected BaseContractTest()
        {
            Serializer = new Serializer(new ContractPrimitiveSerializerV2(null)); // new SmartContractsPoARegTest()
            PersistentState = new InMemoryState();
            MockContractLogger = new Mock<IContractLogger>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(PersistentState);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            MockContractState.Setup(x => x.Serializer).Returns(Serializer);
            Contract = "0x0000000000000000000000000000000000000001".HexToAddress();
            Owner = "0x0000000000000000000000000000000000000002".HexToAddress();
            AddressOne = "0x0000000000000000000000000000000000000003".HexToAddress();
            AddressTwo = "0x0000000000000000000000000000000000000004".HexToAddress();
            AddressThree = "0x0000000000000000000000000000000000000005".HexToAddress();
            AddressFour = "0x0000000000000000000000000000000000000006".HexToAddress();
            AddressFive = "0x0000000000000000000000000000000000000007".HexToAddress();
            AddressSix = "0x0000000000000000000000000000000000000008".HexToAddress();
        }

        protected MultisigContract CreateNewMultisigContract()
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(Contract, Owner, 0));
            MockContractState.Setup(x => x.InternalHashHelper).Returns(new InternalHashHelper());

            var addresses = new[] {AddressOne, AddressTwo, AddressThree};
            var bytes = Serializer.Serialize(addresses);
            uint required = 2;

            return new MultisigContract(MockContractState.Object, bytes, required);
        }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
        }

        protected void SetupBlock(ulong blockNumber)
        {
            MockContractState.Setup(x => x.Block.Number).Returns(blockNumber);
        }

        protected void VerifyLog<T>(T expectedLog, Func<Times> times) where T : struct
        {
            MockContractLogger.Verify(x => x.Log(MockContractState.Object, expectedLog), times);
        }
    }
}
