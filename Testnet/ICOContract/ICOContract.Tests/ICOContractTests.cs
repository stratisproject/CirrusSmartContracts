using System.Linq;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit;
using SalePeriod = ICOContract.SalePeriod;
using SalePeriodInput = ICOContract.SalePeriodInput;
public class ICOContractTests
{
    private readonly Mock<ISmartContractState> mContractState;
    private readonly Mock<IPersistentState> mPersistentState;
    private readonly Mock<IContractLogger> mContractLogger;
    private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;
    private readonly Mock<IBlock> mBlock;
    private Address owner;
    private Address investor;
    private Address contract;
    private Address tokenContract;
    private string name;
    private string symbol;
    private Mock<Network> network;
    private Serializer serializer;

    public ICOContractTests()
    {
        this.mContractLogger = new Mock<IContractLogger>();
        this.mPersistentState = new Mock<IPersistentState>();
        this.mContractState = new Mock<ISmartContractState>();
        this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
        this.network = new Mock<Network>();
        this.mBlock = new Mock<IBlock>();
        this.mContractState.Setup(s => s.Block).Returns(this.mBlock.Object);
        this.mContractState.Setup(s => s.PersistentState).Returns(this.mPersistentState.Object);
        this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
        this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
        this.serializer = new Serializer(new ContractPrimitiveSerializer(this.network.Object));
        this.mContractState.Setup(s => s.Serializer).Returns(this.serializer);
        this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
        this.investor = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
        this.tokenContract = "0x0000000000000000000000000000000000000004".HexToAddress();
        this.name = "Test Token";
        this.symbol = "TST";
    }

    [Fact]
    public void Constructor_Sets_Parameters()
    {
        ulong totalSupply = 100_000;
        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
        this.mBlock.Setup(s => s.Number).Returns(1);

        this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(CreateResult.Succeeded(this.tokenContract));
        var periodInputs = new[]
        {
            new SalePeriodInput { Multiplier = 3, DurationBlocks = 1 },
            new SalePeriodInput { Multiplier = 5, DurationBlocks = 2 }
        };

        var periods = new[]
        {
            new SalePeriod { Multiplier = 3, EndBlock = 2 },
            new SalePeriod { Multiplier = 5, EndBlock = 4 }
        };

        var contract = new ICOContract(this.mContractState.Object, totalSupply, this.name, this.symbol, this.serializer.Serialize(periodInputs));

        // Verify that PersistentState was called with the total supply
        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), totalSupply));
        this.mPersistentState.Verify(s => s.SetAddress(nameof(ICOContract.Owner), this.owner));
        this.mPersistentState.Verify(s => s.SetAddress(nameof(ICOContract.TokenAddress), this.tokenContract));
        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.EndBlock), 4));
        this.mPersistentState.Verify(s => s.SetArray(nameof(ICOContract.SalePeriods), periods));
    }

    [Fact]
    public void Verify_Investment()
    {
        ulong totalSupply = 100;
        var amount = (ulong)Money.Coins(3).Satoshi;
        var periodInputs = new[]
        {
            new SalePeriodInput { Multiplier = 2, DurationBlocks = 1 },
            new SalePeriodInput { Multiplier = 3, DurationBlocks = 2 }
        };

        var periods = new[]
        {
            new SalePeriod { Multiplier = 2, EndBlock = 2 },
            new SalePeriod { Multiplier = 3, EndBlock = 4 }
        };

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mBlock.Setup(s => s.Number).Returns(1);
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.TokenBalance))).Returns(totalSupply);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.Owner))).Returns(this.owner);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.TokenAddress))).Returns(this.tokenContract);
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.EndBlock))).Returns(4);
        this.mPersistentState.Setup(s => s.GetArray<SalePeriod>(nameof(ICOContract.SalePeriods))).Returns(periods);
        this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(CreateResult.Succeeded(this.tokenContract));
        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 6ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
        var contract = new ICOContract(this.mContractState.Object, totalSupply, this.name, this.symbol, this.serializer.Serialize(periodInputs));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), totalSupply - 6));
        this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

        this.mBlock.Setup(s => s.Number).Returns(4);
        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 9ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), totalSupply - 9));
        this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void Verify_OverSold_Investment()
    {
        ulong totalSupply = 100;
        var amount = (ulong)Money.Coins(120).Satoshi;
        var periodInputs = new[]
        {
            new SalePeriodInput { Multiplier = 1, DurationBlocks = 1 }
        };

        var periods = new[]
        {
            new SalePeriod { Multiplier = 1, EndBlock = 2 },
        };

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mBlock.Setup(s => s.Number).Returns(1);
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.TokenBalance))).Returns(totalSupply);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.Owner))).Returns(this.owner);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.TokenAddress))).Returns(this.tokenContract);
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.EndBlock))).Returns(2);
        this.mPersistentState.Setup(s => s.GetArray<SalePeriod>(nameof(ICOContract.SalePeriods))).Returns(periods);
        this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(CreateResult.Succeeded(this.tokenContract));
        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 100ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
        var contract = new ICOContract(this.mContractState.Object, totalSupply, this.name, this.symbol, this.serializer.Serialize(periodInputs));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), 0)); // All tokens are sold
        this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.investor, (ulong)Money.Coins(20).Satoshi), Times.Once);
    }
}