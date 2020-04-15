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
    private readonly ICreateResult createSuccess;
    private Address owner;
    private Address investor;
    private Address contract;
    private Address tokenContract;
    private string name;
    private string symbol;
    private ulong totalSupply;
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
        this.createSuccess = CreateResult.Succeeded(this.tokenContract);
        this.name = "Test Token";
        this.symbol = "TST";
        this.totalSupply = 100;
    }

    [Fact]
    public void Constructor_Sets_Parameters()
    {
        var (contract, periods) = this.Setup(contractCreation: this.createSuccess);

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), this.totalSupply));
        this.mPersistentState.Verify(s => s.SetAddress(nameof(ICOContract.Owner), this.owner));
        this.mPersistentState.Verify(s => s.SetAddress(nameof(ICOContract.TokenAddress), this.tokenContract));
        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.EndBlock), 4));
        this.mPersistentState.Verify(s => s.SetArray(nameof(ICOContract.SalePeriods), periods));
    }

    private (ICOContract contract, SalePeriod[] periods) Setup(ICreateResult contractCreation)
    {
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

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
        this.mBlock.Setup(s => s.Number).Returns(1);
        this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(CreateResult.Succeeded(this.tokenContract));
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.TokenBalance))).Returns(this.totalSupply);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.Owner))).Returns(this.owner);
        this.mPersistentState.Setup(s => s.GetAddress(nameof(ICOContract.TokenAddress))).Returns(this.tokenContract);
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.EndBlock))).Returns(periods[1].EndBlock);
        this.mPersistentState.Setup(s => s.GetArray<SalePeriod>(nameof(ICOContract.SalePeriods))).Returns(periods);
        var contract = new ICOContract(this.mContractState.Object, this.totalSupply, this.name, this.symbol, this.serializer.Serialize(periodInputs));
        return (contract, periods);
    }

    [Fact]
    public void Verify_Investment()
    {
        var amount = (ulong)Money.Coins(3).Satoshi;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 9ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), this.totalSupply - 9));
        this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

        this.mBlock.Setup(s => s.Number).Returns(4);
        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 15ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), this.totalSupply - 15));
        this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void Invest_Refunds_Oversold_Tokens()
    {
        this.totalSupply = 60;
        var amount = (ulong)Money.Coins(30).Satoshi;
        var (contract, _) = this.Setup(contractCreation: this.createSuccess);

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, this.totalSupply }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

        Assert.True(contract.Invest());

        this.mPersistentState.Verify(s => s.SetUInt64(nameof(ICOContract.TokenBalance), 0)); // All tokens are sold
        this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.investor, (ulong)Money.Coins(10).Satoshi), Times.Once);
    }

    [Fact]
    public void Invest_Fails_If_TokenBalance_Is_Zero()
    {
        var amount = 1ul;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);
        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mPersistentState.Setup(s => s.GetUInt64(nameof(ICOContract.TokenBalance))).Returns(0);

        Assert.Throws<SmartContractAssertException>(() => contract.Invest());
    }

    [Fact]
    public void Invest_Fails_If_EndBlock_Reached()
    {
        var amount = 1ul;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);

        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mBlock.Setup(s => s.Number).Returns(5);

        Assert.Throws<SmartContractAssertException>(() => contract.Invest());
    }

    [Fact]
    public void Invest_Fails_If_Investment_Amount_Is_Zero()
    {
        var amount = 0ul;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);
        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

        Assert.Throws<SmartContractAssertException>(() => contract.Invest());
    }

    [Fact]
    public void WithdrawFunds_Fails_If_Caller_Is_Not_Owner()
    {
        var amount = 0ul;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);
        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
        this.mBlock.Setup(m => m.Number).Returns(5);

        Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
    }

    [Fact]
    public void WithdrawFunds_Fails_If_Sale_Is_Open()
    {
        var amount = 0ul;

        var (contract, _) = this.Setup(contractCreation: this.createSuccess);
        this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, amount));
        this.mBlock.Setup(m => m.Number).Returns(4);

        Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
    }
}