using System;
using Stratis.SmartContracts;

public class AddressMapper : SmartContract
{
    public Address GetSecondaryAddress(Address primary) => this.PersistentState.GetAddress($"SecondaryAddress:{primary}");
    private void SetSecondaryAddress(Address primary, Address secondary) => this.PersistentState.SetAddress($"SecondaryAddress:{primary}", secondary);

    public bool GetSecondaryAddressInUse(Address secondary) => this.PersistentState.GetBool($"SecondaryAddressInUse:{secondary}");
    private void SetSecondaryAddressInUse(Address secondary, bool inUse) => this.PersistentState.SetBool($"SecondaryAddressInUse:{secondary}", inUse);


    public bool SecondaryAddressInUse
    {
        get => this.PersistentState.GetBool(nameof(SecondaryAddressInUse));
        set => this.PersistentState.SetBool(nameof(SecondaryAddressInUse), value);
    }
    public AddressMapper(ISmartContractState smartContractState)
    : base(smartContractState)
    {

    }

    public bool MapAddress(Address secondary)
    {
        if (GetSecondaryAddressInUse(secondary))
            return false;

        SetSecondaryAddress(Message.Sender, secondary);
        SetSecondaryAddressInUse(secondary, true);

        Log(new AddressMappedLog { Primary = Message.Sender, Secondary = secondary });

        return true;
    }

    public struct AddressMappedLog
    {
        [Index]
        public Address Primary;
        
        [Index]
        public Address Secondary;
    }
}