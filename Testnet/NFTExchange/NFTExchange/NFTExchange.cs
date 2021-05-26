using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System;

[Deploy]
public class NFTExchange : SmartContract
{
    public ulong GetPrice(Address contract, ulong tokenId) => State.SetUInt64($"contract:{contract}:{tokenId}");
    public ulong SetPrice(Address contract, ulong tokenId, ulong price) => State.SetUInt64($"contract:{contract}:{tokenId}", price);

    public NFTExchange(ISmartContractState state)
        : base(state)
    {
    }


    public void Place(Address contract, ulong tokenId, ulong price)
    {
        var owner = GetOwner(contract, tokenId);

        Assert(Message.Sender == owner || IsApprovedForAll(contract, owner), "The caller is not owner of the token nor approved for all.");

        TransferToken(contract, tokenId, owner, Address);

        Assert(price > 0, "Price should be higher than zero.");

        SetPrice(contract, tokenId, price);
    }

    private bool IsApprovedForAll(Address contract, Address owner)
    {
        var result = Call(contract, 0, "IsApprovedForAll", new object[] { owner, Message.Sender });

        Assert(result.Success, "IsApprovedForAll method call failed.");

        return result.ReturnValue is bool success && success;

    }

    private void TransferToken(Address contract, ulong tokenId, Address from, Address to)
    {
        var result = Call(contract, 0, "TransferFrom", new object[] { from, to, tokenId });

        Assert(result.Success && result.ReturnValue is bool success && success, "The token transfer failed. Be sure the contract is approved to transfer token.");

    }

    private Address GetOwner(Address contract, ulong tokenId)
    {
        var result = Call(contract, 0, "GetOwner", new object[] { tokenId });

        Assert(result.Success && result.ReturnValue is Address, "GetOwner method call failed.");

        return (Address)result.ReturnValue;
    }
}