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

    public uint MaxVotingDuration
    {
        get => State.GetUInt32(nameof(MaxVotingDuration));
        private set => State.SetUInt32(nameof(MaxVotingDuration), value);
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
    private void SetVote(uint proposalId, Address address, Votes vote) => State.SetUInt32($"Vote:{proposalId}:{address}", (uint)vote);

    public Proposal GetProposal(uint index) => State.GetStruct<Proposal>($"Proposals:{index}");

    private void SetProposal(uint index, Proposal proposal) => State.SetStruct($"Proposals:{index}", proposal);

    public const uint DefaultMaxDuration = 60u * 60 * 24 * 7 / 16; // 1 week period as second/ block duration as second
    public DAOContract(ISmartContractState state, uint minVotingDuration)
        : base(state)
    {
        Assert(DefaultMaxDuration > minVotingDuration, $"MinVotingDuration should be lower than maxVotingDuration({DefaultMaxDuration})");

        Owner = Message.Sender;
        LastProposalId = 1;
        MinVotingDuration = minVotingDuration;
        MaxVotingDuration = DefaultMaxDuration;
    }

    public uint CreateProposal(Address recipent, ulong amount, uint votingDuration, string description)
    {
        EnsureNotPayable();

        Assert(votingDuration > MinVotingDuration && votingDuration < MaxVotingDuration, $"Voting duration should be between {MinVotingDuration} and {MaxVotingDuration}.");

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
        EnsureNotPayable();
        Assert(IsWhitelisted(Message.Sender), "The caller is not whitelisted.");

        Assert(GetVotingDeadline(proposalId) > Block.Number, "Voting is closed.");

        VoteProposal(proposalId, Message.Sender, ToVote(vote));
    }

    private void VoteProposal(uint proposalId, Address address, Votes vote)
    {
        var currentVote = (Votes)GetVote(proposalId, address);

        if (currentVote == vote)
        {
            return;
        }

        Unvote(proposalId, currentVote);
        SetVote(proposalId, address, vote);

        Log(new ProposalVotedLog { ProposalId = proposalId, Vote = vote == Votes.Yes });

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
        EnsureNotPayable();

        var proposal = GetProposal(proposalId);

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

    public void Deposit()
    {
        EnsureOwnerOnly();
        Log(new FundRaisedLog { Sender = Message.Sender, Amount = Message.Value });
    }

    public override void Receive() => Deposit();

    public void TransferOwnership(Address newOwner)
    {
        EnsureOwnerOnly();
        Owner = newOwner;
    }
    
    public void UpdateMinVotingDuration(uint minVotingDuration)
    {
        EnsureOwnerOnly();

        Assert(minVotingDuration < MaxVotingDuration, "MinVotingDuration should be lower than MaxVotingDuration.");

        MinVotingDuration = minVotingDuration;
    }

    public void UpdateMaxVotingDuration(uint maxVotingDuration)
    {
        EnsureOwnerOnly();
        Assert(maxVotingDuration > MinVotingDuration, "MaxVotingDuration should be higher than MinVotingDuration.");

        MaxVotingDuration = maxVotingDuration;
    }

    private void EnsureOwnerOnly() => Assert(this.Owner == Message.Sender, "The method is owner only.");
    private void EnsureNotPayable() => Assert(Message.Value == 0, "The method is not payable.");

    public enum Votes : uint
    {
        None,
        No,
        Yes
    }

    public struct ProposalAddedLog
    {
        [Index]
        public Address Recipent;
        public uint ProposalId;
        public ulong Amount;
        public string Description;
    }

    public struct ProposalExecutedLog
    {
        [Index]
        public Address Recipent;
        public uint ProposalId;
        public ulong Amount;
    }

    public struct FundRaisedLog
    {
        [Index]
        public Address Sender;
        public ulong Amount;
    }

    public struct ProposalVotedLog
    {
        public uint ProposalId;
        public bool Vote;
    }

    public struct Proposal
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Recipient;
        
        public ulong RequestedAmount;


        public string Description;

        /// <summary>
        /// True if proposal executed and the requested fund is transferred
        /// </summary>
        public bool Executed;
    }
}