namespace ICOContrat.Tests
{
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
        private Address identity;
        private Address contract;
        private Address tokenContract;
        private Address kycContract;
        private Address mapperContract;

        private InMemoryState persistentState;
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
            this.identity = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000006".HexToAddress();
            this.kycContract = "0x0000000000000000000000000000000000000007".HexToAddress();
            this.mapperContract = "0x0000000000000000000000000000000000000008".HexToAddress();
            this.createSuccess = CreateResult.Succeeded(this.tokenContract);
            this.name = "Test Token";
            this.symbol = "TST";
            this.totalSupply = 100 * Satoshis;
            this.persistentState.IsContractResult = true;
        }

        [Fact]
        public void Constructor_IsContract_ReturnsFalse_ThrowsAssertException()
        {
            this.persistentState.IsContractResult = false;

            Assert.Throws<SmartContractAssertException>(() => this.Create(TokenType.StandardToken));
        }

        [Fact]
        public void Constructor_TokenType_HigherThan2_ThrowsAssertException()
        {
            var tokenType = (TokenType)3;

            Assert.Throws<SmartContractAssertException>(() => this.Create(tokenType));
        }

        [Fact]
        public void Constructor_CreateReturnsFailedResult_ThrowsAssertException()
        {
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(CreateResult.Failed());
            Assert.Throws<SmartContractAssertException>(() => this.Create(TokenType.StandardToken));

            this.mTransactionExecutor.Verify(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsStandardToken_Success()
        {
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            var (contract, periods) = this.Create(TokenType.StandardToken);

            Assert.Equal(this.totalSupply, contract.TokenBalance);
            Assert.Equal(this.owner, contract.Owner);
            Assert.Equal(this.tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            this.mTransactionExecutor.Verify(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, 0), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsDividendToken_Success()
        {
            this.mTransactionExecutor.Setup(m => m.Create<DividendToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            var (contract, periods) = this.Create(TokenType.DividendToken);

            Assert.Equal(this.totalSupply, contract.TokenBalance);
            Assert.Equal(this.owner, contract.Owner);
            Assert.Equal(this.tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            this.mTransactionExecutor.Verify(m => m.Create<DividendToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, 0), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsNonFungibleToken_Success()
        {
            this.mTransactionExecutor.Setup(m => m.Create<NonFungibleToken>(this.mContractState.Object, 0, new object[] { this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            var (contract, periods) = this.Create(TokenType.NonFungibleToken);

            Assert.Equal(ulong.MaxValue, contract.TokenBalance);
            Assert.Equal(this.owner, contract.Owner);
            Assert.Equal(this.tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            this.mTransactionExecutor.Verify(m => m.Create<NonFungibleToken>(this.mContractState.Object, 0, new object[] { this.name, this.symbol }, 0), Times.Once);
        }

        public (ICOContract contract, SalePeriod[] periods) Create(TokenType tokenType)
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
            var contract = new ICOContract(this.mContractState.Object, this.owner, (uint)tokenType, this.totalSupply, this.name, this.symbol, this.kycContract,this.mapperContract, this.serializer.Serialize(periodInputs));
            return (contract, periods);
        }

        [Fact]
        public void Invest_CalledForStandardToken_Success()
        {
            var amount = 15 * Satoshis;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, 5ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(this.identity));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

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
        public void Invest_CalledForNonFungibleToken_Success()
        {
            var amount = 15 * Satoshis;
            var totalSupply = ulong.MaxValue;

            this.mTransactionExecutor.Setup(m => m.Create<NonFungibleToken>(this.mContractState.Object, 0, new object[] { this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.NonFungibleToken);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(NonFungibleToken.MintAll), new object[] { this.investor, 5ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(this.identity));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 5ul, contract.TokenBalance);
            this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

            this.mBlock.Setup(s => s.Number).Returns(4);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(NonFungibleToken.MintAll), new object[] { this.investor, 3ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 8ul, contract.TokenBalance);
            this.mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Invest_Refunds_Oversold_Tokens()
        {
            this.totalSupply = 60;
            var amount = 190 * Satoshis;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.investor, this.totalSupply }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(this.identity));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            Assert.True(contract.Invest());

            Assert.Equal(0ul, contract.TokenBalance); // All tokens are sold
            this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.investor, 10 * Satoshis), Times.Once);
        }

        [Fact]
        public void Invest_Fails_If_TokenBalance_Is_Zero()
        {
            var amount = 1 * Satoshis;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.investor, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.persistentState.SetUInt64(nameof(ICOContract.TokenBalance), 0);

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_EndBlock_Reached()
        {
            var amount = 1 * Satoshis;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.mBlock.Setup(s => s.Number).Returns(5);

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_Investment_Amount_Is_Zero()
        {
            var amount = 0ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetSecondaryAddress_Call_Fails()
        {
            var amount = 10ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Failed());
            //this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetSecondaryAddress_Call_Returns_Zero_Address()
        {
            var amount = 10ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(Address.Zero));
            //this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));
            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetClaim_Call_Fails()
        {
            var amount = 10ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(this.identity));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Failed());
            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetClaim_Call_Returns_Null()
        {
            var amount = 10ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.mapperContract, 0, "GetSecondaryAddress", new object[] { this.investor }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(this.identity));
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.kycContract, 0, "GetClaim", new object[] { this.identity, 3 /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(null));
            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void WithdrawFunds_Fails_If_Caller_Is_Not_Owner()
        {
            var amount = 0ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.investor, amount));
            this.mBlock.Setup(m => m.Number).Returns(5);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawFunds_Fails_If_Sale_Is_Open()
        {
            var amount = 0ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, amount));
            this.mBlock.Setup(m => m.Number).Returns(4);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawFunds_Called_By_Owner_Success()
        {
            var amount = 0ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, amount));
            this.mBlock.Setup(m => m.Number).Returns(4);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawTokens_Called_By_Owner_After_Sale_Is_Closed_Success()
        {
            var amount = 0ul;
            this.mTransactionExecutor.Setup(m => m.Create<StandardToken>(this.mContractState.Object, 0, new object[] { this.totalSupply, this.name, this.symbol }, It.IsAny<ulong>())).Returns(this.createSuccess);

            var (contract, _) = this.Create(TokenType.StandardToken);
            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, amount));
            this.mBlock.Setup(m => m.Number).Returns(5);
            this.persistentState.SetUInt64(nameof(contract.TokenBalance), 100);
            this.mTransactionExecutor.Setup(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.owner, 100ul }, It.IsAny<ulong>())).Returns(TransferResult.Transferred(true));

            var success = contract.WithdrawTokens();

            Assert.True(success);
            Assert.Equal(0ul, this.persistentState.GetUInt64(nameof(contract.TokenBalance)));
            this.mTransactionExecutor.Verify(m => m.Call(this.mContractState.Object, this.tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { this.owner, 100ul }, 0));
        }
    }
}
