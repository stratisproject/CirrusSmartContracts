using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;

namespace OpdexProposalVoteTests
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
            Serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));
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

        protected PrivateYesNoVote CreateNewVoteContract(ulong currentBlock = 100000, ulong duration = 100)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(Contract, Owner, 0));
            MockContractState.Setup(x => x.Block.Number).Returns(currentBlock);
            
            var addresses = new[] {AddressOne, AddressTwo, AddressThree};
            var bytes = Serializer.Serialize(addresses);

            return new PrivateYesNoVote(MockContractState.Object, duration, bytes);
        }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
        }

        protected void SetupBlock(ulong blockNumber)
        {
            MockContractState.Setup(x => x.Block.Number).Returns(blockNumber);
        }

        protected void SetupBalance(ulong balance)
        {
            MockContractState.Setup(x => x.GetBalance).Returns(() => balance);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result)
        {
            MockInternalExecutor
                .Setup(x => x.Call(MockContractState.Object, to, amountToTransfer, methodName, parameters, It.IsAny<ulong>()))
                .Returns(result); 
        }

        protected void SetupTransfer(Address to, ulong value, TransferResult result)
        {
            MockInternalExecutor
                .Setup(x => x.Transfer(MockContractState.Object, to, value))
                .Returns(result);
        }

        protected void SetupCreate<T>(CreateResult result, ulong amount = 0ul, object[] parameters = null)
        {
            MockInternalExecutor
                .Setup(x => x.Create<T>(MockContractState.Object, amount, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Func<Times> times)
        {
            MockInternalExecutor.Verify(x => x.Call(MockContractState.Object, addressTo, amountToTransfer, methodName, parameters, 0ul), times);
        }

        protected void VerifyTransfer(Address to, ulong value, Func<Times> times)
        {
            MockInternalExecutor.Verify(x => x.Transfer(MockContractState.Object, to, value), times);
        }

        protected void VerifyLog<T>(T expectedLog, Func<Times> times) where T : struct
        {
            MockContractLogger.Verify(x => x.Log(MockContractState.Object, expectedLog), times);
        }
        
        protected static byte[] ObjectToByteArray(object obj)
        {
            if(obj == null) return null;
            BinaryFormatter bf = new BinaryFormatter();
            using MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }
    }
}