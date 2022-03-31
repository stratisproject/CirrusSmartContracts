using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using System;
using Xunit;
using static NFTAuctionStore;

namespace NFTAuctionStoreTests
{
    public class NFTAuctionStoreTests
    {
        private readonly InMemoryState state;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<ISerializer> mSerializer;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address creator;
        private readonly Address tokenOwner;
        private readonly Address contract;
        private readonly Address tokenContract;
        private readonly Address attacker;
        private readonly Address operatorAddress;
        private readonly Address buyer;
        private readonly ulong tokenId;
        private readonly ulong duration;

        public NFTAuctionStoreTests()
        {
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mContractLogger = new Mock<IContractLogger>();
            mSerializer = new Mock<ISerializer>();
            mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
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
            duration = 100;

            mContractState.Setup(s => s.Message.ContractAddress).Returns(contract);
            state.SetIsContract(tokenContract, true);
        }
        [Fact]
        public void Constructor_Sending_Coins_Fails()
        {
            SetupMessage(creator, 10);

            Action action = () => new NFTAuctionStore(mContractState.Object);

            action.Invoking(c => c())
                  .Should()
                  .Throw<SmartContractAssertException>()
                  .WithMessage("The method is not payable.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenContract, 10);

            var parameters = new AuctionParam { StartingPrice = 100, Duration = duration };
            
            var paramBytes = Array.Empty<byte>();

            mSerializer.Setup(s => s.ToStruct<AuctionParam>(paramBytes)).Returns(parameters);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Called_By_None_Contract_Fails()
        {
            SetupMessage(creator, 0);

            state.SetIsContract(tokenContract, false);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenContract, 0);

            var parameters = new AuctionParam { StartingPrice = 100, Duration = duration };

            var paramBytes = Array.Empty<byte>();

            mSerializer.Setup(s => s.ToStruct<AuctionParam>(paramBytes)).Returns(parameters);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The Caller is not a contract.");
        }


        [Fact]
        public void OnNonFungibleTokenReceived_GetOwner_Call_Raise_Exception_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Failed());
            SetupMessage(tokenContract, 0);

            var parameters = new AuctionParam { StartingPrice = 100, Duration = duration };

            var paramBytes = Array.Empty<byte>();

            mSerializer.Setup(s => s.ToStruct<AuctionParam>(paramBytes)).Returns(parameters);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("GetOwner method call failed.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Selling_Already_OnSale_Token_Fails()
        {
            SetupMessage(creator, 0);
            SetupBlock(1);
            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));
            
            var parameters = new AuctionParam { StartingPrice = 100, Duration = duration };

            var paramBytes = Array.Empty<byte>();

            mSerializer.Setup(s => s.ToStruct<AuctionParam>(paramBytes)).Returns(parameters);

            store.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes);

            store.Invoking(s => s.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is already on sale.");
        }

        [Fact]
        public void OnNonFungibleTokenReceived_Owner_Sell_Token_Success()
        {
            SetupMessage(creator, 0);
            SetupBlock(1);
            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenContract, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));
            
            var parameters = new AuctionParam { StartingPrice = 100, Duration = duration };

            var paramBytes = Array.Empty<byte>();

            mSerializer.Setup(s => s.ToStruct<AuctionParam>(paramBytes)).Returns(parameters);

            store.OnNonFungibleTokenReceived(tokenOwner, tokenOwner, tokenId, paramBytes)
                 .Should()
                 .BeTrue();

            store.GetAuctionInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new AuctionInfo { Seller = tokenOwner, EndBlock = 101, StartingPrice = 100 });

            VerifyLog(new AuctionStartedLog
            {
                Contract = tokenContract,
                TokenId = tokenId,
                startingPrice = 100,
                Seller = tokenOwner,
                EndBlock = 101
            });

        }

        [Fact]
        public void Bid_Bidding_Called_When_Auction_Ended_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 101, StartingPrice = 100 });

            SetupBlock(101);

            store.Invoking(m => m.Bid(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Auction ended.");
        }

        [Fact]
        public void Bid_Amount_Is_Lower_Than_HighestBid_Or_StartingPrice_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 2, StartingPrice = 100 });

            SetupMessage(buyer, 99);
            SetupBlock(1);

            store.Invoking(m => m.Bid(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The amount is not higher than highest bidder or starting price.");
        }

        [Fact]
        public void Bid_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 101, StartingPrice = 100 });

            SetupBlock(100);
            SetupMessage(buyer, 100);
            store.Bid(tokenContract, tokenId);

            store.GetAuctionInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new AuctionInfo
                 {
                     HighestBid = 100,
                     HighestBidder = buyer,
                     Seller = tokenOwner,
                     EndBlock = 101,
                     StartingPrice = 100
                 });

            VerifyLog(new HighestBidUpdatedLog
            {
                Contract = tokenContract,
                TokenId = tokenId,
                Bid = 100,
                Bidder = buyer
            });

        }

        [Fact]
        public void Bid_Owner_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100 });

            SetupBlock(101);

            store.Invoking(m => m.Bid(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Auction ended.");
        }

        [Fact]
        public void AuctionEnd_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenOwner, 10);
            store.Invoking(m => m.AuctionEnd(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void AuctionEnd_Called_When_Auction_Is_Open_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupBlock(10);
            SetupMessage(tokenOwner, 0);
            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100 });

            store.Invoking(m => m.AuctionEnd(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Auction is not ended yet.");
        }

        [Fact]
        public void AuctionEnd_Already_Executed_AuctionEnd_Calls_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupBlock(101);
            SetupMessage(tokenOwner, 0);
            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100, OnAuction = true });

            store.Invoking(m => m.AuctionEnd(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Auction end already OnAuction.");
        }

        [Fact]
        public void AuctionEnd_Refund_Token_Return_False_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupBlock(101);

            SetupMessage(tokenOwner, 0);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100 });

            SetupSafeTransferToken(contract, tokenOwner, tokenId, TransferResult.Succeed(false));

            store.Invoking(m => m.AuctionEnd(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token transfer failed.");
        }


        [Fact]
        public void AuctionEnd_Transfer_Token_To_Owner_When_Auction_Ended_Without_Bid_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupBlock(101);

            SetupMessage(tokenOwner, 0);

            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100 });

            SetupSafeTransferToken(contract, tokenOwner, tokenId, TransferResult.Succeed(true));

            store.AuctionEnd(tokenContract, tokenId);

            store.GetAuctionInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new AuctionInfo {
                    Seller = tokenOwner,
                    OnAuction = true,
                    EndBlock = 100, 
                    StartingPrice = 100,
                 });

            VerifyLog(new AuctionEndSucceedLog { Contract = tokenContract, TokenId = tokenId, HighestBid = 0, HighestBidder = Address.Zero });
        }

        [Fact]
        public void AuctionEnd_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupBlock(101);

            SetupMessage(tokenOwner, 0);

            SetAuctionInfo(new AuctionInfo
            {
                Seller = tokenOwner,
                HighestBid = 100,
                HighestBidder = buyer,
                OnAuction = false,
                EndBlock = 100,
                StartingPrice = 100
            });

            SetupSafeTransferToken(contract, buyer, tokenId, TransferResult.Succeed(true));

            SetupTransfer(tokenOwner, 100, TransferResult.Succeed());

            store.AuctionEnd(tokenContract, tokenId);

            store.GetAuctionInfo(tokenContract, tokenId)
                 .Should()
                 .Be(new AuctionInfo
                 {
                     Seller = tokenOwner,
                     HighestBid = 100,
                     HighestBidder = buyer,
                     OnAuction = true,
                     EndBlock = 100,
                     StartingPrice = 100,
                 });


            VerifyLog(new AuctionEndSucceedLog { Contract = tokenContract, TokenId = tokenId, HighestBid = 100, HighestBidder = buyer });
        }

        private void SetupBlock(ulong block)
        {
            mContractState.Setup(m => m.Block.Number).Returns(block);
        }

        private void SetAuctionInfo(AuctionInfo auctionInfo)
        {
            state.SetStruct($"AuctionInfo:{tokenContract}:{tokenId}", auctionInfo);
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

        private void SetupSafeTransferToken(Address from, Address to, ulong tokenId, TransferResult result)
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
