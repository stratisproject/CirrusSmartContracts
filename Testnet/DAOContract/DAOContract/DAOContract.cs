using Stratis.SmartContracts;
using System;

[Deploy]
public class DAOContract : SmartContract
{
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    public uint MinQuorum => WhitelistedCount / 2 + 1;

    public uint WhitelistedCount
    {
        get => State.GetUInt32(nameof(WhitelistedCount));
        private set => State.SetUInt32(nameof(WhitelistedCount), value);
    }
    public uint MinVotingDuration
    {
        get => State.GetUInt32(nameof(MinVotingDuration));
        private set => State.SetUInt32(nameof(MinVotingDuration), value);
    }

    public uint LastProposalId
    {
        get => State.GetUInt32(nameof(LastProposalId));
        private set => State.SetUInt32(nameof(LastProposalId), value);
    }

    public bool IsWhitelisted(Address address) => State.GetBool($"Whitelisted:{address}");
    private void SetIsWhitelisted(Address address, bool allowed) => State.SetBool($"Whitelisted:{address}", allowed);

    public ulong GetVotingDeadline(uint proposalId) => State.GetUInt64($"Deadline:{proposalId}");
    private void SetVotingDeadline(uint proposalId, ulong block) => State.SetUInt64($"Deadline:{proposalId}", block);

    public uint GetYesVotes(uint proposalId) => State.GetUInt32($"YesVotes:{proposalId}");
    private void SetYesVotes(uint proposalId, uint value) => State.SetUInt32($"YesVotes:{proposalId}", value);

    public uint GetNoVotes(uint proposalId) => State.GetUInt32($"NoVotes:{proposalId}");
    private void SetNoVotes(uint proposalId, uint value) => State.SetUInt32($"NoVotes:{proposalId}", value);

    public uint GetVote(uint proposalId, Address address) => State.GetUInt32($"Vote:{proposalId}:{address}");

    public void SetVote(uint proposalId, Address address, Votes vote)=> State.SetUInt32($"Vote:{proposalId}:{address}", (uint)vote);

    public Proposal GetProposal(uint index) => State.GetStruct<Proposal>($"Proposals:{index}");

    private void SetProposal(uint index, Proposal proposal) => State.SetStruct($"Proposals:{index}", proposal);

    public DAOContract(ISmartContractState state, uint minVotingDuration)
        : base(state)
    {
        Owner = Message.Sender;
        LastProposalId = 1;
        MinVotingDuration = minVotingDuration;
    }

    public uint CreateProposal(Address recipent, ulong amount, uint votingDuration, string description)
    {
        Assert(votingDuration > MinVotingDuration, $"Voting duration should be higher than {MinVotingDuration}.");

        var length = description?.Length ?? 0;
        Assert(length <= 200, "The description length can be up to 200 characters.");

        var proposal = new Proposal
        {
            RequestedAmount = amount,
            Description = description,
            Recipient = recipent,
            Owner = Message.Sender
        };

        var proposalId = LastProposalId;
        SetProposal(proposalId, proposal);
        SetVotingDeadline(proposalId, checked(votingDuration + Block.Number));
        Log(new ProposalAddedLog
        {
            ProposalId = proposalId,
            Recipent = recipent,
            Amount = amount,
            Description = description
        });

        LastProposalId = proposalId + 1;

        return proposalId;
    }

    public void Vote(uint proposalId, bool vote)
    {
        Assert(IsWhitelisted(Message.Sender), "The caller is not whitelisted.");

        Assert(GetVotingDeadline(proposalId) > Block.Number, "Voting is closed.");

        SetVoteInner(proposalId, Message.Sender, ToVote(vote));
    }

    private void SetVoteInner(uint proposalId, Address address, Votes vote)
    {
        var currentVote = (Votes)GetVote(proposalId, address);

        if (currentVote == vote)
        {
            return;
        }

        Unvote(proposalId, currentVote);
        SetVote(proposalId, address, vote);

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
            case Votes.Yes: SetYesVotes(proposalId, GetYesVotes(proposalId) - 1); break;
            case Votes.No: SetNoVotes(proposalId, GetNoVotes(proposalId) - 1); break;
            case Votes.None: break;
        }
    }

    private Votes ToVote(bool vote) => vote ? Votes.Yes : Votes.No;

    public void ExecuteProposal(uint proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Owner == Message.Sender, "The proposal can be executed by proposal creator.");

        var yesVotes = GetYesVotes(proposalId);
        var noVotes = GetNoVotes(proposalId);

        Assert(yesVotes > noVotes, "The proposal voting is not passed.");

        Assert(yesVotes >= MinQuorum, "Min quorum for proposal is not reached.");

        Assert(!proposal.Executed, "The proposal is already executed.");
        Assert(GetVotingDeadline(proposalId) < Block.Number, "Voting is still open for the proposal.");
        Assert(proposal.RequestedAmount <= Balance, "Insufficient balance.");

        proposal.Executed = true;
        SetProposal(proposalId, proposal);
        var result = Transfer(proposal.Recipient, proposal.RequestedAmount);

        Assert(result.Success, "Transfer failed.");

        Log(new ProposalExecutedLog { ProposalId = proposalId, Amount = proposal.RequestedAmount, Recipent = proposal.Recipient });

    }

    public void BlacklistAddress(Address address)
    {
        EnsureOwnerOnly();

        if (!IsWhitelisted(address))
            return;

        SetIsWhitelisted(address, false);
        WhitelistedCount--;

    }

    public void BlacklistAddresses(byte[] addresses)
    {
        EnsureOwnerOnly();
        foreach (var address in Serializer.ToArray<Address>(addresses))
        {
            if (!IsWhitelisted(address))
                continue;

            SetIsWhitelisted(address, false);
            WhitelistedCount--;
        }
    }

    public void WhitelistAddress(Address address)
    {
        EnsureOwnerOnly();

        if (IsWhitelisted(address))
            return;

        SetIsWhitelisted(address, true);

        WhitelistedCount++;
    }

    public void WhitelistAddresses(byte[] addresses)
    {
        EnsureOwnerOnly();
        foreach (var address in Serializer.ToArray<Address>(addresses))
        {
            if (IsWhitelisted(address))
                continue;

            SetIsWhitelisted(address, true);
            WhitelistedCount++;
        }
    }

    private void EnsureOwnerOnly() => Assert(this.Owner == Message.Sender, "The method is owner only.");

    /// <summary>
    /// Public method for allow deposits. 
    /// </summary>
    public void Deposit()
    {
        Log(new FundRaisedLog { Sender = Message.Sender, Amount = Message.Value });
    }

    public override void Receive() => Deposit();

    public void TransferOwnership(Address newOwner)
    {
        EnsureOwnerOnly();
        this.Owner = newOwner;
    }

    public void UpdateMinVotingDuration(uint minVotingDuration)
    {
        EnsureOwnerOnly();

        MinVotingDuration = minVotingDuration;
    }

    public enum Votes : uint
    {
        None,
        No,
        Yes
    }

    public struct ProposalAddedLog
    {
        public Address Recipent;
        public uint ProposalId;
        public ulong Amount;
        public string Description;
    }

    public struct ProposalExecutedLog
    {
        public Address Recipent;
        public uint ProposalId;
        public ulong Amount;
    }

    public struct FundRaisedLog
    {
        public Address Sender;
        public ulong Amount;
    }

    public struct Proposal
    {
        public ulong RequestedAmount;

        public Address Recipient;

        public string Description;

        /// <summary>
        /// True if proposal executed and the requested fund is transferred
        /// </summary>
        public bool Executed;

        public Address Owner;
    }
}