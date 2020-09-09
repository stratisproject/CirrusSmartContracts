namespace ICOContrat.Tests
{
    using Microsoft.AspNetCore.Authentication;
    using Moq;
    using NBitcoin;
    using Stratis.SmartContracts;
    using Stratis.SmartContracts.CLR;
    using Stratis.SmartContracts.CLR.Serialization;
    using Xunit;
    using SalePeriod = ICOContract.SalePeriod;
    using SalePeriodInput = ICOContract.SalePeriodInput;
    using TokenType = ICOContract.TokenType;

    public class ICOContractTests
    {
        private const ulong Satoshis = 100_000_000;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;
        private readonly Mock<IBlock> mBlock;
        private readonly ICreateResult createSuccess;
        private Address sender;
        private Address owner;
        private Address investor;
        private Address contract;
        private Address tokenContract;
        private Address kycContract;

        private IPersistentState persistentState;
        private string name;
        private string symbol;
        private ulong totalSupply;
        private Mock<Network> network;
        private Serializer serializer;

        public ICOContractTests()
        {
            this.mContractLogger = new Mock<IContractLogger>();
            this.mContractState = new Mock<ISmartContractState>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.persistentState = new InMemoryState();
            this.network = new Mock<Network>();
            this.mBlock = new Mock<IBlock>();
            this.mContractState.Setup(s => s.Block).Returns(this.mBlock.Object);
            this.mContractState.Setup(s => s.PersistentState).Returns(this.persistentState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.serializer = new Serializer(new ContractPrimitiveSerializer(this.network.Object));
            this.mContractState.Setup(s => s.Serializer).Returns(this.serializer);
            this.sender = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.owner = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.investor = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.kycContract = "0x0000000000000000000000000000000000000006".HexToAddress();
            this.createSuccess = CreateResult.Succeeded(this.tokenContract);
            this.name = "Test Token";
            this.symbol = "TST";
            this.totalSupply = 100 * Satoshis;
        }

        [Fact]
        public void Constructor_Sets_Parameters()
        {
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, 0)).Returns(CreateResult.Succeeded(this.tokenContract));

            var (contract, periods) = this.Setup(contractCreation: this.createSuccess, TokenType.StandardToken);

            Assert.Equal(this.totalSupply, contract.TokenBalance);
            Assert.Equal(this.owner, contract.Owner);
            Assert.Equal(this.tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
        }

        public (ICOContract contract, SalePeriod[] periods) Setup(ICreateResult contractCreation, TokenType tokenType = TokenType.StandardToken)
        {
            var periodInputs = new[]
            {
                new SalePeriodInput { PricePerToken = 3 * Satoshis, DurationBlocks = 1 },
                new SalePeriodInput { PricePerToken = 5 * Satoshis, DurationBlocks = 2 }
            };
            var periods = new[]
            {
                new SalePeriod { PricePerToken = 3 * Satoshis, EndBlock = 2 },
                new SalePeriod { PricePerToken = 5 * Satoshis, EndBlock = 4 }
            };

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mBlock.Setup(s => s.Number).Returns(1);
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(contractCreation);
            var contract = new ICOContract(this.mContractState.Object, this.owner, (uint)tokenType, new object[] { this.totalSupply, this.name, this.symbol }, this.kycContract, this.serializer.Serialize(periodInputs));
            return (contract, periods);
        }

        [Fact]
        public void Invest_Success()
        {
            var amount = 15 * Satoshis;

            var (contract, _) = this.Setup(contractCreation: this.createSuccess);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 5ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(this.totalSupply - 5ul, contract.TokenBalance);
            this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

            this.mBlock.Setup(s => s.Number).Returns(4);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 3ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(this.totalSupply - 8ul, contract.TokenBalance);
            this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Invest_Refunds_Oversold_Tokens()
        {
            this.totalSupply = 60;
            var amount = 190 * Satoshis;
            var (contract, _) = this.Setup(contractCreation: this.createSuccess);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, this.totalSupply }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(0ul, contract.TokenBalance); // All tokens are sold
            this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.investor, 10 * Satoshis), Times.Once);
        }

        [Fact]
        public void Invest_Fails_If_TokenBalance_Is_Zero()
        {
            var amount = 1 * Satoshis;

            var (contract, _) = this.Setup(contractCreation: this.createSuccess);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.persistentState.SetUInt64(nameof(ICOContract.TokenBalance), 0);

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_EndBlock_Reached()
        {
            var amount = 1 * Satoshis;

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
}
