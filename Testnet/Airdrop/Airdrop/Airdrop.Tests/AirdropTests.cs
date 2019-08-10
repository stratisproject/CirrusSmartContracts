using System;
using Moq;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.SmartContracts.Samples.Tests
{
    public class AirdropTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private Address owner;
        private Address sender;
        private Address contract;
        private Address spender;
        private Address destination;
        private ulong totalSupply;
        private ulong endBlock;
        private ulong index;

        public AirdropTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.sender = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.spender = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.destination = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.totalSupply = 100_000;
            this.endBlock = 1_000_000;
            this.index = 0;
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            // Verify that PersistentState was called with the total supply, name, symbol and endblock
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.TotalSupply), this.totalSupply));
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.EndBlock), this.endBlock));
            this.mockPersistentState.Verify(s => s.SetAddress(nameof(Airdrop.TokenContractAddress), this.destination));
        }

        #region Set Account Status Tests

        [Fact]
        public void AccountStatus_UnsetAccountReturnsNull()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.UNAPPROVED, status);
        }

        [Fact]
        public void AccountStatus_Set_Approved()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.APPROVED, status);
        }

        [Fact]
        public void AccountStatus_Set_Funded()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.FUNDED);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.FUNDED, status);
        }

        #endregion

        #region SignUp Tests

        [Fact]
        public void SignUp_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            airdrop.SignUp();

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.APPROVED));
        }

        [Fact]
        public void SignUp_SetApprovedStatus()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.SignUp();

            // Assert status succeeds
            Assert.True(status);

            // Verfies that this was run during SignUp
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.APPROVED));

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            // Getting the status returns APPROVED
            Assert.Equal(Airdrop.Status.APPROVED, airdrop.GetAccountStatus(this.owner));
        }


        [Fact]
        public void SignUp_Fail_AirdropClosed()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.SignUp();

            // Assert status succeeds
            Assert.False(status);
        }

        [Fact]
        public void SignUp_Fail_DuplicateAccount()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.SignUp();

            // Assert status succeeds
            Assert.True(status);

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            status = airdrop.SignUp();

            Assert.False(status);
        }

        [Fact]
        public void SignUp_IndexIncrement()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("Index")).Returns(this.index);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            airdrop.SignUp();
            this.mockPersistentState.Verify(s => s.SetUInt64("Index", airdrop.Index + 1));

            for (uint i = 1; i < 4; i++)
            {
                this.mockPersistentState.Setup(s => s.GetUInt64("Index")).Returns(i);
                Assert.Equal(i, airdrop.Index);
            }
        }

        [Fact]
        public void SignUp_LogsStatus()
        {

        }
        #endregion

        #region Withdraw Tests

        [Fact]
        public void Withdraw_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("Index")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}"));

            Assert.True(result);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("Index")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(e => e.GetUInt64("EndBlock"));

            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_AccountPreviouslyFunded()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("Index")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.FUNDED);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(e => e.GetUInt64("EndBlock"));
            this.mockPersistentState.Verify(e => e.GetStruct<Airdrop.Status>($"Status:{this.owner}"));

            Assert.False(result, "Failure - Account has already been funded");
        }

        [Fact]
        public void Withdraw_Fail_TokenContractTransferFailure()
        {

        }

        [Fact]
        public void Withdraw_AmountCalculationIsCorrect()
        {

        }

        [Fact]
        public void Withdraw_SetFundedStatus()
        {

        }

        [Fact]
        public void Withdraw_LogsStatus()
        {

        }
        #endregion
    }
}
