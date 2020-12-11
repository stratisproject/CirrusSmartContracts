using System;
using Stratis.SmartContracts;

public class AddressMapper : SmartContract
{
    public Address GetSecondaryAddress(Address primary) => this.PersistentState.GetAddress($"SecondaryAddress:{primary}");
    private void SetSecondaryAddress(Address primary, Address secondary) => this.PersistentState.SetAddress($"SecondaryAddress:{primary}", secondary);

    private Status GetSecondaryStatus(Address secondary) => (Status)this.PersistentState.GetInt32($"SecondaryAddressStatus:{secondary}");
    private void SetSecondaryStatus(Address secondary, Status status) => this.PersistentState.SetInt32($"SecondaryAddressStatus:{secondary}", (Int32)status);

    public Address Owner
    {
        get => this.PersistentState.GetAddress(nameof(Owner));
        private set => this.PersistentState.SetAddress(nameof(Owner), value);
    }

    public AddressMapper(ISmartContractState smartContractState, Address Owner) : base(smartContractState)
    {
        this.Owner = Owner;
    }

    public bool MapAddress(Address secondary)
    {
        if (GetSecondaryStatus(secondary) != Status.NoStatus)
            return false;

        SetSecondaryAddress(Message.Sender, secondary);
        SetSecondaryStatus(secondary, Status.Pending);

        Log(new AddressMapPendingLog { Primary = Message.Sender, Secondary = secondary });

        return true;
    }

    public void SetApproval(Address secondary, bool approve)
    {
        EnsureAdminOnly();
        Assert(GetSecondaryStatus(secondary) == Status.Pending, "Mapping is not in pending state.");

        if (approve)
        {
            SetSecondaryStatus(secondary, Status.Approved);

            Log(new AddressMappedLog { Primary = Message.Sender, Secondary = secondary });

            return;
        }

        SetSecondaryStatus(secondary, Status.NoStatus); // same address can be mapped again. 
    }

    public void EnsureAdminOnly() => Assert(this.Owner == Message.Sender, "Only contract owner can access.");

    public enum Status
    {
        NoStatus,
        Pending,
        Approved,
    }

    public struct AddressMapPendingLog
    {
        [Index]
        public Address Primary;

        [Index]
        public Address Secondary;
    }

    public struct AddressMappedLog
    {
        [Index]
        public Address Primary;

        [Index]
        public Address Secondary;
    }
}