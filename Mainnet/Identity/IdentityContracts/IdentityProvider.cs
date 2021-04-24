using Stratis.SmartContracts;

/// <summary>
/// A constrained version of ERC780.
/// This contract only holds claims from one identity provider, the "Owner".
/// </summary>
public class IdentityProvider : SmartContract
{
    private Address Owner
    {
        get => this.PersistentState.GetAddress(nameof(Owner));
        set => this.PersistentState.SetAddress(nameof(Owner), value);
    }

    public IdentityProvider(ISmartContractState state) : base(state)
    {
        this.Owner = Message.Sender;
    }

    public void ChangeOwner(Address newOwner)
    {
        Assert(this.Owner == Message.Sender);
        this.Owner = newOwner;
    }

    public void AddClaim(Address issuedTo, uint topic, byte[] data)
    {
        Assert(this.Owner == Message.Sender);

        this.SetClaim(issuedTo, topic, data);

        this.Log(new ClaimChanged
        {
            IssuedTo = issuedTo,
            Topic = topic,
            Data = data
        });
    }

    public void RemoveClaim(Address issuedTo, uint topic)
    {
        Assert(this.Owner == Message.Sender);

        // Nothing to delete.
        byte[] oldData = this.GetClaim(issuedTo, topic);

        if (oldData.Length == 0)
        {
            return;
        }

        ClearClaim(issuedTo, topic);

        this.Log(new ClaimRemoved
        {
            IssuedTo = issuedTo,
            Topic = topic,
            Data = oldData
        });
    }



    public byte[] GetClaim(Address issuedTo, uint topic)
    {
        return this.PersistentState.GetBytes($"Claim[{issuedTo}][{topic}]");
    }

    private void SetClaim(Address issuedTo, uint topic, byte[] data)
    {
        this.PersistentState.SetBytes($"Claim[{issuedTo}][{topic}]", data);
    }

    private void ClearClaim(Address issuedTo, uint topic)
    {
        this.PersistentState.Clear($"Claim[{issuedTo}][{topic}]");
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
    }


    #endregion
}