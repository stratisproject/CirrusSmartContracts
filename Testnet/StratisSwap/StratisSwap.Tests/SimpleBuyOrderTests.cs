using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static SimpleBuyOrder;

namespace StratisSwap.Tests
{
    public class SimpleBuyOrderTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Buyer;
        private readonly Address SellerOne;
        private readonly Address SellerTwo;
        private readonly Address Token;
        private readonly Address ContractAddress;
        private readonly ulong Amount;
        private readonly ulong Price;
        private readonly bool IsActive;
        private const ulong DefaultAmount = 10;
        private const ulong DefaultPrice = 10_000_000;
        private const ulong DefaultValue = 100_000_000;
        private const uint DefaultFullTokenInStratoshis = 100_000_000;

        public SimpleBuyOrderTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Buyer = "0x0000000000000000000000000000000000000001".HexToAddress();
            SellerOne = "0x0000000000000000000000000000000000000002".HexToAddress();
            SellerTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            Token = "0x0000000000000000000000000000000000000004".HexToAddress();
            ContractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        private SimpleBuyOrder NewSimpleBuyOrder(Address sender, ulong value, ulong price, ulong amount, uint fullTokenInStratoshis)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, value));
            MockContractState.Setup(x => x.GetBalance).Returns(() => value);
            MockContractState.Setup(x => x.Block.Number).Returns(12345);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Buyer))).Returns(Buyer);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Token))).Returns(Token);
            MockPersistentState.Setup(x => x.GetUInt32("FullTokenInStratoshis")).Returns(fullTokenInStratoshis);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(Price))).Returns(price);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(amount);
            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(true);
            MockPersistentState.Setup(x => x.IsContract(Token)).Returns(true);

            return new SimpleBuyOrder(MockContractState.Object, Token, fullTokenInStratoshis, price, amount);
        }

        [Fact]
        public void Creates_New_SimpleBuyOrder()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockPersistentState.Verify(x => x.SetAddress(nameof(Buyer), Buyer), Times.Once);
            Assert.Equal(Buyer, order.Buyer);

            MockPersistentState.Verify(x => x.SetAddress(nameof(Token), Token), Times.Once);
            Assert.Equal(Token, order.Token);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Price), DefaultPrice), Times.Once);
            Assert.Equal(DefaultPrice, order.Price);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), DefaultAmount), Times.Once);
            Assert.Equal(DefaultAmount, order.Amount);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), true), Times.Once);
            Assert.Equal(true, order.IsActive);
        }

        [Theory]
        [InlineData(DefaultValue - 1, DefaultPrice, DefaultAmount)]
        [InlineData(0, DefaultPrice, DefaultAmount)]
        [InlineData(DefaultValue, 0, DefaultAmount)]
        [InlineData(DefaultValue, DefaultPrice, 0)]
        public void Create_NewOrder_Fails_Invalid_Parameters(ulong value, ulong price, ulong amount)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Buyer, value));

            Assert.ThrowsAny<SmartContractAssertException>(() => new SimpleBuyOrder(MockContractState.Object, Token, DefaultFullTokenInStratoshis, amount, price));
        }

        [Fact]
        public void Success_GetOrderDetails()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            var actualOrderDetails = order.GetOrderDetails();

            var expectedOrderDetails = new OrderDetails
            {
                Buyer = Buyer,
                Token = Token,
                Price = DefaultPrice,
                Amount = DefaultAmount,
                Balance = DefaultValue,
                OrderType = nameof(SimpleBuyOrder),
                IsActive = true
            };

            Assert.Equal(expectedOrderDetails, actualOrderDetails);
        }

        #region Close Order
        [Fact]
        public void CloseOrder_Fails_Sender_IsNot_Owner()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(order.CloseOrder);
        }

        [Fact]
        public void CloseOrder_Success_Refunds_Balance()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            order.CloseOrder();

            MockContractState.Verify(x => x.GetBalance, Times.AtLeastOnce);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), Buyer, DefaultValue), Times.Once);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            MockPersistentState.Verify(x => x.GetAddress(nameof(Buyer)), Times.AtLeastOnce);
        }
        #endregion

        #region Sell Method
        [Fact]
        public void Sell_Fails_IfContract_IsNotActive()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(false);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Sell(DefaultAmount));

            var expectedCallParams = new object[] { SellerOne, Buyer, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), SellerOne, DefaultValue), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Sell_Fails_If_Sender_IsBuyer()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Buyer, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Sell(DefaultAmount));

            var expectedCallParams = new object[] { SellerOne, Buyer, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), SellerOne, DefaultValue), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Sell_Fails_If_TotalPrice_IsLessThan_ContractBalance()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.GetBalance).Returns(() => 0);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Sell(DefaultAmount));

            var expectedCallParams = new object[] { SellerOne, Buyer, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), SellerOne, DefaultValue), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Sell_Fails_If_SrcTransfer_Fails()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            var amountInStratoshis = DefaultAmount * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { SellerOne, Buyer, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(false));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Sell(DefaultAmount));

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), SellerOne, DefaultValue), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Sell_Success_Until_Amount_IsGone()
        {
            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            // First Seller
            ulong amountToSell = DefaultAmount - 5;
            ulong expectedUpdatedAmount = DefaultAmount - amountToSell;
            ulong orderCost = amountToSell * DefaultPrice;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            var amountInStratoshis = amountToSell * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { SellerOne, Buyer, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            // Transfers CRS to seller, callback to update the contracts balance
            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), SellerOne, orderCost))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => DefaultValue - orderCost));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), expectedUpdatedAmount))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(expectedUpdatedAmount));

            order.Sell(amountToSell);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), SellerOne, orderCost), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), expectedUpdatedAmount));

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Once);


            // Second Seller
            ulong secondAmountToSell = expectedUpdatedAmount;
            ulong secondUpdatedAmount = secondAmountToSell - expectedUpdatedAmount;
            ulong secondOrderCost = secondAmountToSell * DefaultPrice;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerTwo, 0));

            var secondAmountInStratoshis = secondAmountToSell * DefaultFullTokenInStratoshis;
            var secondExpectedCallParams = new object[] { SellerTwo, Buyer, secondAmountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", secondExpectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), secondUpdatedAmount))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(secondUpdatedAmount));

            order.Sell(secondAmountToSell);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", secondExpectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), SellerTwo, secondOrderCost), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), secondUpdatedAmount));

            // Shouldn't have enough balance to continue, close contract
            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Buyer, order.Balance), Times.Once);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.AtLeast(2));
        }

        [Theory]
        [InlineData(DefaultAmount)]
        [InlineData(DefaultAmount + 1)]
        public void Sell_Success_Remaining_Amount_IsZero_CloseOrder(ulong amountToSell)
        {
            amountToSell = DefaultAmount >= amountToSell ? amountToSell : DefaultAmount;

            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            ulong orderCost = amountToSell * DefaultPrice;
            ulong updatedContractBalance = order.Balance - orderCost;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            var amountInStratoshis = amountToSell * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { SellerOne, Buyer, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => updatedContractBalance));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), DefaultAmount - amountToSell))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(DefaultAmount - amountToSell));

            order.Sell(amountToSell);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), SellerOne, orderCost), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Once);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            // Only runs on the second test if there is a balance to transfer
            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Buyer, order.Balance), Times.AtMostOnce());

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Once);
        }

        [Theory]
        [InlineData(DefaultAmount, DefaultFullTokenInStratoshis)]
        [InlineData(DefaultAmount, 1_000_000)]
        [InlineData(DefaultAmount, 10_000)]
        [InlineData(DefaultAmount, 100)]
        public void Sell_CalculatesStratoshisToSend_Success(ulong amountToSell, uint fullTokenInStratoshies)
        {
            amountToSell = DefaultAmount >= amountToSell ? amountToSell : DefaultAmount;

            var order = NewSimpleBuyOrder(Buyer, DefaultValue, DefaultPrice, DefaultAmount, fullTokenInStratoshies);

            ulong orderCost = amountToSell * DefaultPrice;
            ulong updatedContractBalance = order.Balance - orderCost;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, SellerOne, 0));

            var amountInStratoshis = amountToSell * fullTokenInStratoshies;
            var expectedCallParams = new object[] { SellerOne, Buyer, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => updatedContractBalance));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), DefaultAmount - amountToSell))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(DefaultAmount - amountToSell));

            order.Sell(amountToSell);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), SellerOne, orderCost), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Once);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);
        }
        #endregion
    }
}
