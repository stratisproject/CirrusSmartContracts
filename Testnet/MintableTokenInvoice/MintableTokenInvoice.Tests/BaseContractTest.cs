namespace MintableTokenInvoiceTests;

using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

public class BaseContractTest
{
    protected Mock<ISmartContractState> MockContractState { get; private set; }

    protected Mock<IContractLogger> MockContractLogger { get; private set; }

    protected Mock<IInternalTransactionExecutor> MockInternalExecutor { get; private set; }

    protected InMemoryState PersistentState { get; private set; }

    protected ISerializer Serializer { get; private set; }

    protected Address Contract { get; private set; }

    protected Address Owner { get; private set; }

    protected Address AddressOne { get; private set; }

    protected Address AddressTwo { get; private set; }

    protected Address AddressThree { get; private set; }

    protected Address AddressFour { get; private set; }

    protected Address AddressFive { get; private set; }

    protected Address AddressSix { get; private set; }

    protected Address IdentityContract { get; private set; }

    protected BaseContractTest()
    {
        this.Serializer = new Serializer(new ContractPrimitiveSerializerV2(null)); // new SmartContractsPoARegTest()
        this.PersistentState = new InMemoryState();
        this.MockContractLogger = new Mock<IContractLogger>();
        this.MockContractState = new Mock<ISmartContractState>();
        this.MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
        this.MockContractState.Setup(x => x.PersistentState).Returns(this.PersistentState);
        this.MockContractState.Setup(x => x.ContractLogger).Returns(this.MockContractLogger.Object);
        this.MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(this.MockInternalExecutor.Object);
        this.MockContractState.Setup(x => x.Serializer).Returns(this.Serializer);
        this.Contract = "0x0000000000000000000000000000000000000001".HexToAddress();
        this.Owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.AddressOne = "0x0000000000000000000000000000000000000003".HexToAddress();
        this.AddressTwo = "0x0000000000000000000000000000000000000004".HexToAddress();
        this.AddressThree = "0x0000000000000000000000000000000000000005".HexToAddress();
        this.AddressFour = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.AddressFive = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.AddressSix = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.IdentityContract = "0x000000000000000000000000000000000000000F".HexToAddress();
    }

    protected MintableTokenInvoice CreateNewMintableTokenContract()
    {
        this.MockContractState.Setup(x => x.Message).Returns(new Message(this.Contract, this.Owner, 0));
        this.MockContractState.Setup(x => x.InternalHashHelper).Returns(new InternalHashHelper());

        var addresses = new[] { this.AddressOne, this.AddressTwo, this.AddressThree };
        var bytes = this.Serializer.Serialize(addresses);

        return new MintableTokenInvoice(this.MockContractState.Object, 1000, this.IdentityContract);
    }

    protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
    {
        this.MockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
    }

    protected void SetupBlock(ulong blockNumber)
    {
        this.MockContractState.Setup(x => x.Block.Number).Returns(blockNumber);
    }

    protected void VerifyLog<T>(T expectedLog, Func<Times> times) 
        where T : struct
    {
        this.MockContractLogger.Verify(x => x.Log(this.MockContractState.Object, expectedLog), times);
    }
}
