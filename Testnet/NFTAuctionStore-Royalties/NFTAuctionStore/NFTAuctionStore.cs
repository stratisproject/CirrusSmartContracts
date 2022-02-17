using Stratis.SmartContracts;
using System;

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

    public NFTAuctionStore(ISmartContractState state) : base(state)
    {
        EnsureNotPayable();
    }

    public void Bid(Address contract, UInt256 tokenId)
    {
        var auction = GetAuctionInfo(contract, tokenId);

        Assert(auction.OnAuction, "Auction is not found.");

        Assert(!EndBlockReached(auction), "Auction ended.");

        Assert(Message.Value > auction.HighestBid && Message.Value >= auction.StartingPrice, "The amount is not higher than highest bidder or starting price.");

        if (auction.HighestBid > 0)
        {
            //refund for previous bidder
            var balance = GetRefund(auction.HighestBidder) + auction.HighestBid;

            if (!State.IsContract(auction.HighestBidder))
            {
                var result = Transfer(auction.HighestBidder, balance);

                if (result.Success)
                    balance = 0;
            }

            SetRefund(auction.HighestBidder, balance);
        }

        auction.HighestBidder = Message.Sender;
        auction.HighestBid = Message.Value;

        SetAuctionInfo(contract, tokenId, auction);

        Log(new HighestBidUpdatedLog { Contract = contract, TokenId = tokenId, Bidder = auction.HighestBidder, Bid = auction.HighestBid });
    }

    private bool EndBlockReached(AuctionInfo auction)
    {
        return Block.Number >= auction.EndBlock;
    }

    public bool Refund()
    {
        EnsureNotPayable();

        var amount = GetRefund(Message.Sender);

        Assert(amount > 0);

        SetRefund(Message.Sender, 0);

        var transfer = Transfer(this.Message.Sender, amount);

        Assert(transfer.Success, "Transfer failed.");

        Log(new BalanceRefundedLog { To = Message.Sender, Amount = amount });

        return transfer.Success;
    }

    public void AuctionEnd(Address contract, UInt256 tokenId)
    {
        EnsureNotPayable();

        var auction = GetAuctionInfo(contract, tokenId);

        Assert(auction.OnAuction, "Auction is not found.");

        Assert(EndBlockReached(auction), "Auction is not ended yet.");

        auction.OnAuction = false;
        SetAuctionInfo(contract, tokenId, auction);

        if (auction.HighestBid == 0)
        {
            TransferToken(contract, tokenId, Address, auction.Seller);

            Log(new AuctionEndFailedLog { Contract = contract, TokenId = tokenId });
            return;
        }

        var supportsRoyaltyInfo = SupportsRoyaltyInfo(contract);

        var royaltyInfo = supportsRoyaltyInfo
            ? GetRoyaltyInfo(contract, tokenId, auction.HighestBid)
            : new object[] { Address.Zero, 0UL };

        var royaltyRecipient = (Address)royaltyInfo[0];
        var royaltyAmount = (ulong)royaltyInfo[1];

        var salePriceMinusRoyalty = supportsRoyaltyInfo ? auction.HighestBid - royaltyAmount : auction.HighestBid;

        if (State.IsContract(auction.Seller))
        {
            SetRefund(auction.Seller, salePriceMinusRoyalty);
        }
        else
        {
            var result = Transfer(auction.Seller, salePriceMinusRoyalty);

            Assert(result.Success, "Transfer failed.");
        }

        if (royaltyAmount > 0)
        {
            var royaltyTransfer = Transfer(royaltyRecipient, royaltyAmount);

            Assert(royaltyTransfer.Success, "Royalty transfer failed.");
            Log(new RoyaltyPaidLog { Recipient = royaltyRecipient, Amount = royaltyAmount });
        }

        TransferToken(contract, tokenId, Address, auction.HighestBidder);

        Log(new AuctionEndSucceedLog { Contract = contract, TokenId = tokenId, HighestBidder = auction.HighestBidder, HighestBid = auction.HighestBid });
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

        Assert(!auction.OnAuction, "The token is already on auction.");

        auction = new AuctionInfo
        {
            Seller = seller,
            EndBlock = checked(Block.Number + parameters.Duration),
            StartingPrice = parameters.StartingPrice,
            OnAuction = true
        };

        SetAuctionInfo(tokenContract, tokenId, auction);

        Log(new AuctionStartedLog { Contract = tokenContract, TokenId = tokenId, EndBlock = auction.EndBlock, Seller = auction.Seller, startingPrice = auction.StartingPrice });

        return true;
    }

    private void TransferToken(Address contract, UInt256 tokenId, Address from, Address to)
    {
        var result = Call(contract, 0, "TransferFrom", new object[] { from, to, tokenId });

        Assert(result.Success, "The token transfer failed.");
    }


    private Address GetOwner(Address contract, UInt256 tokenId)
    {
        var result = Call(contract, 0, "OwnerOf", new object[] { tokenId });

        Assert(result.Success && result.ReturnValue is Address, "GetOwner method call failed.");

        return (Address)result.ReturnValue;
    }

    private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

    private bool SupportsRoyaltyInfo(Address contract)
    {
        // 6 is IRoyaltyInfo
        var supportsInterfaceResult = Call(contract, 0, "SupportsInterface", new object[] { 6 });

        return supportsInterfaceResult.Success && ((bool)supportsInterfaceResult.ReturnValue);
    }

    private object[] GetRoyaltyInfo(Address contract, UInt256 tokenId, ulong salePrice)
    {
        var royaltyCall = Call(contract, 0, "RoyaltyInfo", new object[] { tokenId, salePrice });

        Assert(royaltyCall.Success, "Get royalty info failed.");

        return royaltyCall.ReturnValue as object[];
    }

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

    public struct AuctionEndSucceedLog
    {
        [Index]
        public Address Contract;
        [Index]
        public UInt256 TokenId;
        [Index]
        public Address HighestBidder;
        public ulong HighestBid;
    }

    public struct AuctionEndFailedLog
    {
        [Index]
        public Address Contract;
        [Index]
        public UInt256 TokenId;
    }

    public struct RoyaltyPaidLog
    {
        [Index]
        public Address Recipient;
        public ulong Amount;
    }

    public struct BalanceRefundedLog
    {
        [Index]
        public Address To;
        public ulong Amount;
    }

    public struct AuctionInfo
    {
        public Address Seller;
        public ulong HighestBid;
        public Address HighestBidder;
        public ulong EndBlock;
        public ulong StartingPrice;
        public bool OnAuction;
    }
    public struct AuctionParam
    {
        public ulong StartingPrice;
        public ulong Duration;
    }
}