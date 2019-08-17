using System;
using Moq;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static Airdrop;

namespace Tests
{
    public class AirdropTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mockInternalExecutor;
        private Address airdropContractOwner;
        private Address registrant;
        private Address tokenContract;
        private Address airdropContract;
        private ulong totalSupply;
        private ulong endBlock;
        private ulong index;

        public AirdropTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
            this.mockContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mockInternalExecutor.Object);
            this.airdropContractOwner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.registrant = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.tokenContract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.airdropContract = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.totalSupply = 100_000;
            this.endBlock = 1_000_000;
            this.index = 0;
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            // Verify that PersistentState was called with the total supply, name, symbol and endblock
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.TotalSupply), this.totalSupply));
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.EndBlock), this.endBlock));
            this.mockPersistentState.Verify(s => s.SetAddress(nameof(Airdrop.TokenContractAddress), this.tokenContract));
        }

        [Fact]
        public void AccountStatus_Returns_CorrectStatus()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            // Not Enrolled registrant
            var status = airdrop.GetAccountStatus(this.registrant);
            this.mockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{this.registrant}"));
            Assert.Equal(Status.NOT_ENROLLED, status);

            // Set and check for Enrolled registrants
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(Status.ENROLLED);
            status = airdrop.GetAccountStatus(this.registrant);
            Assert.Equal(Status.ENROLLED, status);

            // Set and check for Funded registrants
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(Status.FUNDED);
            status = airdrop.GetAccountStatus(this.registrant);
            Assert.Equal(Status.FUNDED, status);
        }

        #region Register Tests

        [Fact]
        public void Register_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Airdrop.Status.ENROLLED));

            Assert.True(result);
        }

        [Fact]
        public void Register_Success_SetsEnrolledStatus()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            // Assert status succeeds
            Assert.True(result);

            // Verfies that this was run during SignUp
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.ENROLLED));
        }

        [Fact]
        public void Register_Success_LogsStatus()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = this.registrant, Status = Status.ENROLLED }));
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var status = airdrop.Register();

            // Assert status succeeds
            Assert.False(status);
        }

        [Fact]
        public void Register_Fail_AccountAlreadyEnrolled()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            // Assert status succeeds
            Assert.True(result);

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(Status.ENROLLED);

            result = airdrop.Register();

            Assert.False(result);
        }

        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(this.index);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();
            this.mockPersistentState.Verify(s => s.SetUInt64("NumberOfRegistrants", airdrop.NumberOfRegistrants + 1));

            for (uint i = 1; i < 5; i++)
            {
                this.mockPersistentState.Setup(s => s.GetUInt64("NumberOfRegistrants")).Returns(i);
                Assert.Equal(i, airdrop.NumberOfRegistrants);
            }
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.AddRegistrant(this.registrant);

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Airdrop.Status.ENROLLED));

            Assert.True(result);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderIsNotOwner()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.AddRegistrant(this.registrant);

            Assert.False(result);
        }
        #endregion

        #region Withdraw Tests

        [Fact]
        public void Withdraw_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            //object[] transferParams = {
            //    new TransferParams { Address = this.registrant, Amount = this.totalSupply }
            //};

            //this.mockContractState.Setup(e => e.InternalTransactionExecutor.Call(this.mockContractState.Object, this.tokenContract, this.totalSupply, "TransferTo", transferParams, 1000)).Returns(new TransferResult());

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(Status.ENROLLED);

            //var test = this.mockContractState.Object.InternalTransactionExecutor.Call(this.mockContractState.Object, this.tokenContract, this.totalSupply, "TransferTo", transferParams, 1000);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{this.registrant}"));
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.FUNDED));

            var status = airdrop.GetAccountStatus(this.registrant);

            Assert.Equal(Airdrop.Status.FUNDED, status);

            Assert.True(result);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            //object[] transferParams = {
            //    new TransferParams { Address = this.registrant, Amount = 100 }
            //};

            //this.mockInternalExecutor.Setup(e => e.Call(this.mockContractState.Object, this.tokenContract, this.totalSupply, "TransferTo", transferParams, 10_000)).Returns(new TransferResult());

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(e => e.GetUInt64("EndBlock"));

            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_AccountPreviouslyFunded()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.registrant}")).Returns(Airdrop.Status.FUNDED);

            var result = airdrop.Withdraw();

            this.mockPersistentState.Verify(e => e.GetUInt64("EndBlock"));
            this.mockPersistentState.Verify(e => e.GetStruct<Airdrop.Status>($"Status:{this.registrant}"));

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
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            Assert.True(WithdrawExecute(airdrop, Status.ENROLLED));
        }

        private bool WithdrawExecute(Airdrop airdrop, Status accountStatus)
        {
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(accountStatus);

            accountStatus = airdrop.GetAccountStatus(this.registrant);

            this.mockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{this.registrant}"));

            if (accountStatus != Status.ENROLLED)
            {
                return false;
            }

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            this.mockPersistentState.Verify(s => s.GetBool($"RegistrationIsClosed"));

            if (registrationIsClosed == false)
            {
                return false;
            }


            var amountToDistribute = airdrop.AmountToDistribute;
            this.mockPersistentState.Verify(s => s.GetUInt64($"AmountToDistribute"));

            Assert.True(amountToDistribute == 100_000);
            var transferParams = new object[] { this.registrant, amountToDistribute };

            this.mockInternalExecutor.Setup(e => e
                .Call(this.mockContractState.Object, this.tokenContract, this.totalSupply, "TransferTo", transferParams, 1000))
                .Returns(new TransferResult());

            var result = this.mockContractState.Object.InternalTransactionExecutor
                .Call(this.mockContractState.Object, this.tokenContract, this.totalSupply, "TransferTo", transferParams, 1000);

            if (result.Success == false)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Registration Is Closed Tests

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfPersistentStateIsTrue()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(s => s.GetBool($"RegistrationIsClosed")).Returns(true);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfCurrentBlockIsGreaterThanEndBlock()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.True(registrationIsClosed);

        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanEndBlock()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.False(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_FromPersistantStateIfNoEndBlock()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(0);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.False(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(0);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(0);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.CloseRegistration();

            Assert.True(result);

            this.mockPersistentState.Verify(s => s.SetBool("RegistrationIsClosed", true));

            this.mockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(true);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Fail_SenderIsNotOwner()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(0);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.CloseRegistration();

            Assert.False(result);

            var registrationIsClosed = airdrop.RegistrationIsClosed;

            Assert.False(registrationIsClosed);

        }
        #endregion

        #region Amount To Distribute Tests

        [Fact]
        public void AmountToDistribute_ReturnsCorrectAmount()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(s => s.GetBool($"RegistrationIsClosed")).Returns(true);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(10);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var amountToDistribute = airdrop.AmountToDistribute;

            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)10_000, amountToDistribute);


            // Starting with 10 tokens / 3 registrants = 3 each
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(10);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(3);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)3, amountToDistribute);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetAmountIfRegistrationOpen()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(0);
            this.mockPersistentState.Setup(s => s.GetBool($"RegistrationIsClosed")).Returns(false);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(10);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            // Call to initially calc the amount
            var amountToDistribute = airdrop.AmountToDistribute;

            // verify the amount was called from persistant state
            this.mockPersistentState.Verify(s => s.GetUInt64("AmountToDistribute"));

            // verify that RegistrationIsClosed was called from persistant state
            this.mockPersistentState.Verify(s => s.GetBool("RegistrationIsClosed"));

            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)0, amountToDistribute);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetIfAlreadyCalculated()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(s => s.GetBool($"RegistrationIsClosed")).Returns(true);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(10);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            // Call to initially calc the amount
            var amountToDistribute = airdrop.AmountToDistribute;

            // verify the amount was called from persistant state
            this.mockPersistentState.Verify(s => s.GetUInt64("AmountToDistribute"));
            // verify the amount was set with the correct amount
            this.mockPersistentState.Verify(s => s.SetUInt64("AmountToDistribute", 10_000));
            // Set the amount in persistant state
            this.mockPersistentState.Setup(s => s.GetUInt64("AmountToDistribute")).Returns(10_000);
            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)10_000, amountToDistribute);


            // Change the total supply and NumberOfRegistrants
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(10);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(3);

            // Get the amountToDistribute again
            amountToDistribute = airdrop.AmountToDistribute;

            // Should equal the amount before, ignoring any new changes
            Assert.Equal((ulong)10_000, amountToDistribute);
        }
        #endregion
        public class TransferResult : ITransferResult
        {
            public object ReturnValue => null;

            public bool Success => true;
        }
    }
}
