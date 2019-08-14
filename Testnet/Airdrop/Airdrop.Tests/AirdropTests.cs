using System;
using Moq;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;

namespace Tests
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
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

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
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.NOT_ENROLLED, status);
        }

        [Fact]
        public void AccountStatus_Set_Approved()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.ENROLLED);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.ENROLLED, status);
        }

        [Fact]
        public void AccountStatus_Set_Funded()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

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
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            airdrop.Register();

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.ENROLLED));
        }

        [Fact]
        public void SignUp_SetApprovedStatus()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            var status = airdrop.Register();

            // Assert status succeeds
            Assert.True(status);

            // Verfies that this was run during SignUp
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.ENROLLED));

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.ENROLLED);

            // Getting the status returns APPROVED
            Assert.Equal(Airdrop.Status.ENROLLED, airdrop.GetAccountStatus(this.owner));
        }


        [Fact]
        public void SignUp_Fail_AirdropClosed()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            var status = airdrop.Register();

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
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            var status = airdrop.Register();

            // Assert status succeeds
            Assert.True(status);

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.ENROLLED);

            status = airdrop.Register();

            Assert.False(status);
        }

        [Fact]
        public void SignUp_NumberOfRegistrantsIncrement()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockContractState.Setup(c => c.InternalTransactionExecutor.Call(this.mockContractState.Object, this.destination, 0, "Transfer To", new object[1], 0)).Returns(new TransferResult());
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(this.index);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            airdrop.Register();
            this.mockPersistentState.Verify(s => s.SetUInt64("NumberOfRegistrants", airdrop.NumberOfRegistrants + 1));

            for (uint i = 1; i < 4; i++)
            {
                this.mockPersistentState.Setup(s => s.GetUInt64("NumberOfRegistrants")).Returns(i);
                Assert.Equal(i, airdrop.NumberOfRegistrants);
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
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            //this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.ENROLLED);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}"));

            Assert.True(result);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(e => e.GetUInt64("EndBlock"));

            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_AccountPreviouslyFunded()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.destination, this.totalSupply, this.endBlock);

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

        #region Registration Is Closed Tests

        [Fact]
        public void RegistrationIsClosed_IsTrue_FromPersistantStateIfTrue()
        {

        }

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfCurrentBlockIsGreaterThanEndBlock()
        {

        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanEndBlock()
        {

        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_FromPersistantStateIfNoEndBlock()
        {

        }
        #endregion

        #region Amount To Distribute Tests

        #endregion

        public class TransferResult : ITransferResult
        {
            public object ReturnValue => null;

            public bool Success => true;
        }
    }
}
