using Stratis.SmartContracts;

[Deploy]
public class NFTAuctionStore : SmartContract //,INonFungibleTokenReceiver
{
    private void SetAuctionInfo(Address contract, UInt256 tokenId, AuctionInfo auctionInfo)
    {
        State.SetStruct($"AuctionInfo:{contract}:{tokenId}", auctionInfo);
    }

    public AuctionInfo GetAuctionInfo(Address contract, UInt256 tokenId)
    {
        return State.GetStruct<AuctionInfo>($"AuctionInfo:{contract}:{tokenId}");
    }

    public ulong GetRefund(Address address)
    {
        return State.GetUInt64($"Refund:{address}");
    }

    private void SetRefund(Address address, ulong balance)
    {
        State.SetUInt64($"Refund:{address}", balance);
    }

    public NFTAuctionStore(ISmartContractState state)
        : base(state)
    {
        EnsureNotPayable();
    }

    public void Bid(Address contract, UInt256 tokenId)
    {
        var auction = GetAuctionInfo(contract, tokenId);

        Assert(Block.Number < auction.EndBlock, "Auction ended.");

        Assert(Message.Value > auction.HighestBid && Message.Value >= auction.StartingPrice, "The amount is not higher than highest bidder or starting price.");

        if (auction.HighestBid > 0)
        {
            //refund for previous bidder
            var balance = GetRefund(auction.HighestBidder);

            SetRefund(auction.HighestBidder, balance + auction.HighestBid);
        }

        auction.HighestBidder = Message.Sender;
        auction.HighestBid = Message.Value;

        SetAuctionInfo(contract, tokenId, auction);

        Log(new HighestBidUpdatedLog { Contract = contract, TokenId = tokenId, Bidder = auction.HighestBidder, Bid = auction.HighestBid });
    }

    public bool Refund()
    {
        EnsureNotPayable();

        var amount = GetRefund(Message.Sender);

        Assert(amount > 0);

        SetRefund(Message.Sender, 0);

        var transfer = Transfer(this.Message.Sender, amount);

        if (!transfer.Success)
            SetRefund(Message.Sender, amount);

        return transfer.Success;
    }

    public void AuctionEnd(Address contract, UInt256 tokenId)
    {
        EnsureNotPayable();

        var auction = GetAuctionInfo(contract, tokenId);

        Assert(Block.Number >= auction.EndBlock, "Auction is not ended yet.");

        Assert(!auction.Executed, "Auction end already executed.");

        auction.Executed = true;
        SetAuctionInfo(contract, tokenId, auction);

        if (auction.HighestBid > 0)
        {
            var result = Transfer(auction.Seller, auction.HighestBid);

            Assert(result.Success, "Transfer failed.");

            SafeTransferToken(contract, tokenId, Address, auction.HighestBidder);
        }
        else
        {
            SafeTransferToken(contract, tokenId, Address, auction.Seller);
        }

        Log(new AuctionEndedLog { Contract = contract, TokenId = tokenId, Seller = auction.Seller, HighestBidder = auction.HighestBidder, HighestBid = auction.HighestBid });
    }

    public bool OnNonFungibleTokenReceived(Address operatorAddress, Address fromAddress, UInt256 tokenId, byte[] data)
    {
        EnsureNotPayable();
        
        var tokenContract = Message.Sender;
        
        Assert(State.IsContract(tokenContract), "The Caller is not a contract.");

        var seller = fromAddress == Address.Zero ? operatorAddress : fromAddress;

        var parameters = Serializer.ToStruct<AuctionParam>(data);

        Assert(parameters.StartingPrice > 0, "Price should be higher than zero.");

        Assert(Address == GetOwner(tokenContract, tokenId), "The store contract is not owner of the token.");

        var auction = GetAuctionInfo(tokenContract, tokenId);

        Assert(auction.StartingPrice == 0, "The token is already on sale.");

        auction = new AuctionInfo
        {
            Seller = seller,
            EndBlock = checked(Block.Number + parameters.Duration),
            StartingPrice = parameters.StartingPrice
        };

        SetAuctionInfo(tokenContract, tokenId, auction);

        Log(new AuctionStartedLog { Contract = tokenContract, TokenId = tokenId, EndBlock = auction.EndBlock, Seller = auction.Seller, startingPrice = auction.StartingPrice });

        return true;
    }

    private void SafeTransferToken(Address contract, UInt256 tokenId, Address from, Address to)
    {
        var result = Call(contract, 0, "SafeTransferFrom", new object[] { from, to, tokenId });

        Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed.");
    }

    private Address GetOwner(Address contract, UInt256 tokenId)
    {
        var result = Call(contract, 0, "OwnerOf", new object[] { tokenId });

        Assert(result.Success && result.ReturnValue is Address, "GetOwner method call failed.");

        return (Address)result.ReturnValue;
    }

    private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

    public struct AuctionStartedLog
    {
        [Index]
        public Address Contract;
        [Index]
        public UInt256 TokenId;
        public ulong EndBlock;
        public ulong startingPrice;
        [Index]
        public Address Seller;
    }

    public struct HighestBidUpdatedLog
    {
        [Index]
        public Address Contract;
        [Index]
        public UInt256 TokenId;
        [Index]
        public Address Bidder;
        public ulong Bid;
    }
    public struct AuctionEndedLog
    {
        [Index]
        public Address Contract;
        [Index]
        public UInt256 TokenId;
        [Index]
        public Address HighestBidder;
        public ulong HighestBid;
    }

    public struct AuctionInfo
    {
        public Address Seller;
        public ulong HighestBid;
        public Address HighestBidder;
        public ulong EndBlock;
        public bool Executed;
        public ulong StartingPrice;
    }
    public struct AuctionParam
    {
        public ulong StartingPrice;
        public ulong Duration;
    }
}