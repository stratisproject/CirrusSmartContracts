using System;
using FluentAssertions;
using Moq;
using OpdexProposalVoteTests;
using Stratis.SmartContracts;
using Xunit;

namespace PrivateYesNoVoteTests
{
    public class PrivateYesNoVoteTests : BaseContractTest
    {
        [Fact]
        public void CreatesVoteContract_Success()
        {
            const ulong currentBlock = 1000;
            const ulong duration = 1500;
            const ulong expectedEndBlock = 2500;
            
            var voteContract = CreateNewVoteContract(currentBlock, duration);

            voteContract.VotePeriodEndBlock.Should().Be(expectedEndBlock);
            voteContract.IsVoter(AddressOne).Should().BeTrue();
            voteContract.IsVoter(AddressTwo).Should().BeTrue();
            voteContract.IsVoter(AddressThree).Should().BeTrue();
        }

        [Fact]
        public void WhitelistVoters_Success()
        {
            var voteContract = CreateNewVoteContract();

            SetupMessage(Contract, Owner);
            
            var addressList = new[] {AddressFour, AddressFive, AddressSix};
            var bytes = Serializer.Serialize(addressList);
            
            voteContract.WhitelistVoters(bytes);

            voteContract.IsVoter(AddressFour).Should().BeTrue();
            voteContract.IsVoter(AddressFive).Should().BeTrue();
            voteContract.IsVoter(AddressSix).Should().BeTrue();
        }

        [Fact]
        public void WhitelistVoters_Throws_SenderIsNotOwner()
        {
            var voteContract = CreateNewVoteContract();
            var addressList = new[] {AddressFour, AddressFive, AddressSix};
            var bytes = Serializer.Serialize(addressList);
            
            SetupMessage(Contract, AddressOne);

            voteContract
                .Invoking(v => v.WhitelistVoters(bytes))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Must be contract owner to whitelist addresses.");
        }
        
        [Fact]
        public void WhitelistVoter_Success()
        {
            var voteContract = CreateNewVoteContract();

            SetupMessage(Contract, Owner);
            
            voteContract.WhitelistVoter(AddressFour);

            voteContract.IsVoter(AddressFour).Should().BeTrue();
        }

        [Fact]
        public void WhitelistVoter_Throws_SenderIsNotOwner()
        {
            var voteContract = CreateNewVoteContract();
            
            SetupMessage(Contract, AddressOne);

            voteContract
                .Invoking(v => v.WhitelistVoter(AddressFour))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Must be contract owner to whitelist addresses.");
        }

        [Fact]
        public void CanVoteYes_Success()
        {
            var sender = AddressOne;
            const bool vote = true;

            var voteContract = CreateNewVoteContract();
            
            SetupMessage(Contract, sender);

            voteContract.YesVotes.Should().Be(0);
            voteContract.NoVotes.Should().Be(0);
            
            voteContract.Vote(vote);

            voteContract.NoVotes.Should().Be(0);
            voteContract.YesVotes.Should().Be(1);

            VerifyLog(new PrivateYesNoVote.VoteEvent {Voter = AddressOne, Vote = vote}, Times.Once);
        }
        
        [Fact]
        public void CanVoteNo_Success()
        {
            var sender = AddressOne;
            const bool vote = false;
            
            var voteContract = CreateNewVoteContract();
            
            SetupMessage(Contract, sender);

            voteContract.YesVotes.Should().Be(0);
            voteContract.NoVotes.Should().Be(0);
            
            voteContract.Vote(vote);

            voteContract.NoVotes.Should().Be(1);
            voteContract.YesVotes.Should().Be(0);
            
            VerifyLog(new PrivateYesNoVote.VoteEvent {Voter = sender, Vote = vote}, Times.Once);
        }

        [Fact]
        public void CanVote_UpdatesCounts_Success()
        {
            var voteContract = CreateNewVoteContract();
            
            voteContract.YesVotes.Should().Be(0);
            voteContract.NoVotes.Should().Be(0);
            
            // Address 1 Vote
            SetupMessage(Contract, AddressOne);
            voteContract.Vote(false);
            
            // Address 2 Vote
            SetupMessage(Contract, AddressTwo);
            voteContract.Vote(true);
            
            // Address 3 Vote
            SetupMessage(Contract, AddressThree);
            voteContract.Vote(true);

            voteContract.YesVotes.Should().Be(2);
            voteContract.NoVotes.Should().Be(1);
        }

        [Fact]
        public void CanVote_Throws_AlreadyVoted()
        {
            var voteContract = CreateNewVoteContract();
            
            SetupMessage(Contract, AddressOne);
            voteContract.Vote(false);
            
            voteContract.NoVotes.Should().Be(1);
            
            voteContract
                .Invoking(v => v.Vote(true))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Sender has already voted.");
        }

        [Fact]
        public void CanVote_Throws_NotAuthorized()
        {
            var voteContract = CreateNewVoteContract();
            
            SetupMessage(Contract, Owner);
            
            voteContract
                .Invoking(v => v.Vote(true))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Sender is not authorized to vote.");
        }

        [Fact]
        public void CanVote_Throws_VotPeriodEnded()
        {
            var voteContract = CreateNewVoteContract();

            SetupMessage(Contract, AddressOne);
            SetupBlock(ulong.MaxValue);

            voteContract
                .Invoking(v => v.Vote(true))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Voting period has ended.");
        }

        [Fact]
        public void CreateContract_Throws_BlockDurationOverflow()
        {
            try
            {
                CreateNewVoteContract(2, ulong.MaxValue);
                
                // Intentionally fail the test if we reach here
                false.Should().BeTrue();
            }
            catch (OverflowException ex)
            {
                ex.Should().NotBeNull();
            }
        }
    }
}