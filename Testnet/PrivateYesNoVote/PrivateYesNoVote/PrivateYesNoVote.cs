using Stratis.SmartContracts;

public class PrivateYesNoVote : SmartContract
{
    private const char Yes = 'y';
    private const char No = 'n';
    
    public PrivateYesNoVote(ISmartContractState smartContractState, ulong duration, byte[] addresses) 
        : base(smartContractState)
    {
        VotePeriodEndBlock = checked(Block.Number + duration);
        Owner = Message.Sender;
        AuthorizeVotersExecute(addresses);
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

    private void AuthorizeVoterExecute(Address address)
    {
        PersistentState.SetBool($"Voter:{address}", true);
    }

    public bool IsVoter(Address address)
    {
        return PersistentState.GetBool($"Voter:{address}");
    }

    public char GetVote(Address address)
    {
        return PersistentState.GetChar($"Vote:{address}");
    }
    
    private void SetVote(Address address, char vote)
    {
        PersistentState.SetChar($"Vote:{address}", vote);
    }
    
    public void AuthorizeVoter(Address address)
    {
        AuthorizeOwner();
        AuthorizeVoterExecute(address);
    }
    
    public void AuthorizeVoters(byte[] addresses)
    {
        AuthorizeOwner();
        AuthorizeVotersExecute(addresses);
    }

    private void AuthorizeVotersExecute(byte[] addresses)
    {
        var addressList = Serializer.ToArray<Address>(addresses);
        
        foreach (var address in addressList)
        {
            AuthorizeVoterExecute(address);
        }
    }
    
    public void Vote(bool vote)
    {
        Assert(IsVoter(Message.Sender), "Sender is not authorized to vote.");
        Assert(GetVote(Message.Sender) == default(char), "Sender has already voted.");
        Assert(Block.Number <= VotePeriodEndBlock, "Voting period has ended.");
        
        char voteChar;
        
        if (vote)
        {
            voteChar = Yes;
            YesVotes++;
        }
        else
        {
            voteChar = No;
            NoVotes++;
        }
        
        SetVote(Message.Sender, voteChar);
        
        Log(new VoteEvent { Voter = Message.Sender, Vote = vote });
    }

    private void AuthorizeOwner()
    {
        Assert(Message.Sender == Owner, "Must be contract owner to authorize addresses.");
    }
    
    public struct VoteEvent
    {
        [Index]
        public Address Voter;
        public bool Vote;
    }
}