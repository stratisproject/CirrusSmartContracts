using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static Airdrop;
using System.Collections.Generic;

namespace Tests
{
    public class AirdropTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Owner;
        private readonly Address Registrant;
        private readonly Address TokenContractAddress;
        private readonly Address AirdropContractAddress;
        private readonly ulong TotalSupply;
        private readonly ulong EndBlock;
        private readonly ulong NumberOfRegistrants;
        private ulong CurrentBlock;

        public AirdropTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(s => s.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(s => s.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(s => s.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            Registrant = "0x0000000000000000000000000000000000000002".HexToAddress();
            TokenContractAddress = "0x0000000000000000000000000000000000000003".HexToAddress();
            AirdropContractAddress = "0x0000000000000000000000000000000000000004".HexToAddress();
            TotalSupply = 100_000;
            EndBlock = 1_000_000;
            NumberOfRegistrants = 0;
            CurrentBlock = 1;
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            MockContractState.Setup(m => m.Message).Returns(new Message(AirdropContractAddress, Owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(MockContractState.Object, TokenContractAddress, TotalSupply, EndBlock);

            // Verify that PersistentState was called with the total supply, name, symbol and endblock
            MockPersistentState.Verify(s => s.SetUInt64(nameof(TotalSupply), TotalSupply));
            MockPersistentState.Verify(s => s.SetUInt64(nameof(EndBlock), EndBlock));
            MockPersistentState.Verify(s => s.SetAddress(nameof(TokenContractAddress), TokenContractAddress));
            MockPersistentState.Verify(s => s.SetAddress(nameof(Owner), Owner));
        }

        [Fact]
        public void AccountStatus_Returns_CorrectStatus()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Not Enrolled registrant
            var status = airdrop.GetAccountStatus(Registrant);
            MockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{Registrant}"));
            Assert.Equal(Status.NOT_ENROLLED, status);

            // Set and check for Enrolled registrants
            MockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{Registrant}")).Returns(Status.ENROLLED);
            status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal(Status.ENROLLED, status);

            // Set and check for Funded registrants
            MockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{Registrant}")).Returns(Status.FUNDED);
            status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal(Status.FUNDED, status);
        }

        #region Register Tests

        [Fact]
        public void Register_Success()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.Register();

            MockPersistentState.Verify(s => s.SetStruct($"Status:{Registrant}", Status.ENROLLED));

            Assert.True(result);

            // Verfies the new status was set
            MockPersistentState.Verify(s => s.SetStruct($"Status:{Registrant}", Status.ENROLLED));
            // Verify the new status was logged
            MockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = Status.ENROLLED }));
        }

        [Fact]
        public void Register_Success_WithEndBlock_0_UntilRegistrationIsClosed()
        {
            ulong _endBlock = 0;
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, _endBlock, TotalSupply);

            var result = airdrop.Register();
            Assert.True(result);

            MockPersistentState.Setup(e => e.GetBool("RegistrationIsClosed")).Returns(true);

            result = airdrop.Register();
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            var status = airdrop.Register();
            Assert.False(status);
        }

        [Fact]
        public void Register_Fail_AccountAlreadyEnrolled()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.Register();

            // Verify the status was set for the registrant to Enrolled
            MockPersistentState.Verify(s => s.SetStruct($"Status:{Registrant}", Status.ENROLLED));

            // Assert status succeeds
            Assert.True(result);

            // Set the status manually in persistent state
            MockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{Registrant}")).Returns(Status.ENROLLED);

            result = airdrop.Register();

            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_RegistrantIsOwner()
        {
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.Register();
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_MoreRegistrantsThanSupply()
        {
            ulong _totalSupply = 1;
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, _totalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            var result = airdrop.Register();
            Assert.False(result);

            MockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{Registrant}"));
            MockPersistentState.Verify(s => s.GetBool("RegistrationIsClosed"));
            MockPersistentState.Verify(s => s.GetUInt64(nameof(TotalSupply)));
            MockPersistentState.Verify(s => s.GetUInt64(nameof(NumberOfRegistrants)));

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.False(registrationIsClosed);

            var accountStatus = airdrop.GetAccountStatus(Registrant);
            Assert.Equal(Status.NOT_ENROLLED, accountStatus);

            _totalSupply = airdrop.TotalSupply;
            Assert.Equal((ulong)1, _totalSupply);

            var numRegistrants = airdrop.NumberOfRegistrants;
            Assert.Equal((ulong)10, numRegistrants);
        }

        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(NumberOfRegistrants);

            var result = airdrop.Register();
            MockPersistentState.Verify(s => s.SetUInt64(nameof(NumberOfRegistrants), airdrop.NumberOfRegistrants + 1));

            for (uint i = 1; i < 5; i++)
            {
                MockPersistentState.Setup(s => s.GetUInt64(nameof(NumberOfRegistrants))).Returns(i);
                Assert.Equal(i, airdrop.NumberOfRegistrants);
            }
        }

        [Fact]
        public void Register_Fail_NumberOfRegistrantsNotIncremented()
        {
            ulong totalNumberOfRegistrants = 0;
            var registrations = new Dictionary<string, Status>();

            MockContractState.Setup(m => m.Message).Returns(new Message(AirdropContractAddress, Owner, 0));
            MockContractState.Setup(b => b.Block.Number).Returns(999_999);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(EndBlock))).Returns(EndBlock);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(TotalSupply);

            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(totalNumberOfRegistrants);

            MockPersistentState.Setup(e => e.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()))
                .Callback<string, ulong>((key, value) => { totalNumberOfRegistrants = value; });

            MockPersistentState.Setup(e => e.GetStruct<Status>($"Status:{Registrant}"))
                .Returns<string>(key => registrations.ContainsKey(key) ? registrations[key] : Status.NOT_ENROLLED);

            MockPersistentState.Setup(e => e.SetStruct($"Status:{Registrant}", It.IsAny<Status>()))
                .Callback<string, Status>((key, value) =>
                {
                    if (registrations.ContainsKey(key)) registrations[key] = value;
                    else registrations.Add(key, value);
                });

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(MockContractState.Object, TokenContractAddress, TotalSupply, EndBlock);

            Assert.Equal((ulong)0, totalNumberOfRegistrants);
            MockContractState.Setup(m => m.Message).Returns(new Message(AirdropContractAddress, Registrant, 0));

            // call first time with registrant that is not registered yet.
            var result = airdrop.Register();

            Assert.True(result);
            MockPersistentState.Verify(s => s.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Once);
            Assert.Equal((ulong)1, totalNumberOfRegistrants);

            // call second time with same registrant that is already registered.
            result = airdrop.Register();
            Assert.False(result);
            Assert.Equal((ulong)1, totalNumberOfRegistrants);

            // verify it's not called more times than before. It should still be only once.
            MockPersistentState.Verify(s => s.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.AddRegistrant(Registrant);

            MockPersistentState.Verify(s => s.SetStruct($"Status:{Registrant}", Status.ENROLLED));

            Assert.True(result);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderIsNotOwner()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.AddRegistrant(Registrant);
            Assert.False(result);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderAndRegistrantAreBothOwner()
        {
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            var result = airdrop.AddRegistrant(Owner);
            Assert.False(result);
        }
        #endregion

        #region Withdraw Tests

        [Fact]
        public void Withdraw_Success()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                transferResult = new TransferResult(success: true)
            };

            var result = WithdrawExecute(withdrawParams);
            Assert.True(result);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            Address sender = Registrant;
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                transferResult = new TransferResult(success: true)
            };

            var result = WithdrawExecute(withdrawParams);
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_IncorrectAccountStatus()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.FUNDED,
                ExpectedAmountToDistribute = TotalSupply,
                transferResult = new TransferResult(success: true)
            };
            var result = WithdrawExecute(withdrawParams);
            Assert.False(result);

            withdrawParams.Status = Status.NOT_ENROLLED;
            result = WithdrawExecute(withdrawParams);
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_TokenContractAddressTransferFailure()
        {
            Address sender = Registrant;
            CurrentBlock = 1_000_001;
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                transferResult = new TransferResult(success: false)
            };

            var result = WithdrawExecute(withdrawParams);

            // Contract should fail
            Assert.False(result);

            var accountStatus = airdrop.GetAccountStatus(Registrant);

            // Status should not be set to funded for user
            Assert.Equal(Status.ENROLLED, accountStatus);
        }

        [Fact]
        public void Withdraw_Fail_AmountToDistributesZero()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            ulong _totalSupply = 0;
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, _totalSupply);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = 0,
                transferResult = new TransferResult(success: false)
            };

            var result = WithdrawExecute(withdrawParams);

            MockPersistentState.Verify(e => e.GetUInt64("AmountToDistribute"));
            Assert.Equal((ulong)0, airdrop.AmountToDistribute);

            // Contract should fail
            Assert.False(result);
        }

        private bool WithdrawExecute(WithdrawExecuteParams withdrawParams)
        {
            // Set status specified by param
            MockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}")).Returns(withdrawParams.Status);
            // Get the acount status from the contract method
            var accountStatus = withdrawParams.Airdrop.GetAccountStatus(withdrawParams.Sender);
            // Ensure the status was retrieved from persistant state
            MockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}"));
            // Fail if account status is not enrolled
            if (accountStatus != Status.ENROLLED)
            {
                return false;
            }

            // Check if the airdrops registration is cold.
            var registrationIsClosed = withdrawParams.Airdrop.RegistrationIsClosed;
            // Ensure persistent state is checked
            MockPersistentState.Verify(s => s.GetBool("RegistrationIsClosed"));
            // If registration is closed, fail
            if (registrationIsClosed == false)
            {
                return false;
            }

            // Get the amount to distribute from the contract method
            var amountToDistribute = withdrawParams.Airdrop.AmountToDistribute;
            // Ensure the amount to distribute was retrieved from persistant state
            MockPersistentState.Verify(s => s.GetUInt64("AmountToDistribute"));
            // Assert the amount to distibute is whats expected
            Assert.True(amountToDistribute == withdrawParams.ExpectedAmountToDistribute);
            // Create transfer params to use in mock call
            var transferParams = new object[] { withdrawParams.Sender, amountToDistribute };

            // Mock the calls expected result
            MockInternalExecutor.Setup(e => e
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferTo", transferParams, 10_000))
                .Returns(withdrawParams.transferResult);

            // Make call and retrieve the result
            var result = MockContractState.Object.InternalTransactionExecutor
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferTo", transferParams, 10_000);
            // Fail if result was not successful
            if (result.Success == false)
            {
                return false;
            }

            // Act as normal method would, set FUNDED account status
            MockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}")).Returns(Status.FUNDED);
            // Act as normal method would, log the new status
            MockContractLogger.Setup(l => l.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = withdrawParams.Sender, Status = Status.FUNDED })).Verifiable();

            return true;
        }
        #endregion

        #region Registration Is Closed Tests

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfPersistentStateIsTrue()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(true);

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfCurrentBlockIsGreaterThanEndBlock()
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanOrEqualToEndBlock()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.False(registrationIsClosed);

            MockContractState.Setup(b => b.Block.Number).Returns(EndBlock);

            registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.False(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Success()
        {
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.False(airdrop.RegistrationIsClosed);

            var result = airdrop.CloseRegistration();
            Assert.True(result);

            MockPersistentState.Verify(s => s.SetBool("RegistrationIsClosed", true));
            MockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(true);

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Fail_SenderIsNotOwner()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.False(airdrop.RegistrationIsClosed);

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
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(true);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            var amountToDistribute = airdrop.AmountToDistribute;

            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)10_000, amountToDistribute);


            // Starting with 10 tokens / 3 registrants = 3 each
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(10);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(3);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)3, amountToDistribute);


            // Starting with 10 tokens / 4 registrants = 2 each
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(10);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(4);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)2, amountToDistribute);


            // Starting with 100_000_000_000 tokens / 5456 registrants = 18328445 each
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(100_000_000_000);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(5456);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)18328445, amountToDistribute);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetAmountIfRegistrationOpen()
        {
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(false);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            // Call to initially calc the amount
            var amountToDistribute = airdrop.AmountToDistribute;

            // verify the amount was called from persistant state
            MockPersistentState.Verify(s => s.GetUInt64("AmountToDistribute"));

            // verify that RegistrationIsClosed was called from persistant state
            MockPersistentState.Verify(s => s.GetBool("RegistrationIsClosed"));

            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)0, amountToDistribute);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetIfAlreadyCalculated()
        {
            ulong expectedAmount = 10_000;
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(s => s.GetBool("RegistrationIsClosed")).Returns(true);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            // Call to initially calc the amount
            var amountToDistribute = airdrop.AmountToDistribute;

            // verify the amount was called from persistant state
            MockPersistentState.Verify(s => s.GetUInt64("AmountToDistribute"));
            // verify the amount was set with the correct amount
            MockPersistentState.Verify(s => s.SetUInt64("AmountToDistribute", expectedAmount));
            // Set the amount in persistant state
            MockPersistentState.Setup(s => s.GetUInt64("AmountToDistribute")).Returns(expectedAmount);
            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal((ulong)expectedAmount, amountToDistribute);


            // Change the total supply and NumberOfRegistrants
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(10);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(NumberOfRegistrants))).Returns(3);

            // Get the amountToDistribute again
            amountToDistribute = airdrop.AmountToDistribute;

            // Should equal the amount before, ignoring any new changes
            Assert.Equal(expectedAmount, amountToDistribute);
        }
        #endregion

        public class TransferResult : ITransferResult
        {
            public TransferResult(bool success = true)
            {
                Success = success;
            }

            public object ReturnValue => null;
            public bool Success { get; set; }
        }

        public struct WithdrawExecuteParams
        {
            public Address Sender;
            public Airdrop Airdrop;
            public Status Status;
            public ulong ExpectedAmountToDistribute;
            public TransferResult transferResult;
        }

        private Airdrop InitializeTest(Address sender, Address owner, ulong currentBlock, ulong _endBlock, ulong _totalSupply)
        {
            MockContractState.Setup(m => m.Message).Returns(new Message(AirdropContractAddress, sender, 0));
            MockContractState.Setup(b => b.Block.Number).Returns(currentBlock);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(EndBlock))).Returns(_endBlock);
            MockPersistentState.Setup(e => e.GetAddress(nameof(Owner))).Returns(owner);
            MockPersistentState.Setup(e => e.GetUInt64(nameof(TotalSupply))).Returns(_totalSupply);

            return new Airdrop(MockContractState.Object, TokenContractAddress, _totalSupply, EndBlock);
        }
    }
}