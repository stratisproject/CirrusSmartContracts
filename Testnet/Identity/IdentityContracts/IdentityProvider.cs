using Stratis.SmartContracts;

/// <summary>
/// A constrained version of ERC780.
/// This contract only holds claims from one identity provider, the "Owner".
/// </summary>
public class IdentityProvider : SmartContract
{
    private Address Owner
    {
        get
        {
            return this.PersistentState.GetAddress(nameof(Owner));
        }
        set
        {
            this.PersistentState.SetAddress(nameof(Owner), value);
        }
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

        byte[] oldData = this.GetClaim(issuedTo, topic);
        bool exists = oldData != null;

        // This exact claim value is stored already. Nothing to change.
        if (exists && ByteArrayCompare(oldData, data))
            return;

        this.SetClaim(issuedTo, topic, data);

        if (exists)
        {
            this.Log(new ClaimChanged
            {
                IssuedTo = issuedTo,
                Topic = topic,
                Data = data
            });
        }
        else
        {
            this.Log(new ClaimAdded
            {
                IssuedTo = issuedTo,
                Topic = topic,
                Data = data
            });
        }
    }

    public void RemoveClaim(Address issuedTo, uint topic)
    {
        Assert(this.Owner == Message.Sender);

        // Nothing to delete.
        byte[] oldData = this.GetClaim(issuedTo, topic);
        if (oldData == null)
        {
            return;
        }

        this.PersistentState.Clear($"Claim[{issuedTo}][{topic}]");

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

    #region Utils

    static bool ByteArrayCompare(byte[] a1, byte[] a2)
    {
        if (a1.Length != a2.Length)
            return false;

        for (int i = 0; i < a1.Length; i++)
            if (a1[i] != a2[i])
                return false;

        return true;
    }

    #endregion

    #region Events

    public struct ClaimAdded
    {
        [Index]
        public Address IssuedTo;
        [Index]
        public uint Topic;
        public byte[] Data;
    }

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