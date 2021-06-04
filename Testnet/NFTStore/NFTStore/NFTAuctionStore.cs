using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System;

[Deploy]
public class NFTAuctionStore : SmartContract
{
    private void SetAuctionInfo(Address contract, ulong tokenId, AuctionInfo auctionInfo)
    {
        State.SetStruct($"AuctionInfo:{contract}:{tokenId}", auctionInfo);
    }

    public AuctionInfo GetAuctionInfo(Address contract, ulong tokenId)
    {
        EnsureNotPayable();
        return State.GetStruct<AuctionInfo>($"AuctionInfo:{contract}:{tokenId}");
    }

    public ulong GetBalance(Address address)
    {
        EnsureNotPayable();
        return State.GetUInt64($"Balances:{address}");
    }

    private void SetBalance(Address address, ulong balance)
    {
        State.SetUInt64($"Balances:{address}", balance);
    }

    public NFTAuctionStore(ISmartContractState state)
        : base(state)
    {
        EnsureNotPayable();
    }

    public void Auction(Address contract, ulong tokenId, ulong startingPrice, ulong duration)
    {
        EnsureNotPayable();

        var tokenOwner = GetOwner(contract, tokenId);

        Assert(tokenOwner == Address, "The token is already on sale.");

        EnsureCallerCanOperate(contract, tokenOwner);

        TransferToken(contract, tokenId, tokenOwner, Address);

        var auction = new AuctionInfo
        {
            Seller = tokenOwner,
            EndBlock = checked(Block.Number + duration),
            StartingPrice = startingPrice,
            Ended = false
        };

        SetAuctionInfo(contract, tokenId, auction);
        Log(new AuctionStartedLog { EndBlock = auction.EndBlock, Seller = auction.Seller, startingPrice = auction.StartingPrice });
    }

    public void Bid(Address contract, ulong tokenId)
    {
        var auction = GetAuctionInfo(contract, tokenId);

        Assert(Block.Number < auction.EndBlock);
        Assert(Message.Value > auction.HighestBid && Message.Value >= auction.StartingPrice);

        if (auction.HighestBid > 0)
        {
            var balance = GetBalance(auction.HighestBidder);

            SetBalance(auction.HighestBidder, balance + auction.HighestBid);
        }

        auction.HighestBidder = Message.Sender;
        auction.HighestBid = Message.Value;

        SetAuctionInfo(contract, tokenId, auction);

        Log(new HighestBidUpdatedLog { Bidder = auction.HighestBidder, Bid = auction.HighestBid });
    }

    public bool Withdraw()
    {
        EnsureNotPayable();

        var amount = GetBalance(Message.Sender);

        Assert(amount > 0);

        SetBalance(Message.Sender, 0);

        var transfer = Transfer(this.Message.Sender, amount);

        if (!transfer.Success)
            SetBalance(Message.Sender, amount);

        return transfer.Success;
    }

    public void AuctionEnd(Address contract, ulong tokenId)
    {
        EnsureNotPayable();

        var auction = GetAuctionInfo(contract, tokenId);

        Assert(Block.Number >= auction.EndBlock);

        Assert(!auction.Ended);

        auction.Ended = true;
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

        Log(new AuctionEndedLog { Contract = contract, TokenId = tokenId, HighestBidder = auction.HighestBidder, HighestBid = auction.HighestBid });
    }

    private bool IsApprovedForAll(Address contract, Address tokenOwner)
    {
        var result = Call(contract, 0, "IsApprovedForAll", new object[] { tokenOwner, Message.Sender });

        Assert(result.Success, "IsApprovedForAll method call failed.");

        return result.ReturnValue is bool success && success;
    }

    private void TransferToken(Address contract, ulong tokenId, Address from, Address to)
    {
        var result = Call(contract, 0, "TransferFrom", new object[] { from, to, tokenId });

        Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed. Be sure sender is approved to transfer token.");
    }

    private void SafeTransferToken(Address contract, ulong tokenId, Address from, Address to)
    {
        var result = Call(contract, 0, "SafeTransferFrom", new object[] { from, to, tokenId });

        Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed. Be sure sender is approved to transfer token.");
    }

    private Address GetOwner(Address contract, ulong tokenId)
    {
        var result = Call(contract, 0, "GetOwner", new object[] { tokenId });

        Assert(result.Success && result.ReturnValue is Address, "GetOwner method call failed.");

        return (Address)result.ReturnValue;
    }

    private void EnsureCallerCanOperate(Address contract, Address tokenOwner)
    {
        Assert(Message.Sender == tokenOwner || IsApprovedForAll(contract, tokenOwner), "The caller is not owner of the token nor approved for all.");
    }

    private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

    public struct AuctionStartedLog
    {
        public ulong EndBlock;
        public ulong startingPrice;
        public Address Seller;
    }

    public struct HighestBidUpdatedLog
    {
        public Address Bidder;
        public ulong Bid;
    }
    public struct AuctionEndedLog
    {
        public Address Contract;
        public ulong TokenId;
        public Address HighestBidder;
        public ulong HighestBid;
    }

    public struct AuctionInfo
    {
        public Address Seller;
        public ulong HighestBid;
        public Address HighestBidder;
        public ulong EndBlock;
        public bool Ended;
        internal ulong StartingPrice;
    }
}