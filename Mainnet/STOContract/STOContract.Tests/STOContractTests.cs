namespace STOContractTests
{
    using Moq;
    using Stratis.SmartContracts;
    using Xunit;
    using SalePeriod = STOContract.SalePeriod;
    using SalePeriodInput = STOContract.SalePeriodInput;
    using TokenType = STOContract.TokenType;

    public class STOContractTests
    {
        private const ulong Satoshis = 100_000_000;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;
        private readonly Mock<ISerializer> mSerializer;
        private readonly Mock<IBlock> mBlock;

        private readonly Address sender;
        private readonly Address owner;
        private readonly Address investor;
        private readonly Address identity;
        private readonly Address currentContract;
        private readonly Address tokenContract;
        private readonly Address kycContract;
        private readonly Address mapperContract;

        private readonly InMemoryState persistentState;
        private UInt256 totalSupply;
        private readonly string name;
        private readonly string symbol;
        private readonly uint decimals;

        public STOContractTests()
        {
            mContractLogger = new Mock<IContractLogger>();
            mContractState = new Mock<ISmartContractState>();
            mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            persistentState = new InMemoryState();
            mBlock = new Mock<IBlock>();
            mContractState.Setup(s => s.Block).Returns(mBlock.Object);
            mContractState.Setup(s => s.PersistentState).Returns(persistentState);
            mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            mSerializer = new Mock<ISerializer>();
            mContractState.Setup(s => s.Serializer).Returns(mSerializer.Object);
            sender = "0x0000000000000000000000000000000000000001".HexToAddress();
            owner = "0x0000000000000000000000000000000000000002".HexToAddress();
            investor = "0x0000000000000000000000000000000000000003".HexToAddress();
            identity = "0x0000000000000000000000000000000000000004".HexToAddress();
            currentContract = "0x0000000000000000000000000000000000000005".HexToAddress();
            tokenContract = "0x0000000000000000000000000000000000000006".HexToAddress();
            kycContract = "0x0000000000000000000000000000000000000007".HexToAddress();
            mapperContract = "0x0000000000000000000000000000000000000008".HexToAddress();

            name = "Test Token";
            symbol = "TST";
            totalSupply = 100 * Satoshis;
            decimals = 0;
            persistentState.IsContractResult = true;
        }

        private ICreateResult CreateSucceed()
        {
            var mock = new Mock<ICreateResult>();

            mock.SetupGet(m => m.Success).Returns(true);
            mock.SetupGet(m => m.NewContractAddress).Returns(tokenContract);

            return mock.Object;
        }

        private ICreateResult CreateFailed()
        {
            var mock = new Mock<ICreateResult>();

            mock.SetupGet(m => m.Success).Returns(false);

            return mock.Object;
        }

        private ITransferResult TransferSucceed(object returnValue = null)
        {
            var mock = new Mock<ITransferResult>();

            mock.SetupGet(m => m.Success).Returns(true);
            mock.SetupGet(m => m.ReturnValue).Returns(returnValue);

            return mock.Object;
        }

        private ITransferResult TransferFailed()
        {
            var mock = new Mock<ITransferResult>();

            mock.SetupGet(m => m.Success).Returns(false);

            return mock.Object;
        }

        [Fact]
        public void Constructor_IsContract_ReturnsFalse_ThrowsAssertException()
        {
            persistentState.IsContractResult = false;

            Assert.Throws<SmartContractAssertException>(() => Create(TokenType.StandardToken));
        }

        [Fact]
        public void Constructor_TokenType_HigherThan2_ThrowsAssertException()
        {
            var tokenType = (TokenType)3;

            Assert.Throws<SmartContractAssertException>(() => Create(tokenType));
        }

        [Fact]
        public void Constructor_CreateReturnsFailedResult_ThrowsAssertException()
        {
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateFailed());
            Assert.Throws<SmartContractAssertException>(() => Create(TokenType.StandardToken));

            mTransactionExecutor.Verify(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsStandardToken_Success()
        {
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            var (contract, periods) = Create(TokenType.StandardToken);

            Assert.Equal(totalSupply, contract.TokenBalance);
            Assert.Equal(owner, contract.Owner);
            Assert.Equal(tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            mTransactionExecutor.Verify(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, 0), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsDividendToken_Success()
        {
            mTransactionExecutor.Setup(m => m.Create<DividendToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            var (contract, periods) = Create(TokenType.DividendToken);

            Assert.Equal(totalSupply, contract.TokenBalance);
            Assert.Equal(owner, contract.Owner);
            Assert.Equal(tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            mTransactionExecutor.Verify(m => m.Create<DividendToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, 0), Times.Once);
        }

        [Fact]
        public void Constructor_TokenTypeIsNonFungibleToken_Success()
        {
            mTransactionExecutor.Setup(m => m.Create<NonFungibleToken>(mContractState.Object, 0, new object[] { name, symbol }, It.IsAny<ulong>())).Returns(CreateSucceed());
            var (contract, periods) = Create(TokenType.NonFungibleToken);

            Assert.Equal((UInt256)ulong.MaxValue, contract.TokenBalance);
            Assert.Equal(owner, contract.Owner);
            Assert.Equal(tokenContract, contract.TokenAddress);
            Assert.Equal(4ul, contract.EndBlock);
            Assert.Equal(periods, contract.SalePeriods);
            mTransactionExecutor.Verify(m => m.Create<NonFungibleToken>(mContractState.Object, 0, new object[] { name, symbol }, 0), Times.Once);
        }

        public (STOContract contract, SalePeriod[] periods) Create(TokenType tokenType)
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

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, 0));
            mBlock.Setup(s => s.Number).Returns(1);
            mSerializer.Setup(m => m.ToArray<SalePeriodInput>(new byte[0])).Returns(periodInputs);
            var contract = new STOContract(mContractState.Object, owner, (uint)tokenType, totalSupply, name, symbol, decimals, kycContract, mapperContract, new byte[0]);
            return (contract, periods);
        }

        [Fact]
        public void Invest_CalledForStandardToken_Success()
        {
            var amount = 15 * Satoshis;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { investor, (UInt256)5 }, It.IsAny<ulong>())).Returns(TransferSucceed(true));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(identity));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { identity, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferSucceed(new byte[] { 1 }));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 5ul, contract.TokenBalance);
            mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

            mBlock.Setup(s => s.Number).Returns(4);
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { investor, (UInt256)3 }, It.IsAny<ulong>())).Returns(TransferSucceed(true));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 8ul, contract.TokenBalance);
            mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Invest_CalledForNonFungibleToken_Success()
        {
            var amount = 15 * Satoshis;
            var totalSupply = (UInt256)ulong.MaxValue;

            mTransactionExecutor.Setup(m => m.Create<NonFungibleToken>(mContractState.Object, 0, new object[] { name, symbol }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.NonFungibleToken);

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(NonFungibleToken.MintAll), new object[] { investor, 5ul }, It.IsAny<ulong>())).Returns(TransferSucceed(true));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(identity));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { identity, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferSucceed(new byte[] { 1 }));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 5, contract.TokenBalance);
            mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);

            mBlock.Setup(s => s.Number).Returns(4);
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(NonFungibleToken.MintAll), new object[] { investor, 3ul }, It.IsAny<ulong>())).Returns(TransferSucceed(true));

            Assert.True(contract.Invest());

            Assert.Equal(totalSupply - 8ul, contract.TokenBalance);
            mTransactionExecutor.Verify(s => s.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Invest_Refunds_Oversold_Tokens()
        {
            totalSupply = 60;
            var amount = 190 * Satoshis;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { investor, totalSupply }, It.IsAny<ulong>())).Returns(TransferSucceed(true));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(identity));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { identity, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferSucceed(new byte[] { 1 }));
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, investor, 10 * Satoshis)).Returns(TransferSucceed());
            Assert.True(contract.Invest());

            Assert.Equal((UInt256)0, contract.TokenBalance); // All tokens are sold
            mTransactionExecutor.Verify(s => s.Transfer(mContractState.Object, investor, 10 * Satoshis), Times.Once);
        }

        [Fact]
        public void Invest_Fails_If_TokenBalance_Is_Zero()
        {
            var amount = 1 * Satoshis;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { investor, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferSucceed(new byte[] { 1 }));

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));
            persistentState.SetUInt256(nameof(STOContract.TokenBalance), 0);

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_EndBlock_Reached()
        {
            var amount = 1 * Satoshis;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));
            mBlock.Setup(s => s.Number).Returns(5);

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_Investment_Amount_Is_Zero()
        {
            var amount = 0ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetSecondaryAddress_Call_Fails()
        {
            var amount = 10ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferFailed());
            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetSecondaryAddress_Call_Returns_Zero_Address()
        {
            var amount = 10ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(Address.Zero));
            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetClaim_Call_Fails()
        {
            var amount = 10ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(identity));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { identity, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferFailed());
            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void Invest_Fails_If_GetClaim_Call_Returns_Null()
        {
            var amount = 10ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, mapperContract, 0, "GetSecondaryAddress", new object[] { investor }, It.IsAny<ulong>())).Returns(TransferSucceed(identity));
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, kycContract, 0, "GetClaim", new object[] { identity, 3U /*shufti kyc*/ }, It.IsAny<ulong>())).Returns(TransferSucceed(null));
            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));

            Assert.Throws<SmartContractAssertException>(() => contract.Invest());
        }

        [Fact]
        public void WithdrawFunds_Fails_If_Caller_Is_Not_Owner()
        {
            var amount = 0ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, investor, amount));
            mBlock.Setup(m => m.Number).Returns(5);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawFunds_Fails_If_Sale_Is_Open()
        {
            var amount = 0ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, amount));
            mBlock.Setup(m => m.Number).Returns(4);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawFunds_Called_By_Owner_Success()
        {
            var amount = 0ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, amount));
            mBlock.Setup(m => m.Number).Returns(4);

            Assert.Throws<SmartContractAssertException>(() => contract.WithdrawFunds());
        }

        [Fact]
        public void WithdrawTokens_Called_By_Owner_After_Sale_Is_Closed_Success()
        {
            var amount = 0ul;
            mTransactionExecutor.Setup(m => m.Create<StandardToken>(mContractState.Object, 0, new object[] { totalSupply, name, symbol, decimals }, It.IsAny<ulong>())).Returns(CreateSucceed());

            var (contract, _) = Create(TokenType.StandardToken);
            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, amount));
            mBlock.Setup(m => m.Number).Returns(5);
            persistentState.SetUInt256(nameof(contract.TokenBalance), 100);
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { owner, (UInt256)100 }, It.IsAny<ulong>())).Returns(TransferSucceed(true));

            var success = contract.WithdrawTokens();

            Assert.True(success);
            Assert.Equal((UInt256)0, persistentState.GetUInt256(nameof(contract.TokenBalance)));
            mTransactionExecutor.Verify(m => m.Call(mContractState.Object, tokenContract, 0, nameof(StandardToken.TransferTo), new object[] { owner, (UInt256)100 }, 0));
        }
    }
}
