using System;
using Stratis.SmartContracts;

public class AddressMapper : SmartContract
{
    public Address GetSecondaryAddress(Address primary) => State.GetAddress($"SecondaryAddress:{primary}");
    private void SetSecondaryAddress(Address primary, Address secondary) => State.SetAddress($"SecondaryAddress:{primary}", secondary);

    private MappingInfo GetMappingInfo(Address secondary) => State.GetStruct<MappingInfo>($"MappingInfo:{secondary}");
    private void SetMappingInfo(Address secondary, MappingInfo value) => State.SetStruct($"MappingInfo:{secondary}", value);

    private void ClearMappingInfo(Address secondary) => State.Clear($"MappingInfo:{secondary}");

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    public AddressMapper(ISmartContractState smartContractState, Address owner) : base(smartContractState)
    {
        this.Owner = owner;
    }

    public void MapAddress(Address secondary)
    {
        Assert(GetMappingInfo(secondary).Status == (int)Status.NoStatus);

        SetMappingInfo(secondary, new MappingInfo { Primary = Message.Sender, Status = (int)Status.Pending });
    }

    public void Approve(Address secondary)
    {
        EnsureAdminOnly();
        var mapping = GetMappingInfo(secondary);
        Assert(mapping.Status == (int)Status.Pending, "Mapping is not in pending state.");

        SetSecondaryAddress(mapping.Primary, secondary);
        SetMappingInfo(secondary, new MappingInfo { Primary = mapping.Primary, Status = (int)Status.Approved });

        Log(new AddressMappedLog { Primary = mapping.Primary, Secondary = secondary });
    }

    public void Reject(Address secondary)
    {
        EnsureAdminOnly();
        var mapping = GetMappingInfo(secondary);
        Assert(mapping.Status == (int)Status.Pending, "Mapping is not in pending state.");

        ClearMappingInfo(secondary); // same address can be mapped again. 
    }

    public string GetStatus(Address secondary) => GetMappingInfo(secondary).Status.ToString();
    public Address GetPrimaryAddress(Address secondary)
    {
        var mapping = GetMappingInfo(secondary);

        Assert(mapping.Status == (int)Status.Approved, "The mapping is not approved.");

        return mapping.Primary;
    }

    public void ChangeOwner(Address owner)
    {
        EnsureAdminOnly();

        Assert(owner != Address.Zero, $"The {nameof(owner)} parameter can not be default(zero) address.");

        this.Owner = owner;
    }

    public void EnsureAdminOnly() => Assert(this.Owner == Message.Sender, "Only contract owner can access.");

    public enum Status
    {
        NoStatus,
        Pending,
        Approved,
    }

    public struct MappingInfo
    {
        public Address Primary;
        public int Status;
    }

    public struct AddressMappedLog
    {
        [Index]
        public Address Primary;

        [Index]
        public Address Secondary;
    }
}