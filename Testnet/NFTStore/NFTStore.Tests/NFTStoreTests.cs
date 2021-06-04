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
        private readonly IPersistentState persistentState;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address creator;
        private readonly Address tokenOwner;
        private readonly Address contract;
        private readonly Address tokenContract;
        private readonly Address attacker;
        private readonly Address operatorAddress;
        private readonly ulong tokenId;
        public NFTStoreTests()
        {
            this.persistentState = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(this.persistentState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(this.mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.creator = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.tokenOwner = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.attacker = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.operatorAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
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
        public void Constructor_Success()
        {
            SetupMessage(creator, 0);

            Action action = () => new NFTStore(mContractState.Object);

            action.Invoking(c => c())
                  .Should()
                  .NotThrow();
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

            SetupGetOwnerOfTokenFails();

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
            SetupGetOwnerOfToken(contract);

            store.Invoking(s => s.Sale(tokenContract, tokenId, 100))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is already on sale.");
        }

        [Fact]
        public void Sale_Called_By_None_Owner_Of_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(tokenOwner);

            SetupMessage(attacker, 0);

            SetupIsApprovedForAll(tokenOwner, operatorAddress, false);

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

            SetupGetOwnerOfToken(tokenOwner);

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, false);

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

            SetupGetOwnerOfToken(tokenOwner);

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, true);

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

            SetupGetOwnerOfToken(tokenOwner);

            SetupMessage(operatorAddress, 0);

            SetupIsApprovedForAll(tokenOwner, operatorAddress, true);

            SetupTransferToken(tokenOwner, contract, tokenId, true);

            store.Sale(tokenContract, tokenId, 100);

            var saleInfo = store.GetSaleInfo(tokenContract, tokenId);

            Assert.Equal(new SaleInfo { Seller = tokenOwner, Price = 100 }, saleInfo);

            VerifyLog(new TokenOnSaleLog { Contract = tokenContract, TokenId = tokenId, Price = 100, Seller = tokenOwner });
        }

        private void VerifyLog<T>(T expectedLog) where T : struct
        {
            mContractLogger.Verify(x => x.Log(mContractState.Object, expectedLog), Times.Once());
        }

        private void SetupTransferToken(Address from, Address to, ulong tokenId, bool result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "TransferFrom", new object[] { from, to, tokenId }, 0))
                                .Returns(TransferResult.Succeed(result));
        }

        private void SetupMessage(Address caller, ulong amount)
        {
            mContractState.Setup(s => s.Message).Returns(new Message(contract, caller, amount));
        }

        private void SetupGetOwnerOfToken(Address owner)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "GetOwner", new object[] { tokenId }, 0))
                                .Returns(TransferResult.Succeed(owner));
        }

        private void SetupGetOwnerOfTokenFails()
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "GetOwner", new object[] { tokenId }, 0))
                                .Returns(TransferResult.Failed());
        }

        private void SetupIsApprovedForAll(Address owner, Address delegator, bool returnValue)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "IsApprovedForAll", new object[] { owner, delegator }, 0))
                                .Returns(TransferResult.Succeed(returnValue));
        }

        private void SetupIsApprovedForAllFails(Address owner, Address delegator)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "IsApprovedForAll", new object[] { owner, delegator }, 0))
                                .Returns(TransferResult.Failed());
        }
    }
}
