using Stratis.SmartContracts;

/// <summary>
/// A constrained version of ERC780.
/// contract only holds claims from one identity provider, the "Owner".
/// </summary>
public class IdentityProvider : SmartContract
{
    private Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        set => State.SetAddress(nameof(Owner), value);
    }

    public IdentityProvider(ISmartContractState state) : base(state)
    {
        this.Owner = Message.Sender;
    }

    public void ChangeOwner(Address newOwner)
    {
        EnsureOwnerOnly();
        Owner = newOwner;
    }

    private void EnsureOwnerOnly()
    {
        Assert(Owner == Message.Sender,"The method can be called by only owner.");
    }

    public void AddClaim(Address issuedTo, uint topic, byte[] data)
    {
        EnsureOwnerOnly();

        byte[] oldData = GetClaim(issuedTo, topic);

        SetClaim(issuedTo, topic, data);

        Log(new ClaimChanged
        {
            IssuedTo = issuedTo,
            Topic = topic,
            Data = data,
            OldData = oldData
        });
    }

    public void RemoveClaim(Address issuedTo, uint topic)
    {
        EnsureOwnerOnly();

        // Nothing to delete.
        byte[] oldData = GetClaim(issuedTo, topic);

        if (oldData.Length == 0)
        {
            return;
        }

        ClearClaim(issuedTo, topic);

        Log(new ClaimRemoved
        {
            IssuedTo = issuedTo,
            Topic = topic,
            Data = oldData
        });
    }

    public byte[] GetClaim(Address issuedTo, uint topic)
    {
        return State.GetBytes($"Claim[{issuedTo}][{topic}]");
    }

    private void SetClaim(Address issuedTo, uint topic, byte[] data)
    {
        State.SetBytes($"Claim[{issuedTo}][{topic}]", data);
    }

    private void ClearClaim(Address issuedTo, uint topic)
    {
        State.Clear($"Claim[{issuedTo}][{topic}]");
    }
    #region Events

    public struct ClaimRemoved
    {
        [Index]
        public Address IssuedTo;
        [Index]
        public uint Topic;
        public byte[] Data;
    }

    public struct ClaimChanged
    {
        [Index]
        public Address IssuedTo;
        [Index]
        public uint Topic;
        public byte[] Data;
        public byte[] OldData;
    }

    #endregion
}