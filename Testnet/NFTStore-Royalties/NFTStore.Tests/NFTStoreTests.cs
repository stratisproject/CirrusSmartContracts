using Moq;
using Stratis.SmartContracts;
using FluentAssertions;
using Xunit;
using System;
using static NFTStore;

namespace NFTStoreTests
{
    public class NFTStoreTests
    {
        private readonly InMemoryState state;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;
        private readonly Mock<ISerializer> mSerializer;

        private readonly Address creator;
        private readonly Address tokenOwner;
        private readonly Address contract;
        private readonly Address tokenContract;
        private readonly Address attacker;
        private readonly Address operatorAddress;
        private readonly Address buyer;
        private readonly UInt256 tokenId;
        public NFTStoreTests()
        {
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mContractLogger = new Mock<IContractLogger>();
            mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            mSerializer = new Mock<ISerializer>();
            mContractState.Setup(s => s.PersistentState).Returns(state);
            mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            mContractState.Setup(s => s.Serializer).Returns(mSerializer.Object);
            creator = "0x0000000000000000000000000000000000000001".HexToAddress();
            tokenOwner = "0x0000000000000000000000000000000000000002".HexToAddress();
            contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            tokenContract = "0x0000000000000000000000000000000000000004".HexToAddress();
            attacker = "0x0000000000000000000000000000000000000005".HexToAddress();
            operatorAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
            buyer = "0x0000000000000000000000000000000000000006".HexToAddress();
            tokenId = 3;

            state.SetIsContract(tokenContract, true);

            SetupBlockNumber(1);
        }

        [Fact]
        public void Constructor_Set_Properties_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            Assert.Equal(1ul, store.CreatedAt);
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
        public void OnNonFungibleTokenReceived_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenContract, 10);

            var price = 5_00_000_000ul;

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, BitConverter.GetBytes(price)))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Called_By_None_Contract_Fails()
        {
            SetupMessage(creator, 0);

            state.SetIsContract(tokenContract, false);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            var price = 5_00_000_000ul;
            var priceBytes = BitConverter.GetBytes(price);

            mSerializer.Setup(m => m.ToUInt64(priceBytes)).Returns(price);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, priceBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The Caller is not a contract.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_GetOwner_Call_Raise_Exception_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Failed());
            SetupMessage(tokenContract, 0);
            var price = 5_00_000_000ul;
            var priceBytes = BitConverter.GetBytes(price);

            mSerializer.Setup(m => m.ToUInt64(priceBytes)).Returns(price);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, priceBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("OwnerOf method call failed.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Selling_Already_OnSale_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));
            var price = 5_00_000_000ul;
            var priceBytes = BitConverter.GetBytes(price);

            mSerializer.Setup(m => m.ToUInt64(priceBytes)).Returns(price);

            store.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, priceBytes);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, priceBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is already on sale.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Owner_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));
            var price = 5_00_000_000ul;
            var priceBytes = BitConverter.GetBytes(price);

            mSerializer.Setup(m => m.ToUInt64(priceBytes)).Returns(price);

            store.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, priceBytes)
                 .Should()
                 .BeTrue();

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new SaleInfo { Price = price, Seller = tokenOwner });

            VerifyLog(new TokenOnSaleLog
            {
                Contract = tokenContract,
                TokenId = tokenId,
                Seller = tokenOwner,
                Operator = tokenOwner,
                Price = price
            });

        }

        [Fact]
        public void OnNonFungibleTokenReceived_Operator_Mint_And_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));
            var price = 5_00_000_000ul;
            var priceBytes = BitConverter.GetBytes(price);

            mSerializer.Setup(m => m.ToUInt64(priceBytes)).Returns(price);


            store.OnNonFungibleTokenReceived(operatorAddress, Address.Zero, tokenId, priceBytes)
                 .Should()
                 .BeTrue();

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new SaleInfo { Price = price, Seller = operatorAddress });

            VerifyLog(new TokenOnSaleLog
            {
                Contract = tokenContract,
                TokenId = tokenId,
                Seller = operatorAddress,
                Operator = operatorAddress,
                Price = price
            });

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
            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, 100, TransferResult.Failed());

            SetupSupportsRoyaltyInfo(TransferResult.Succeed(true));
            SetupRoyaltyInfo(tokenId, saleInfo.Price, TransferResult.Succeed(new object[] { Address.Zero, 0UL }));

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

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, 100, TransferResult.Succeed());

            SetupSupportsRoyaltyInfo(TransferResult.Succeed(true));
            SetupRoyaltyInfo(tokenId, saleInfo.Price, TransferResult.Succeed(new object[] { Address.Zero, 0UL }));

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Buyer = buyer });

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));
        }

        [Fact]
        public void Buy_When_Seller_Is_A_Contract_Success()
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

            state.SetIsContract(tokenOwner, true);
            state.SetUInt64($"Balance:{tokenOwner}", 100);

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, 100, TransferResult.Succeed());

            SetupSupportsRoyaltyInfo(TransferResult.Failed());

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Buyer = buyer });

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));

            store.GetBalance(tokenOwner)
                 .Should()
                 .Be(200);//existing balance + current sale price
        }

        [Fact]
        public void Buy_Token_Buying_With_Royalty_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            var royaltyAmount = 11UL;
            var royaltyRecipient = Address.Zero;
            var salePriceMinusRoyalty = saleInfo.Price - royaltyAmount;

            SetSaleInfo(saleInfo);

            SetupMessage(buyer, 100);

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, salePriceMinusRoyalty, TransferResult.Succeed());
            SetupTransfer(royaltyRecipient, royaltyAmount, TransferResult.Succeed());

            SetupSupportsRoyaltyInfo(TransferResult.Succeed(true));
            SetupRoyaltyInfo(tokenId, saleInfo.Price, TransferResult.Succeed(new object[] { royaltyRecipient, royaltyAmount }));

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Buyer = buyer });
            VerifyLog(new RoyaltyPaidLog { Recipient = royaltyRecipient, Amount = royaltyAmount });

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));
        }

        [Fact]
        public void Buy_Token_Buying_With_Royalty_From_Cache_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTStore(mContractState.Object);

            var saleInfo = new SaleInfo
            {
                Price = 100,
                Seller = tokenOwner
            };

            var royaltyAmount = 11UL;
            var royaltyRecipient = Address.Zero;
            var salePriceMinusRoyalty = saleInfo.Price - royaltyAmount;

            SetSaleInfo(saleInfo);

            SetupMessage(buyer, 100);

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, salePriceMinusRoyalty, TransferResult.Succeed());
            SetupTransfer(royaltyRecipient, royaltyAmount, TransferResult.Succeed());


            SetupSupportsRoyaltyInfo(TransferResult.Failed());

            SetSupportsRoyaltyProperty(true);

            SetupRoyaltyInfo(tokenId, saleInfo.Price, TransferResult.Succeed(new object[] { royaltyRecipient, royaltyAmount }));

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Buyer = buyer });
            VerifyLog(new RoyaltyPaidLog { Recipient = royaltyRecipient, Amount = royaltyAmount });

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));
        }

        [Fact]
        public void Buy_Token_Buying_Without_Royalty_Support_Success()
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

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed());

            SetupTransfer(tokenOwner, saleInfo.Price, TransferResult.Succeed());

            SetupSupportsRoyaltyInfo(TransferResult.Failed());

            store.Buy(tokenContract, tokenId);

            VerifyLog(new TokenPurchasedLog { Contract = tokenContract, TokenId = tokenId, Buyer = buyer });

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

            SetupSafeTransferToken(contract, tokenOwner, tokenId, TransferResult.Failed());

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

            SetupSafeTransferToken(contract, tokenOwner, tokenId, TransferResult.Succeed());

            store.CancelSale(tokenContract, tokenId);

            store.GetSaleInfo(tokenContract, tokenId)
                 .Should()
                 .Be(default(SaleInfo));

            VerifyLog(new TokenSaleCanceledLog { Contract = tokenContract, TokenId = tokenId, Seller = tokenOwner });
        }

        private void SetSaleInfo(SaleInfo saleInfo)
        {
            state.SetStruct($"SaleInfo:{tokenContract}:{tokenId}", saleInfo);
        }

        private void SetSupportsRoyaltyProperty(bool value)
        {
            state.SetString($"SupportsRoyalty:{tokenContract}", value.ToString());
        }

        private void VerifyLog<T>(T expectedLog) where T : struct
        {
            mContractLogger.Verify(x => x.Log(mContractState.Object, expectedLog), Times.Once());
        }

        private void SetupTransferToken(Address from, Address to, UInt256 tokenId, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "TransferFrom", new object[] { from, to, tokenId }, 0))
                                .Returns(result);
        }

        private void SetupSafeTransferToken(Address from, Address to, UInt256 tokenId, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "SafeTransferFrom", new object[] { from, to, tokenId }, 0))
                                .Returns(result);
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

        private void SetupBlockNumber(ulong blockNumber)
        {
            mContractState.Setup(s => s.Block.Number).Returns(blockNumber);
        }

        private void SetupGetOwnerOfToken(TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "OwnerOf", new object[] { tokenId }, 0))
                                .Returns(result);
        }


        private void SetupIsApprovedForAll(Address owner, Address delegator, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "IsApprovedForAll", new object[] { owner, delegator }, 0))
                                .Returns(result);
        }

        private void SetupSupportsRoyaltyInfo(TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "SupportsInterface", new object[] { 6u }, 0))
                    .Returns(result);
        }

        private void SetupRoyaltyInfo(UInt256 tokenId, ulong salePrice, TransferResult result)
        {
            mTransactionExecutor.Setup(m => m.Call(mContractState.Object, tokenContract, 0, "RoyaltyInfo", new object[] { tokenId, salePrice }, 0))
                .Returns(result);
        }
    }
}
