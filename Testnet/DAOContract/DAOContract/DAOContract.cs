using Stratis.SmartContracts;
using System;

[Deploy]
public class DAOContract : SmartContract
{
    /// <summary>
    /// Min quorum for yes votes
    /// </summary>
    public uint MinQuorum
    {
        get => State.GetUInt32(nameof(MinQuorum));
        private set => State.SetUInt32(nameof(MinQuorum), value);
    }

    public uint LastProposalIndex
    {
        get => State.GetUInt32(nameof(MinQuorum));
        private set => State.SetUInt32(nameof(MinQuorum), value);
    }

    public bool IsWhitelisted(Address address) => State.GetBool($"Whitelisted:{address}");
    public void SetWhitelisted(Address address, bool allowed) => State.SetBool($"Whitelisted:{address}", allowed);

    public uint GetYesVotes(uint proposalId) => State.GetUInt32($"YesVotes:{proposalId}");
    private void SetYesVotes(uint proposalId, uint value) => State.SetUInt32($"YesVotes:{proposalId}", value);

    public uint GetNoVotes(uint proposalId) => State.GetUInt32($"NoVotes:{proposalId}");
    private void SetNoVotes(uint proposalId, uint value) => State.SetUInt32($"NoVotes:{proposalId}", value);

    public uint GetVote(uint proposalId, Address address) => State.GetUInt32($"Vote:{proposalId}:{address}");

    public Proposal GetProposal(uint index) => State.GetStruct<Proposal>($"Proposals:{index}");

    public void SetProposal(uint index, Proposal proposal) => State.SetStruct($"Proposals:{index}", proposal);

    public DAOContract(ISmartContractState state, uint minQuorum)
        : base(state)
    {
        this.MinQuorum = minQuorum;
    }

    public uint CreateProposal(Address recipent, ulong amount, uint debatingDuration, string description)
    {
        var proposal = new Proposal
        {
            Amount = amount,
            Description = description,
            Recipient = recipent,
            VotingDeadline = checked(debatingDuration + Block.Number),
            Creator = Message.Sender,
            Open = true
        };

        SetProposal(LastProposalIndex, proposal);

        Log(new ProposalAddedLog
        {
            ProposalId = LastProposalIndex,
            Recipent = recipent,
            Amount = amount,
            Description = description
        });

        return LastProposalIndex++;
    }

    public void Vote(uint proposalId, bool vote)
    {
        Assert(IsWhitelisted(Message.Sender));

        var proposal = GetProposal(proposalId);

        Assert(proposal.VotingDeadline > Block.Number, "Voting is closed.");

        SetVote(proposalId, Message.Sender, ToVote(vote));

    }

    private void SetVote(uint proposalId, Address address, Votes vote)
    {
        var currentVote = (Votes)GetVote(proposalId, address);

        if (currentVote == vote)
        {
            return;
        }

        Unvote(proposalId, currentVote);

        State.SetUInt32($"Vote:{proposalId}:{address}", (uint)vote);

        if (vote == Votes.Yes)
        {
            SetYesVotes(proposalId, GetYesVotes(proposalId) + 1);
        }
        else
        {
            SetNoVotes(proposalId, GetNoVotes(proposalId) + 1);
        }
    }

    private void Unvote(uint proposalId, Votes currentVote)
    {
        switch (currentVote)
        {
            case Votes.None: break;
            case Votes.Yes: SetYesVotes(proposalId, GetYesVotes(proposalId) - 1); break;
            case Votes.No: SetNoVotes(proposalId, GetNoVotes(proposalId) - 1); break;
        }
    }

    private Votes ToVote(bool vote) => vote ? Votes.Yes : Votes.No;

    public void ExecuteProposal(uint proposalId)
    {
        Assert(IsWhitelisted(Message.Sender));

        var proposal = GetProposal(proposalId);
        var yesVotes = GetYesVotes(proposalId);
        var noVotes = GetNoVotes(proposalId);

        Assert(yesVotes > noVotes, "The proposal voting is not passed.");
        Assert(yesVotes >= MinQuorum, "Min quorum for proposal is not reached.");

        Assert(proposal.Open, "The proposal is closed.");
        Assert(proposal.VotingDeadline < Block.Number, "Voting is still open for the proposal.");
        Assert(proposal.Amount < Balance, "Insufficient balance.");

        proposal.Open = false;

        SetProposal(proposalId, proposal);
        var result = Transfer(proposal.Recipient, proposal.Amount);

        Assert(result.Success, "Transfer failed.");

    }

    public void Deposit()
    {

    }
    public enum Votes : uint
    {
        None,
        No,
        Yes
    }

    struct ProposalAddedLog
    {
        public Address Recipent;
        public uint ProposalId;
        public ulong Amount;
        public string Description;
    }
}