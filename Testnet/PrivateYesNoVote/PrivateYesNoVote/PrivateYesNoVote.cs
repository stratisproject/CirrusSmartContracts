using Stratis.SmartContracts;

public class PrivateYesNoVote : SmartContract
{
    public PrivateYesNoVote(ISmartContractState smartContractState, ulong duration, byte[] addressesBytes) 
        : base(smartContractState)
    {
        VotePeriodEndBlock = checked(Block.Number + duration);
        Owner = Message.Sender;
        WhiteListAddressesExecute(addressesBytes);
    }

    public Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        private set => PersistentState.SetAddress(nameof(Owner), value); 
    }

    public ulong VotePeriodEndBlock
    {
        get => PersistentState.GetUInt64(nameof(VotePeriodEndBlock));
        private set => PersistentState.SetUInt64(nameof(VotePeriodEndBlock), value);
    }

    public uint YesVotes
    {
        get => PersistentState.GetUInt32(nameof(YesVotes));
        private set => PersistentState.SetUInt32(nameof(YesVotes), value);
    }
    
    public uint NoVotes
    {
        get => PersistentState.GetUInt32(nameof(NoVotes));
        private set => PersistentState.SetUInt32(nameof(NoVotes), value);
    }

    private void SetAuthorization(Address address)
    {
        PersistentState.SetBool($"Voter:{address}", true);
    }

    public bool IsAuthorized(Address address)
    {
        return PersistentState.GetBool($"Voter:{address}");
    }

    public char GetVote(Address address)
    {
        return PersistentState.GetChar($"Vote:{address}");
    }

    public void WhitelistAddresses(byte[] addressBytes)
    {
        Assert(Message.Sender == Owner, "Must be contract owner to whitelist addresses.");
        WhiteListAddressesExecute(addressBytes);
    }

    private void WhiteListAddressesExecute(byte[] addressBytes)
    {
        var addresses = Serializer.ToArray<Address>(addressBytes);
        
        foreach (var address in addresses)
        {
            SetAuthorization(address);
        }
    }

    private void SetVote(Address address, bool vote)
    {
        char voteChar;
        
        if (vote)
        {
            voteChar = 'y';
            YesVotes++;
        }
        else
        {
            voteChar = 'n';
            NoVotes++;
        }
        
        PersistentState.SetChar($"Vote:{address}", voteChar);
    }

    public void Vote(bool vote)
    {
        Assert(IsAuthorized(Message.Sender), "Sender is not authorized to vote.");
        Assert(GetVote(Message.Sender) == default(char), "Sender has already voted.");
        Assert(Block.Number <= VotePeriodEndBlock, "Voting period has ended.");
        
        SetVote(Message.Sender, vote);
        
        Log(new VoteEvent { Voter = Message.Sender, Vote = vote });
    }

    public struct VoteEvent
    {
        [Index]
        public Address Voter;
        public bool Vote;
    }
}