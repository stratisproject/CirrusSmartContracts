using Moq;
using Stratis.SmartContracts;
using Xunit;

namespace DividendTokenContract.Tests
{
    public class DividendTokenTests
    {
        private readonly IPersistentState state;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address owner;
        private readonly Address tokenHolder;
        private readonly Address currentContract;

        private readonly string name;
        private readonly string symbol;
        private readonly UInt256 totalSupply;
        private readonly byte decimals;

        public DividendTokenTests()
        {
            this.state = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(state);
            this.mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.tokenHolder = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.currentContract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.name = "Test Token";
            this.symbol = "TST";
            this.totalSupply = 1_000;
            this.decimals = 0;
        }


        [Fact]
        public void Deposited_Dividend_Should_Be_Distributed_Equaly()
        {
            var dividend = 1000ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 100));

            contract.Receive();

            Assert.Equal(dividend, contract.Dividends);
            Assert.Equal(100ul, contract.GetDividends(tokenHolder));
            Assert.Equal(100ul, contract.GetTotalDividends(tokenHolder));
            Assert.Equal(900ul, contract.GetDividends(owner));
            Assert.Equal(900ul, contract.GetTotalDividends(owner));
        }

        [Fact]
        public void Multiple_Deposited_Dividend_Should_Be_Distributed_Equaly()
        {
            var dividend = 1000ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 100));

            contract.Receive();

            Assert.True(contract.TransferTo(tokenHolder, 100));

            contract.Receive();

            Assert.True(contract.TransferTo(tokenHolder, 100));

            Assert.Equal(2 * dividend, contract.Dividends);
            Assert.Equal(300ul, contract.GetDividends(tokenHolder));
            Assert.Equal(300ul, contract.GetTotalDividends(tokenHolder));

            Assert.Equal(1700ul, contract.GetDividends(owner));
            Assert.Equal(1700ul, contract.GetTotalDividends(owner));
        }

        [Fact]
        public void Cumulative_Dividends_Should_Be_Distributed_Equaly()
        {
            var dividend = 1000ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 100));

            contract.Receive();
            contract.Receive();

            Assert.Equal(2 * dividend, contract.Dividends);
            Assert.Equal(2 * 100ul, contract.GetDividends(tokenHolder));
            Assert.Equal(2 * 900ul, contract.GetDividends(owner));
        }

        /// <summary>
        /// In the case of dividend per token is 1.5 satoshi,
        /// address that holds 1 token will have 1 dividend at first deposit despite actual earned amount is  1.5 satoshi
        /// because satoshi unit doesn't support decimal points. Still 0.5 satoshi is accounted in the account
        /// But after second deposit by same rate (0.5 satoshi per token), the user will be able to have 1 satoshi (0.5 + 0.5)
        /// </summary>
        [Fact]
        public void Cumulative_Deposits_Should_Be_Distributed_Equaly_When_Dividends_Have_Decimal_Value()
        {
            var dividend = 500ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 1));

            contract.Receive();

            Assert.Equal(dividend, contract.Dividends);
            Assert.Equal(0ul, contract.GetDividends(tokenHolder));
            Assert.Equal(499ul, contract.GetDividends(owner));

            contract.Receive();

            Assert.Equal(2 * dividend, contract.Dividends);
            Assert.Equal(1ul, contract.GetDividends(tokenHolder));
            Assert.Equal(999ul, contract.GetDividends(owner));
        }

        [Fact]
        public void Deposited_Dividend_Should_Be_Withdrawable()
        {
            var dividend = 500ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));
            mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, tokenHolder, 5)).Returns(TransferResult.Succeed(true));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 11));

            contract.Receive();

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, tokenHolder, 0));

            contract.Withdraw();

            mTransactionExecutor.Verify(s => s.Transfer(mContractState.Object, tokenHolder, 5), Times.Once);
            Assert.Equal(0ul, contract.GetDividends());
            var account = state.GetStruct<DividendToken.Account>($"Account:{tokenHolder}");
            Assert.Equal((UInt256)500, account.DividendBalance);
            Assert.Equal(5ul, account.WithdrawnDividends);
            Assert.Equal(dividend, account.CreditedDividends);
        }

        [Fact]
        public void GetDividends_Returns_Current_Sender_Dividends()
        {
            var dividend = 1000ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));
            mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, tokenHolder, 100)).Returns(TransferResult.Succeed(true));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 100));

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, tokenHolder, dividend));
            contract.Receive();

            Assert.Equal(100ul, contract.GetDividends());
        }

        /// <summary>
        /// GetTotalDividends should to return Withdrawable + Withdrawn dividends
        /// </summary>
        [Fact]
        public void GetTotalDividends_Returns_Current_Sender_TotalDividends()
        {
            var dividend = 1000ul;

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, owner, dividend));
            mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, tokenHolder, 100)).Returns(TransferResult.Succeed(true));

            var contract = new DividendToken(mContractState.Object, totalSupply, name, symbol, decimals);

            Assert.True(contract.TransferTo(tokenHolder, 100));

            contract.Receive();

            mContractState.Setup(m => m.Message).Returns(new Message(currentContract, tokenHolder, 0));

            contract.Withdraw();

            mTransactionExecutor.Verify(s => s.Transfer(mContractState.Object, tokenHolder, 100), Times.Once);
            Assert.Equal(0ul, contract.GetDividends());
            Assert.Equal(100ul, contract.GetTotalDividends());
        }
    }
}
