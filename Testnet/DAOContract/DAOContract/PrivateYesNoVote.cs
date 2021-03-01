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
        SetVotersExecute(addresses);
    }
    
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    public ulong VotePeriodEndBlock
    {
        get => State.GetUInt64(nameof(VotePeriodEndBlock));
        private set => State.SetUInt64(nameof(VotePeriodEndBlock), value);
    }

    public uint YesVotes
    {
        get => State.GetUInt32(nameof(YesVotes));
        private set => State.SetUInt32(nameof(YesVotes), value);
    }

    public uint NoVotes
    {
        get => State.GetUInt32(nameof(NoVotes));
        private set => State.SetUInt32(nameof(NoVotes), value);
    }

    private void SetVoterExecute(Address address)
    {
        State.SetBool($"Voter:{address}", true);
    }

    public bool IsVoter(Address address)
    {
        return State.GetBool($"Voter:{address}");
    }

    public char GetVote(Address address)
    {
        return State.GetChar($"Vote:{address}");
    }

    private void SetVote(Address address, char vote)
    {
        State.SetChar($"Vote:{address}", vote);
    }

    public void SetVoter(Address address)
    {
        EnsureOwnerOnly();
        SetVoterExecute(address);
    }

    public void SetVoters(byte[] addresses)
    {
        EnsureOwnerOnly();
        SetVotersExecute(addresses);
    }

    private void SetVotersExecute(byte[] addresses)
    {
        var addressList = Serializer.ToArray<Address>(addresses);

        foreach (var address in addressList)
        {
            SetVoterExecute(address);
        }
    }

    public void Vote(bool vote)
    {
        Assert(IsVoter(Message.Sender), "Sender is not authorized to vote.");
        Assert(GetVote(Message.Sender) == default(char), "Sender has already voted.");
        Assert(Block.Number <= VotePeriodEndBlock, "Voting period has ended.");

        char voteType;

        if (vote)
        {
            voteType = Yes;
            YesVotes++;
        }
        else
        {
            voteType = No;
            NoVotes++;
        }

        SetVote(Message.Sender, voteType);

        Log(new VoteEvent { Voter = Message.Sender, Vote = vote });
    }

    private void EnsureOwnerOnly()
    {
        Assert(Message.Sender == Owner, "Must be the contract owner to authorize voters.");
    }

    public struct VoteEvent
    {
        [Index]
        public Address Voter;
        public bool Vote;
    }
}