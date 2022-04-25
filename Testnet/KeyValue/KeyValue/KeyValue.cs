using System;
using Stratis.SmartContracts;

public class KeyValueContract : SmartContract
{
    public string Get(Address address, string key)
    {
        return State.GetString($"{address}:{key}");
    }

    public void Set(string key, string value)
    {
        Assert(key != null, "Key cannot be null");

        Assert(value != null, "Value cannot be null");

        Assert(key.Length <= this.MaxLength, "Key length cannot exceed maximum");

        Assert(value.Length <= this.MaxLength, "Value length cannot exceed maximum");

        State.SetString($"{Message.Sender}:{key}", value);
    }

    public UInt32 MaxLength
    {
        get => State.GetUInt32(nameof(this.MaxLength));
        private set => State.SetUInt32(nameof(this.MaxLength), value);
    }

    public KeyValueContract(ISmartContractState smartContractState, UInt32 maxLength) : base(smartContractState)
    {
        Assert(maxLength > 0, "Maximum length must be 1 or more");

        this.MaxLength = maxLength;
    }
}
