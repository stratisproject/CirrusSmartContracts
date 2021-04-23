using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using System;
using Xunit;
using static DAOContract;

namespace DAOContractTests
{
    public class DAOContractTests
    {
        private const string Description = "AMM based DEX project";

        private readonly IPersistentState state;

        private readonly Mock<ISmartContractState> mContractState;
        private readonly Mock<IContractLogger> mContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;

        private readonly Address owner;
        private readonly Address contract;
        private readonly Address recipent;
        private readonly Address proposalOwner;
        private readonly Address voter;
        private uint minVotingDuration;
        public DAOContractTests()
        {
            state = new InMemoryState();
            mContractState = new Mock<ISmartContractState>();
            mContractLogger = new Mock<IContractLogger>();
            mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            mContractState.Setup(s => s.PersistentState).Returns(state);
            mContractState.Setup(s => s.ContractLogger).Returns(mContractLogger.Object);
            mContractState.Setup(s => s.InternalTransactionExecutor).Returns(mTransactionExecutor.Object);
            owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            contract = "0x0000000000000000000000000000000000000002".HexToAddress();
            recipent = "0x0000000000000000000000000000000000000003".HexToAddress();
            proposalOwner = "0x0000000000000000000000000000000000000004".HexToAddress();
            voter = "0x0000000000000000000000000000000000000005".HexToAddress();
            minVotingDuration = 1;
        }

        [Fact]
        public void Constructor_Sets_Parameters()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.MinQuorum.Should().Be(1);
            contract.Owner.Should().Be(owner);
            contract.LastProposalId.Should().Be(1);
            contract.MinVotingDuration.Should().Be(minVotingDuration);
        }

        [Fact]
        public void Constructor_MinVotingDuration_Higher_Than_MaxVotingDuration_Fails()
        {
            SetupMessage();
            minVotingDuration = 2 * DAOContract.DefaultMaxDuration;

            Func<DAOContract> contract = () => CreateContract();

            contract.Invoking(c => c())
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage($"MinVotingDuration should be lower than maxVotingDuration({DAOContract.DefaultMaxDuration})");
        }

        [Fact]
        public void UpdateMinVotingDuration_Called_By_None_Owner_Fails()
        {
            SetupMessage();

            var contract = CreateContract();

            SetupMessage(voter);
            contract.Invoking(c => c.UpdateMinVotingDuration(100))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The method is owner only.");
        }

        [Fact]
        public void UpdateMinVotingDuration_MinVotingDuration_Lower_Than_MaxVotingDuration_Fails()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.Invoking(c => c.UpdateMinVotingDuration(2 * DAOContract.DefaultMaxDuration))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("MinVotingDuration should be lower than MaxVotingDuration.");
        }

        [Fact]
        public void UpdateMinVotingDuration_Called_By_Owner_Success()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.UpdateMinVotingDuration(100);

            contract.MinVotingDuration.Should().Be(100);
        }

        [Fact]
        public void UpdateMaxVotingDuration_Called_By_None_Owner_fails()
        {
            SetupMessage();

            var contract = CreateContract();

            SetupMessage(voter);
            contract.Invoking(c => c.UpdateMaxVotingDuration(100))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The method is owner only.");
        }

        [Fact]
        public void UpdateMaxVotingDuration_Called_By_Owner_Success()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.UpdateMaxVotingDuration(100);

            contract.MaxVotingDuration.Should().Be(100);
        }

        [Fact]
        public void UpdateMaxVotingDuration_MaxVotingDuration_Higher_Than_MinVotingDuration_Fails()
        {
            minVotingDuration = 100;
            SetupMessage();

            var contract = CreateContract();

            contract.Invoking(c => c.UpdateMaxVotingDuration(20))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("MaxVotingDuration should be higher than MinVotingDuration.");
        }

        [Fact]
        public void MinQuorum_Success()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.MinQuorum
                    .Should()
                    .Be(1);

            contract.WhitelistAddress(voter);

            contract.MinQuorum
                    .Should()
                    .Be(1);


            contract.WhitelistedCount
                    .Should()
                    .Be(1);

            contract.WhitelistAddress(owner);

            contract.MinQuorum
                    .Should()
                    .Be(2);

            contract.WhitelistedCount
                    .Should()
                    .Be(2);
        }

        [Fact]
        public void WhitelistAddress_Success()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.WhitelistAddress(voter);

            contract.IsWhitelisted(voter)
                    .Should()
                    .BeTrue();
        }

        [Fact]
        public void BlacklistAddress_Remove_Whitelisted_Address_Success()
        {
            SetupMessage();

            var contract = CreateContract();

            contract.WhitelistAddress(voter);

            contract.BlacklistAddress(voter);

            contract.IsWhitelisted(voter)
                    .Should()
                    .BeFalse();
        }

        [Fact]
        public void CreateProposal_Caller_Send_Funds_Failss()
        {
            var contract = CreateContract();
            SetupMessage(proposalOwner,20);

            contract.Invoking(c => c.CreateProposal(recipent, 100, 10, Description))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage($"The method is not payable.");
        }

        [Fact]
        public void CreateProposal_VotingDuration_Lower_Than_MinVotingDuration_Fails()
        {
            minVotingDuration = 100;
            var contract = CreateContract();
            SetupMessage(proposalOwner);

            contract.Invoking(c => c.CreateProposal(recipent, 100, 10, Description))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage($"Voting duration should be between 100 and {DAOContract.DefaultMaxDuration}.");
        }

        [Fact]
        public void CreateProposal_Description_Higher_Than_200_length_Fails()
        {
            var contract = CreateContract();
            SetupMessage(proposalOwner);

            var description = new string('a', 201);

            contract.Invoking(c => c.CreateProposal(recipent, 100, 10, description))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The description length can be up to 200 characters.");
        }

        [Fact]
        public void CreateProposal_Success()
        {
            var contract = CreateContract();
            SetupMessage(proposalOwner);

            contract.CreateProposal(recipent, 100, 10, Description)
                    .Should()
                    .Be(1);

            var proposal = new Proposal
            {
                RequestedAmount = 100,
                Owner = proposalOwner,
                Recipient = recipent,
                Description = Description,
            };

            contract.GetProposal(1).Should().Be(proposal);
            contract.GetVotingDeadline(1).Should().Be(11);
            contract.LastProposalId.Should().Be(2);

            var log = new ProposalAddedLog
            {
                ProposalId = 1,
                Amount = 100,
                Recipent = recipent,
                Description = Description
            };

            VerifyLog(log);
        }

        [Fact]
        public void Vote_Caller_Send_Funds_Fails()
        {
            var contract = CreateContract();

            SetupMessage(voter, 20);
            contract.Invoking(c => c.Vote(1, true))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The method is not payable.");

        }

        [Fact]
        public void Vote_As_Yes_Proposal_Fails_If_Caller_Is_Not_Whitelisted()
        {
            var duration = 10u;
            var contract = CreateContract();

            SetupMessage(proposalOwner);

            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            contract.Invoking(c => c.Vote(proposalId, true))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The caller is not whitelisted.");

        }

        [Fact]
        public void Vote_As_Yes_Proposal_Fails_If_Voting_Ended()
        {
            var duration = 10u;
            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupBlock(duration + 1);
            SetupMessage(voter);

            contract.Invoking(c => c.Vote(proposalId, true))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("Voting is closed.");

        }

        [Fact]
        public void Vote_As_Yes_Proposal_Success()
        {
            var duration = 10u;
            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            SetupBlock(10);

            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            VerifyLog(new ProposalVotedLog { ProposalId = proposalId, Vote = true });

            contract.GetNoVotes(proposalId)
                    .Should()
                    .Be(0);

            contract.GetYesVotes(proposalId)
                    .Should()
                    .Be(1);
        }

        [Fact]
        public void Vote_Change_Already_Casted_No_Vote_Success()
        {
            var duration = 10u;
            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupBlock(10);
            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, false);

            contract.Vote(proposalId, true);


            contract.GetNoVotes(proposalId)
                    .Should()
                    .Be(0);

            contract.GetYesVotes(proposalId)
                    .Should()
                    .Be(1);

        }

        [Fact]
        public void Vote_Change_Already_Casted_Yes_Vote_Success()
        {
            var duration = 10u;
            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            SetupBlock(10);

            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            contract.Vote(proposalId, false);

            contract.GetNoVotes(proposalId)
                    .Should()
                    .Be(1);

            contract.GetYesVotes(proposalId)
                    .Should()
                    .Be(0);

        }

        [Fact]
        public void ExecuteProposal_Caller_Send_Funds_Fails()
        {
            var contract = CreateContract();

            SetupMessage(proposalOwner,20);
            contract.Invoking(m => m.ExecuteProposal(1))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The method is not payable.");
        }

        [Fact]
        public void ExecuteProposal_Proposal_Voted_As_No_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, false);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The proposal voting is not passed.");
        }

        [Fact]
        public void ExecuteProposal_Voting_MinQuorum_Is_Not_Reached_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);
            contract.WhitelistAddress(owner);
            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("Min quorum for proposal is not reached.");
        }

        [Fact]
        public void ExecuteProposal_Already_Executed_Proposal_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());
            var proposal = contract.GetProposal(proposalId);
            proposal.Executed = true;
            state.SetStruct($"Proposals:{proposalId}", proposal);

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The proposal is already executed.");
        }

        [Fact]
        public void ExecuteProposal_Proposal_In_Voting_Duration_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(10);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("Voting is still open for the proposal.");
        }

        [Fact]
        public void ExecuteProposal_Insufficient_Balance_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 99);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("Insufficient balance.");
        }

        [Fact]
        public void ExecuteProposal_Transfer_Fails()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Failed());

            contract.Invoking(m => m.ExecuteProposal(proposalId))
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("Transfer failed.");
        }

        [Fact]
        public void ExecuteProposal_Success()
        {
            var duration = 10u;

            var contract = CreateContract();
            contract.WhitelistAddress(voter);

            SetupMessage(proposalOwner);
            var proposalId = contract.CreateProposal(recipent, 100, duration, Description);

            SetupMessage(voter);
            contract.Vote(proposalId, true);

            SetupMessage(proposalOwner);
            SetupBlock(12);
            mContractState.Setup(m => m.GetBalance).Returns(() => 100);
            mTransactionExecutor.Setup(m => m.Transfer(mContractState.Object, recipent, 100)).Returns(TransferResult.Succeed());

            contract.ExecuteProposal(proposalId);

            mTransactionExecutor.Verify(m => m.Transfer(mContractState.Object, recipent, 100), Times.Once());
            contract.GetProposal(proposalId)
                    .Executed
                    .Should()
                    .BeTrue();

            VerifyLog(new ProposalExecutedLog { ProposalId = proposalId, Recipent = recipent, Amount = 100 });
        }

        [Fact]
        public void Deposit_Called_By_None_Owner_Fails()
        {
            var amount = 1200000ul;
            var contract = CreateContract();

            SetupMessage(voter, amount);
            contract.Invoking(c=>c.Deposit())
                    .Should()
                    .Throw<SmartContractAssertException>()
                    .WithMessage("The method is owner only.");
        }

        [Fact]
        public void Deposit_Success()
        {
            var amount = 1200000ul;
            var contract = CreateContract();

            SetupMessage(owner, amount);
            contract.Deposit();

            VerifyLog(new FundRaisedLog { Sender = owner, Amount = amount });
        }

        private DAOContract CreateContract()
        {
            SetupMessage();
            SetupBlock();

            return new DAOContract(mContractState.Object, minVotingDuration);
        }

        private void VerifyLog<T>(T expectedLog) where T : struct
        {
            VerifyLog(expectedLog, Times.Once());
        }

        private void VerifyLog<T>(T expectedLog, Times times) where T : struct
        {
            mContractLogger.Verify(x => x.Log(mContractState.Object, expectedLog), times);
        }

        private void SetupMessage(Address? owner = null, ulong amount = 0)
        {
            mContractState.Setup(m => m.Message).Returns(new Message(contract, owner ?? this.owner, amount));
        }

        private void SetupBlock(uint block = 1)
        {
            mContractState.Setup(m => m.Block.Number).Returns(block);
        }
    }
}
