using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace DividendTokenContract.Tests
{
    public class DividendTokenTests
    {
        private const ulong Satoshis = 100_000_000;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly IPersistentState persistentState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private Address owner;
        private Address tokenHolder;
        private Address contract;

        private string name;
        private string symbol;
        private ulong totalSupply;

        public DividendTokenTests()
        {
            this.mContractLogger = new Mock<IContractLogger>();
            this.persistentState = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(this.persistentState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.tokenHolder = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.name = "Test Token";
            this.symbol = "TST";
            this.totalSupply = 1_000;
        }

        [Fact]
        public void Deposited_Dividend_Should_Be_Distributed_Equaly()
        {
            var dividend = 1000ul;

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 100));

            contract.Receive();

            Assert.Equal(dividend, contract.Dividends);
            Assert.Equal(100ul, contract.GetDividends(this.tokenHolder));
            Assert.Equal(900ul, contract.GetDividends(this.owner));
        }

        [Fact]
        public void Cumulative_Dividends_Should_Be_Distributed_Equaly()
        {
            var dividend = 1000ul;

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 100));

            contract.Receive();
            contract.Receive();

            Assert.Equal(2 * dividend, contract.Dividends);
            Assert.Equal(2 * 100ul, contract.GetDividends(this.tokenHolder));
            Assert.Equal(2 * 900ul, contract.GetDividends(this.owner));
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

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 1));

            contract.Receive();

            Assert.Equal(dividend, contract.Dividends);
            Assert.Equal(0ul, contract.GetDividends(this.tokenHolder));
            Assert.Equal(499ul, contract.GetDividends(this.owner));

            contract.Receive();

            Assert.Equal(2 * dividend, contract.Dividends);
            Assert.Equal(1ul, contract.GetDividends(this.tokenHolder));
            Assert.Equal(999ul, contract.GetDividends(this.owner));
        }

        [Fact]
        public void Deposited_Dividend_Should_Be_Withdrawable()
        {
            var dividend = 1000ul;

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));
            this.mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            this.mTransactionExecutor.Setup(m => m.Transfer(this.mContractState.Object, this.tokenHolder, 100)).Returns(TransferResult.Transferred(true));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 100));

            contract.Receive();

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.tokenHolder, 0));

            contract.Withdraw();

            this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.tokenHolder, 100), Times.Once);
            Assert.Equal(0ul, contract.GetDividends());
        }

        [Fact]
        public void GetDividends_Returns_Current_Sender_Dividends()
        {
            var dividend = 1000ul;

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));
            this.mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            this.mTransactionExecutor.Setup(m => m.Transfer(this.mContractState.Object, this.tokenHolder, 100)).Returns(TransferResult.Transferred(true));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 100));

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.tokenHolder, dividend));
            contract.Receive();

            Assert.Equal(100ul, contract.GetDividends());
        }

        /// <summary>
        /// GetTotalDividends should returns Withdrawable + Withdrawn dividends
        /// </summary>
        [Fact]
        public void GetTotalDividends_Returns_Current_Sender_TotalDividends()
        {
            var dividend = 1000ul;

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, dividend));
            this.mContractState.Setup(m => m.GetBalance).Returns(() => dividend);
            this.mTransactionExecutor.Setup(m => m.Transfer(this.mContractState.Object, this.tokenHolder, 100)).Returns(TransferResult.Transferred(true));

            var contract = new DividendToken(this.mContractState.Object, this.totalSupply, this.name, this.symbol);

            Assert.True(contract.TransferTo(this.tokenHolder, 100));

            contract.Receive();

            this.mContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.tokenHolder, 0));

            contract.Withdraw();

            this.mTransactionExecutor.Verify(s => s.Transfer(this.mContractState.Object, this.tokenHolder, 100), Times.Once);
            Assert.Equal(0ul, contract.GetDividends());
            Assert.Equal(100ul, contract.GetTotalDividends());
        }
    }
}
