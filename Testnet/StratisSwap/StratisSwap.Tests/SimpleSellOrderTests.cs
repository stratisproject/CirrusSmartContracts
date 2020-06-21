using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static SimpleSellOrder;

namespace StratisSwap.Tests
{
    public class SimpleSellOrderTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Seller;
        private readonly Address BuyerOne;
        private readonly Address BuyerTwo;
        private readonly Address Token;
        private readonly Address ContractAddress;
        private readonly ulong Amount;
        private readonly ulong Price;
        private readonly bool IsActive;
        private const ulong DefaultAmount = 10;
        private const ulong DefaultPrice = 10_000_000;
        private const ulong DefaultZeroValue = 0;
        private const ulong DefaultCostValue = 100_000_000;
        private const uint DefaultFullTokenInStratoshis = 100_000_000;

        public SimpleSellOrderTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Seller = "0x0000000000000000000000000000000000000001".HexToAddress();
            BuyerOne = "0x0000000000000000000000000000000000000002".HexToAddress();
            BuyerTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            Token = "0x0000000000000000000000000000000000000004".HexToAddress();
            ContractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        private SimpleSellOrder NewSimpleSellOrder(Address sender, ulong value, ulong price, ulong amount, uint fullTokenInStratoshis)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, value));
            MockContractState.Setup(x => x.GetBalance).Returns(() => value);
            MockContractState.Setup(x => x.Block.Number).Returns(12345);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Seller))).Returns(Seller);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Token))).Returns(Token);
            MockPersistentState.Setup(x => x.GetUInt32("FullTokenInStratoshis")).Returns(fullTokenInStratoshis);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(Price))).Returns(price);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(amount);
            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(true);
            MockPersistentState.Setup(x => x.IsContract(Token)).Returns(true);

            return new SimpleSellOrder(MockContractState.Object, Token, fullTokenInStratoshis, price, amount);
        }

        [Fact]
        public void Creates_New_SimpleSellOrder()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockPersistentState.Verify(x => x.SetAddress(nameof(Seller), Seller));
            Assert.Equal(Seller, order.Seller);
            
            MockPersistentState.Verify(x => x.SetAddress(nameof(Token), Token));
            Assert.Equal(Token, order.Token);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Price), DefaultPrice));
            Assert.Equal(DefaultPrice, order.Price);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), DefaultAmount));
            Assert.Equal(DefaultAmount, order.Amount);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), true));
            Assert.Equal(true, order.IsActive);
        }

        [Theory]
        [InlineData(0, DefaultAmount)]
        [InlineData(DefaultPrice, 0)]
        public void Create_NewOrder_Fails_Invalid_Parameters(ulong price, ulong amount)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Seller, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => new SimpleSellOrder(MockContractState.Object, Token, DefaultFullTokenInStratoshis, amount, price));
        }

        [Fact]
        public void Success_GetOrderDetails()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, DefaultZeroValue));

            var expectedBalance = DefaultAmount * 100_000_000;
            var expectedCallParams = new object[] { Seller, ContractAddress };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "Allowance", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(expectedBalance));

            var actualOrderDetails = order.GetOrderDetails();

            var expectedOrderDetails = new OrderDetails
            {
                Seller = Seller,
                Token = Token,
                Price = DefaultPrice,
                Amount = DefaultAmount,
                OrderType = nameof(SimpleSellOrder),
                IsActive = true,
                Balance = expectedBalance
            };

            Assert.Equal(expectedOrderDetails, actualOrderDetails);
        }

        #region Close Order
        [Fact]
        public void CloseOrder_Fails_Sender_IsNot_Owner()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(order.CloseOrder);
        }

        [Fact]
        public void CloseOrder_Success()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            order.CloseOrder();

            MockPersistentState.Verify(x => x.GetAddress(nameof(Seller)), Times.Once);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);
        }
        #endregion

        #region Buy Method
        [Fact]
        public void Buy_Fails_IfContract_IsNotActive()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(false);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Buy(DefaultAmount));

            var expectedCallParams = new object[] { Seller, BuyerOne, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), Seller, DefaultAmount), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Buy_Fails_If_Sender_IsSeller()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Seller, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Buy(DefaultAmount));

            var expectedCallParams = new object[] { Seller, BuyerOne, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), Seller, DefaultAmount), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Buy_Fails_If_MessageValue_IsLessThan_Cost()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, DefaultZeroValue));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Buy(DefaultAmount));

            var expectedCallParams = new object[] { Seller, BuyerOne, DefaultAmount };

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Never);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), Seller, DefaultAmount), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Buy_Fails_If_SrcTransfer_Fails()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, DefaultCostValue));

            var amountInStratoshis = DefaultAmount * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { Seller, BuyerOne, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(false));

            Assert.ThrowsAny<SmartContractAssertException>(() => order.Buy(DefaultAmount));

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockInternalExecutor.Verify(x => x.Transfer(It.IsAny<ISmartContractState>(), Seller, DefaultCostValue), Times.Never);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Never);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Never);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Never);
        }

        [Fact]
        public void Buy_Success_Until_Amount_IsGone()
        {
            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            // First Seller
            ulong amountToBuy = DefaultAmount - 5;
            ulong expectedUpdatedAmount = DefaultAmount - amountToBuy;
            ulong orderCost = amountToBuy * DefaultPrice;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, orderCost));

            var amountInStratoshis = amountToBuy * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { Seller, BuyerOne, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            // Transfers CRS to seller, callback to update the contracts balance
            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), Seller, orderCost))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => DefaultCostValue - orderCost));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), expectedUpdatedAmount))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(expectedUpdatedAmount));

            order.Buy(amountToBuy);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Seller, orderCost), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), expectedUpdatedAmount));

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Once);


            // Second Seller
            ulong secondAmountToBuy = expectedUpdatedAmount;
            ulong secondUpdatedAmount = secondAmountToBuy - expectedUpdatedAmount;
            ulong secondOrderCost = secondAmountToBuy * DefaultPrice;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerTwo, secondOrderCost));

            var secondAmountInStratoshis = secondAmountToBuy * DefaultFullTokenInStratoshis;
            var secondExpectedCallParams = new object[] { Seller, BuyerTwo, secondAmountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", secondExpectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), secondUpdatedAmount))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(secondUpdatedAmount));

            order.Buy(secondAmountToBuy);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", secondExpectedCallParams, 0), Times.Once);

            // Runs twice because in this case we're sending the same amount to the same seller (owner)
            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Seller, secondOrderCost), Times.AtLeast(2));

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), secondUpdatedAmount));

            // Shouldn't have enough balance to continue, close contract
            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.AtLeast(2));
        }

        [Theory]
        [InlineData(DefaultAmount)]
        [InlineData(DefaultAmount + 1)]
        public void Buy_Success_Remaining_Amount_IsZero_CloseOrder(ulong amountToBuy)
        {
            amountToBuy = DefaultAmount >= amountToBuy ? amountToBuy : DefaultAmount;

            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, DefaultFullTokenInStratoshis);

            ulong orderCost = amountToBuy * DefaultPrice;
            ulong updatedContractBalance = order.Balance - orderCost;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, orderCost));

            var amountInStratoshis = amountToBuy * DefaultFullTokenInStratoshis;
            var expectedCallParams = new object[] { Seller, BuyerOne, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => updatedContractBalance));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), DefaultAmount - amountToBuy))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(DefaultAmount - amountToBuy));

            order.Buy(amountToBuy);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Seller, orderCost), Times.Once);

            // Only runs on the second test if there is a balance to transfer
            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), BuyerOne, order.Balance), Times.AtMostOnce());

            MockPersistentState.Verify(x => x.SetUInt64(nameof(Amount), 0), Times.Once);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<Transaction>()), Times.Once);
        }

        [Theory]
        [InlineData(DefaultAmount, DefaultFullTokenInStratoshis)]
        [InlineData(DefaultAmount, 1_000_000)]
        [InlineData(DefaultAmount, 10_000)]
        [InlineData(DefaultAmount, 100)]
        public void Buy_CalculatesStratoshisToSend_Success(ulong amountToBuy, uint fullTokenInStratoshies)
        {
            amountToBuy = DefaultAmount >= amountToBuy ? amountToBuy : DefaultAmount;

            var order = NewSimpleSellOrder(Seller, DefaultZeroValue, DefaultPrice, DefaultAmount, fullTokenInStratoshies);

            ulong orderCost = amountToBuy * DefaultPrice;
            ulong updatedContractBalance = order.Balance - orderCost;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, orderCost));

            var amountInStratoshis = amountToBuy * fullTokenInStratoshies;
            var expectedCallParams = new object[] { Seller, BuyerOne, amountInStratoshis };

            MockInternalExecutor.Setup(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0))
                .Returns(TransferResult.Transferred(true));

            MockInternalExecutor.Setup(x => x.Transfer(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>()))
                .Callback(() => MockContractState.Setup(x => x.GetBalance).Returns(() => updatedContractBalance));

            MockPersistentState.Setup(x => x.SetUInt64(nameof(Amount), DefaultAmount - amountToBuy))
                .Callback(() => MockPersistentState.Setup(x => x.GetUInt64(nameof(Amount))).Returns(DefaultAmount - amountToBuy));

            order.Buy(amountToBuy);

            MockInternalExecutor.Verify(x =>
                x.Call(It.IsAny<ISmartContractState>(), Token, 0, "TransferFrom", expectedCallParams, 0), Times.Once);

            MockContractState.Verify(x => x.InternalTransactionExecutor
                .Transfer(It.IsAny<ISmartContractState>(), Seller, orderCost), Times.Once);
        }

        #endregion
    }
}
