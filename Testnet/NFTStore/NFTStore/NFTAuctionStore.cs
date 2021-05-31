using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System;

[Deploy]
public class NFTAuctionStore : SmartContract
{
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    public SaleInfo GetSaleInfo(Address contract, ulong tokenId) => State.GetStruct<SaleInfo>($"SaleInfo:{contract}:{tokenId}");
    private void SetSaleInfo(Address contract, ulong tokenId, SaleInfo value) => State.SetStruct<SaleInfo>($"SaleInfo:{contract}:{tokenId}", value);

    public void ClearSaleInfo(Address contract, ulong tokenId) => State.Clear($"SaleInfo:{contract}:{tokenId}");

    public bool IsWhitelistedToken(Address address) => State.GetBool($"WhitelistedToken:{address}");
    private void SetIsWhitelistedToken(Address address, bool allowed) => State.SetBool($"WhitelistedToken:{address}", allowed);
    public NFTAuctionStore(ISmartContractState state)
        : base(state)
    {
        EnsureNotPayable();
        Owner = Message.Sender;
    }

    /// <summary>
    /// Ensure the token is approved on the non fungible contract for exchange contract as pre-condition.
    /// </summary>
    public void Sale(Address contract, ulong tokenId, ulong price)
    {
        EnsureNotPayable();

        Assert(price > 0, "Price should be higher than zero.");

        var tokenOwner = GetOwner(contract, tokenId);

        Assert(tokenOwner == Address, "The token is already on sale.");

        EnsureCallerCanOperate(contract, tokenOwner);

        TransferToken(contract, tokenId, tokenOwner, Address);

        SetSaleInfo(contract, tokenId, new SaleInfo { Price = price, Seller = tokenOwner });

        Log(new TokenOnSaleLog { Contract = contract, TokenId = tokenId, Price = price, Seller = tokenOwner });
    }

    public void Buy(Address contract, ulong tokenId)
    {
        var saleInfo = GetSaleInfo(contract, tokenId);

        Assert(Message.Value == saleInfo.Price, "Transferred amount is not matching exact price of the token.");

        SafeTransferToken(contract, tokenId, Address, Message.Sender);

        ClearSaleInfo(contract, tokenId);

        var result = Transfer(saleInfo.Seller, saleInfo.Price);

        Assert(result.Success, "Transfer failed.");

        Log(new TokenPurchasedLog { Contract = contract, TokenId = tokenId, Purchaser = Message.Sender, Seller = GetOwner(contract, tokenId) });
    }

    public void CancelSale(Address contract, ulong tokenId)
    {
        EnsureNotPayable();
        var saleInfo = GetSaleInfo(contract, tokenId);

        Assert(saleInfo.Seller != Address.Zero, "The token is not on sale");

        EnsureCallerCanOperate(contract, saleInfo.Seller);

        SafeTransferToken(contract, tokenId, Address, saleInfo.Seller);

        ClearSaleInfo(contract, tokenId);

        Log(new TokenSaleCanceledLog { Contract = contract, TokenId = tokenId });
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

    public void BlacklistToken(Address address)
    {
        EnsureNotPayable();
        EnsureOwnerOnly();

        if (State.IsContract(address))
        {
            SetIsWhitelistedToken(address, false);
        }
    }

    public void BlacklistTokens(byte[] addresses)
    {
        EnsureOwnerOnly();
        foreach (var address in Serializer.ToArray<Address>(addresses))
        {
            if (State.IsContract(address))
            {
                BlacklistToken(address);
            }
        }
    }

    public void WhitelistToken(Address address)
    {
        EnsureNotPayable();
        EnsureOwnerOnly();

        if (State.IsContract(address))
        {
            SetIsWhitelistedToken(address, true);
        }
    }

    public void WhitelistTokens(byte[] addresses)
    {
        EnsureNotPayable();
        EnsureOwnerOnly();
        foreach (var address in Serializer.ToArray<Address>(addresses))
        {
            if (State.IsContract(address))
            {
                WhitelistToken(address);
            }
        }
    }
    public void TransferOwnership(Address newOwner)
    {
        EnsureOwnerOnly();
        Owner = newOwner;
    }

    private void EnsureOwnerOnly() => Assert(this.Owner == Message.Sender, "The method is owner only.");
    private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

    #region Auction Logic

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
        return State.GetUInt64($"Balances[{address}]");
    }

    private void SetBalance(Address address, ulong balance)
    {
        State.SetUInt64($"Balances[{address}]", balance);
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

        var transferResult = Transfer(this.Message.Sender, amount);

        if (!transferResult.Success)
            SetBalance(this.Message.Sender, amount);

        return transferResult.Success;
    }

    public void AuctionEnd(Address contract, ulong tokenId)
    {
        EnsureNotPayable();

        var auction = GetAuctionInfo(contract, tokenId);

        Assert(Block.Number >= auction.EndBlock);
        
        Assert(!auction.Ended);

        auction.Ended = true;
        
        var result = Transfer(this.Owner, auction.HighestBid);
        
        Assert(result.Success, "Transfer failed.");

        SafeTransferToken(contract, tokenId, Address, auction.HighestBidder);

        Log(new AuctionEndedLog { Contract = contract, TokenId = tokenId, HighestBidder = auction.HighestBidder, HighestBid = auction.HighestBid });
    }

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
    #endregion

    private struct TokenOnSaleLog
    {
        public Address Contract;
        internal Address Seller;
        public ulong TokenId;
        public ulong Price;
    }

    public struct TokenPurchasedLog
    {
        public Address Contract;
        public ulong TokenId;
        public Address Purchaser;

        public Address Seller { get; set; }
    }

    public struct TokenSaleCanceledLog
    {
        public Address Contract;
        public ulong TokenId;
    }

    public struct SaleInfo
    {
        public ulong Price;
        public Address Seller;
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