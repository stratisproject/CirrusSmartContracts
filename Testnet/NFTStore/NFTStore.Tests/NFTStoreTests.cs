using Moq;
using Stratis.SmartContracts;
using FluentAssertions;
using Xunit;
using System;
using static NFTStore.NFTStore;

namespace NFTStore.Tests
{
    public class NFTStoreTests
    {
        private readonly IPersistentState inMemoryState;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address creator;
        private readonly Address tokenOwner;
        private readonly Address contract;
        private readonly Address tokenContract;
        private readonly Address attacker;
        private readonly Address operatorAddress;
        private readonly Address buyer;
        private readonly ulong tokenId;
        public NFTStoreTests()
        {
            this.inMemoryState = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(this.inMemoryState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.creator = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.tokenOwner = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.attacker = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.operatorAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.buyer = "0x0000000000000000000000000000000000000006".HexToAddress();
            this.tokenId = 3;
        }

        [Fact]
        public void Constructor_Sending_Coins_Fails()
        {
            SetupMessage(creator, 10);

            Action action = () => new NFTStore(mContractState.Object);

            action.Invoking(c => c())
                  .Should()
                  .Throw<SmartContractAssertException>()
                  .WithMessage("The method is not payable.");
        }

        [Fact]
        public void Sale_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenOwner, 10);

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void Sale_GetOwner_Fails()
        {
            SetupMessage(creator, 0);

            SetupGetOwnerOfToken(TransferResult.Failed());

            var store = new NFTStore(mContractState.Object);

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("GetOwner method call failed.");
        }

        [Fact]
        public void Sale_Selling_Already_OnSale_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenOwner, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is already on sale.");
        }

        [Fact]
        public void Sale_Called_By_None_Owner_Or_Operator_Of_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(attacker, 0);

            SetupIsApprovedForAll(tokenOwner, attacker, TransferResult.Succeed(false));

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The caller is not owner of the token nor approved for all.");
        }

        [Fact]
        public void Sale_None_Approved_Token_For_Contract_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, TransferResult.Succeed(false));

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token transfer failed. Be sure contract is approved to transfer token.");

        }

        [Fact]
        public void Sale_Owner_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, TransferResult.Succeed(true));

            store.Sale(tokenContract, tokenId, 100);

            var saleInfo = store.GetSaleInfo(tokenContract, tokenId);

            Assert.Equal(new SaleInfo { Seller = tokenOwner, Price = 100 }, saleInfo);

            VerifyLog(new TokenOnSaleLog { Contract = tokenContract, TokenId = tokenId, Price = 100, Seller = tokenOwner });
        }

        [Fact]
        public void Sale_Operator_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(operatorAddress, 0);

            SetupIsApprovedForAll(tokenOwner, operatorAddress, TransferResult.Succeed(true));

            SetupTransferToken(tokenOwner, contract, tokenId, TransferResult.Succeed(true));

            store.Sale(tokenContract, tokenId, 100);

            var saleInfo = store.GetSaleInfo(tokenContract, tokenId);

            Assert.Equal(new SaleInfo { Seller = tokenOwner, Price = 100 }, saleInfo);

            VerifyLog(new TokenOnSaleLog { Contract = tokenContract, TokenId = tokenId, Price = 100, Seller = tokenOwner });
        }

        [Fact]
        public void Buy_Token_Is_Not_On_Sale_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            store.Invoking(s => s.Buy(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is not on sale.");
        }

        [Fact]
        public void Buy_Transferred_Amount_Deos_Not_Match_Token_Price_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(buyer, 90);

            store.Invoking(s => s.Buy(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Transferred amount is not matching exact price of the token.");
        }

        [Fact]
        public void Buy_Transfer_Returns_False_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(buyer, 100);
            SetupSafeTransferToken(contract, buyer, tokenId, true);

            SetupTransfer(tokenOwner, 100, TransferResult.Failed());

            store.Invoking(s => s.Buy(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Transfer failed.");
        }

        [Fact]
        public void Buy_Token_Buying_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(buyer, 100);

            SetupSafeTransferToken(contract, buyer, tokenId, true);

            SetupTransfer(tokenOwner, 100, TransferResult.Succeed());

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Seller = tokenOwner, Buyer = buyer });

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));
        }

        [Fact]
        public void CancelSale_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenOwner, 100);

            store.Invoking(s => s.CancelSale(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void CancelSale_Token_Is_Not_On_Sale_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenOwner, 0);

            store.Invoking(s => s.CancelSale(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is not on sale.");
        }

        [Fact]
        public void CancelSale_Called_By_None_Token_Owner_Or_Operator_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(attacker, 0);
            SetupIsApprovedForAll(tokenOwner, attacker, TransferResult.Succeed(false));

            store.Invoking(s => s.CancelSale(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The caller is not owner of the token nor approved for all.");
        }

        [Fact]
        public void CancelSale_SafeTokenTransfer_Returns_False_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(tokenOwner, 0);

            SetupSafeTransferToken(contract, tokenOwner, tokenId, false);

            store.Invoking(s => s.CancelSale(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token transfer failed.");
        }

        [Fact]
        public void CancelSale_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            SetSaleInfo(saleInfo);

            SetupMessage(tokenOwner, 0);

            SetupSafeTransferToken(contract, tokenOwner, tokenId, true);

            store.CancelSale(tokenContract, tokenId);

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));

            VerifyLog(new TokenSaleCanceledLog { Contract = tokenContract, TokenId = tokenId, Seller = tokenOwner });
        }

        private void SetSaleInfo(SaleInfo saleInfo)
        {
            inMemoryState.SetStruct($"SaleInfo:{tokenContract}:{tokenId}", saleInfo);
        }

        private void VerifyLog<T>(T expectedLog) where T : struct
        {
            mContractLogger.Verify(x => x.Log(mContractState.Object, expectedLog), Times.Once());
        }

        private void SetupTransferToken(Address from, Address to, ulong tokenId, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "TransferFrom", new object[] { from, to, tokenId }, 0))
                                .Returns(result);
        }

        private void SetupSafeTransferToken(Address from, Address to, ulong tokenId, bool result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "SafeTransferFrom", new object[] { from, to, tokenId }, 0))
                                .Returns(TransferResult.Succeed(result));
        }

        private void SetupTransfer(Address to, ulong amount, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, to, amount))
                                .Returns(result);
        }

        private void SetupMessage(Address caller, ulong amount)
        {
            mContractState.Setup(s => s.Message).Returns(new Message(contract, caller, amount));
        }

        private void SetupGetOwnerOfToken(TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "GetOwner", new object[] { tokenId }, 0))
                                .Returns(result);
        }


        private void SetupIsApprovedForAll(Address owner, Address delegator, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "IsApprovedForAll", new object[] { owner, delegator }, 0))
                                .Returns(result);
        }
    }
}
