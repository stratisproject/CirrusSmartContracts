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
        private readonly ulong duration;

        public NFTAuctionStoreTests()
        {
            this.inMemoryState = new InMemoryState();
            this.mContractState = new Mock<ISmartContractState>();
            this.mContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mContractState.Setup(s => s.PersistentState).Returns(inMemoryState);
            this.mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            this.mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            this.creator = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.tokenOwner = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.attacker = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.operatorAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.buyer = "0x0000000000000000000000000000000000000006".HexToAddress();
            this.tokenId = 3;
            this.duration = 100;
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
        public void Auction_Sending_Coins_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenOwner, 10);

            store.Invoking(s => s.Auction(tokenContract, tokenId, 100, duration))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The method is not payable.");
        }

        [Fact]
        public void Auction_GetOwner_Fails()
        {
            SetupMessage(creator, 0);

            SetupGetOwnerOfToken(TransferResult.Failed());

            var store = new NFTAuctionStore(mContractState.Object);

            store.Invoking(s => s.Auction(tokenContract, tokenId, 100, duration))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("GetOwner method call failed.");
        }

        [Fact]
        public void Auction_Selling_Already_On_Auction_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupMessage(tokenOwner, 0);
            SetupGetOwnerOfToken(TransferResult.Succeed(contract));

            store.Invoking(s => s.Auction(tokenContract, tokenId, 100, duration))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token is already on sale.");
        }

        [Fact]
        public void Auction_Called_By_None_Owner_Or_Operator_Of_Token_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(attacker, 0);

            SetupIsApprovedForAll(tokenOwner, attacker, TransferResult.Succeed(false));

            store.Invoking(s => s.Auction(tokenContract, tokenId, 100, duration))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The caller is not owner of the token nor approved for all.");
        }

        [Fact]
        public void Auction_None_Approved_Token_For_Contract_Fails()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, TransferResult.Succeed(false));

            store.Invoking(s => s.Auction(tokenContract, tokenId, 100, duration))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("The token transfer failed. Be sure sender is approved to transfer token.");

        }

        [Fact]
        public void Auction_Owner_Sell_Token_Success()
        {
            SetupMessage(creator, 0);

            var store = new NFTAuctionStore(mContractState.Object);

            SetupGetOwnerOfToken(TransferResult.Succeed(tokenOwner));

            SetupMessage(tokenOwner, 0);

            SetupTransferToken(tokenOwner, contract, tokenId, TransferResult.Succeed(true));

            mContractState.Setup(m => m.Block.Number).Returns(1);

            store.Auction(tokenContract, tokenId, 100, duration);

            var auctionInfo = store.GetAuctionInfo(tokenContract, tokenId);

            auctionInfo.Should().Be(new AuctionInfo { Seller = tokenOwner, EndBlock = 101, StartingPrice = 100 });

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
                 .WithMessage("Auction closed.");
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
            SetAuctionInfo(new AuctionInfo { Seller = tokenOwner, EndBlock = 100, StartingPrice = 100, Ended = true });

            store.Invoking(m => m.AuctionEnd(tokenContract, tokenId))
                 .Should()
                 .Throw<SmartContractAssertException>()
                 .WithMessage("Auction end already executed.");
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
                    Ended = true,
                    EndBlock = 100, 
                    StartingPrice = 100,
                 });

            VerifyLog(new AuctionEndedLog { Contract = tokenContract, TokenId = tokenId, HighestBid = 0, HighestBidder = Address.Zero });
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
                Ended = false,
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
                     Ended = true,
                     EndBlock = 100,
                     StartingPrice = 100,
                 });


            VerifyLog(new AuctionEndedLog { Contract = tokenContract, TokenId = tokenId, HighestBid = 100, HighestBidder = buyer });
        }

        private void SetupBlock(ulong block)
        {
            mContractState.Setup(m => m.Block.Number).Returns(block);
        }

        private void SetAuctionInfo(AuctionInfo auctionInfo)
        {
            inMemoryState.SetStruct($"AuctionInfo:{tokenContract}:{tokenId}", auctionInfo);
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
