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
        private readonly Address RegistrantTwo;
        private readonly Address RegistrantThree;
        private readonly Address TokenContractAddress;
        private readonly Address AirdropContractAddress;
        private readonly ulong TotalSupply;
        private readonly ulong EndBlock;
        private ulong NumberOfRegistrants;
        private ulong CurrentBlock;
        private Dictionary<string, uint> Registrations;
        private bool RegistrationIsClosed;

        public AirdropTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            Registrant = "0x0000000000000000000000000000000000000002".HexToAddress();
            RegistrantTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            RegistrantThree = "0x0000000000000000000000000000000000000004".HexToAddress();
            TokenContractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
            AirdropContractAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
            TotalSupply = 100_000;
            EndBlock = 1_000_000;
            NumberOfRegistrants = 0;
            CurrentBlock = 1;
            Registrations = new Dictionary<string, uint>();
            RegistrationIsClosed = false;
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify that PersistentState was called to set the TotalSupply
            MockPersistentState.Verify(x => x.SetUInt64(nameof(TotalSupply), TotalSupply));
            Assert.Equal(TotalSupply, airdrop.TotalSupply);

            // Verify that PersistentState was called to set the EndBlock
            MockPersistentState.Verify(x => x.SetUInt64(nameof(EndBlock), EndBlock));
            Assert.Equal(EndBlock, airdrop.EndBlock);

            // Verify that PersistentState was called to set the TokenContractAddress
            MockPersistentState.Verify(x => x.SetAddress(nameof(TokenContractAddress), TokenContractAddress));
            Assert.Equal(TokenContractAddress, airdrop.TokenContractAddress);

            // Verify that PersistentState was called to set the Owner
            MockPersistentState.Verify(x => x.SetAddress(nameof(Owner), Owner));
            Assert.Equal(Owner, airdrop.Owner);
        }

        [Fact]
        public void AccountStatus_Returns_CorrectStatus()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Not Enrolled registrant
            var status = airdrop.GetAccountStatus(Registrant);
            MockPersistentState.Verify(x => x.GetUInt32($"Status:{Registrant}"));
            Assert.Equal((uint)Status.NOT_ENROLLED, status);

            // Set and check for Enrolled registrants
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}")).Returns((uint)Status.ENROLLED);
            status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal((uint)Status.ENROLLED, status);

            // Set and check for Funded registrants
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}")).Returns((uint)Status.FUNDED);
            status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal((uint)Status.FUNDED, status);
        }

        #region Register Tests

        [Fact]
        public void Register_Success()
        {
            var expectedStatus = (uint)Status.ENROLLED;
            ulong expectedNumberOfRegistrants = 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify Register Succeeds
            var result = airdrop.Register();
            Assert.True(result);

            // Verfies the new status was set to Enrolled
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", expectedStatus));
            var status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal(expectedStatus, status);

            // Verify Number of registrants was incremented
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), expectedNumberOfRegistrants));
            Assert.Equal(expectedNumberOfRegistrants, NumberOfRegistrants);

            // Verify the new status was logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = expectedStatus }));
        }

        [Fact]
        public void Register_Success_WithEndBlock_0_UntilRegistrationIsClosed()
        {
            ulong endBlock = 0;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, endBlock, TotalSupply);

            // Verify the registration was successfull
            var result = airdrop.Register();
            Assert.True(result);

            // Set registration to closed
            MockPersistentState.Setup(x => x.GetBool(nameof(RegistrationIsClosed))).Returns(true);

            // Register with a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));

            // Verify the registration failed
            result = airdrop.Register();
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            CurrentBlock = EndBlock + 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration fails
            var result = airdrop.Register();
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_AccountAlreadyEnrolled()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration succeeds and the new status is set
            var result = airdrop.Register();
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", (uint)Status.ENROLLED));
            Assert.True(result);

            // Attempt Registration again with the same address
            result = airdrop.Register();
            Assert.False(result);

            // Check that the useres status remains Enrolled
            var status = airdrop.GetAccountStatus(Registrant);
            Assert.Equal((uint)Status.ENROLLED, status);
        }

        [Fact]
        public void Register_Fail_RegistrantIsOwner()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration fails
            var result = airdrop.Register();
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_NumberOfRegistrants_IsGreaterThanOrEqualTo_TotalSupply()
        {
            ulong totalSupply = 1;
            ulong numberOfRegistrants = 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, totalSupply);
            // Set the numberOfRegistrants
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);

            // Verify the registration fails
            var result = airdrop.Register();
            Assert.False(result);

            // Verify that status, registrationIsClosed, totalSupply and numberOfRegistrations were fetched
            MockPersistentState.Verify(x => x.GetUInt32($"Status:{Registrant}"));
            MockPersistentState.Verify(x => x.GetBool(nameof(RegistrationIsClosed)));
            MockPersistentState.Verify(x => x.GetUInt64(nameof(TotalSupply)));
            MockPersistentState.Verify(x => x.GetUInt64(nameof(NumberOfRegistrants)));

            // Verify registration is not closed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            Assert.False(registrationIsClosed);

            // Verify the status remains Not Enrolled
            var accountStatus = airdrop.GetAccountStatus(Registrant);
            Assert.Equal((uint)Status.NOT_ENROLLED, accountStatus);

            // Verify the total supply is 1
            totalSupply = airdrop.TotalSupply;
            Assert.Equal((ulong)1, totalSupply);

            // Verify the number of registrants is 1
            var numRegistrants = airdrop.NumberOfRegistrants;
            Assert.Equal((ulong)1, numRegistrants);

            // Set the numberOfRegistrants
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(2);
            // Verify the registration fails
            result = airdrop.Register();
            Assert.False(result);
            // Verify the number of registrants is 1
            numRegistrants = airdrop.NumberOfRegistrants;
            Assert.Equal((ulong)2, numRegistrants);
        }

        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Register with the first registrant
            var result = airdrop.Register();
            Assert.True(result);
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), airdrop.NumberOfRegistrants + 1));
            Assert.Equal((ulong)1, NumberOfRegistrants);

            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));
            // Register with the second registrant
            result = airdrop.Register();
            Assert.True(result);
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), airdrop.NumberOfRegistrants + 1));
            Assert.Equal((ulong)2, NumberOfRegistrants);

            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantThree, 0));
            // Register with the third registrant
            result = airdrop.Register();
            Assert.True(result);
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), airdrop.NumberOfRegistrants + 1));
            Assert.Equal((ulong)3, NumberOfRegistrants);
        }

        [Fact]
        public void Register_Fail_NumberOfRegistrantsNotIncremented()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Check that NumberOfRegistrants starts at 0
            Assert.Equal((ulong)0, NumberOfRegistrants);

            // Verify that the registration succeeds
            var result = airdrop.Register();
            Assert.True(result);

            // Verify the NumberOfRegistrants was incremented by 1
            Assert.Equal((ulong)1, NumberOfRegistrants);

            // Verify registration fails if user attempts again
            result = airdrop.Register();
            Assert.False(result);

            // Verify that NumberOfRegistrants was only set once ever
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Once);
            // Verify NumberOfRegistrants is still 1
            Assert.Equal((ulong)1, NumberOfRegistrants);

            // Create a new message from a different registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));

            // Verify the registration succeeds
            result = airdrop.Register();
            Assert.True(result);

            // Verify the numberOfRegistrants was incremented by 1
            Assert.Equal((ulong)2, NumberOfRegistrants);
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration succeeds
            var result = airdrop.AddRegistrant(Registrant);
            Assert.True(result);

            // Verify the status of the registrant was set successfully
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", (uint)Status.ENROLLED));
            // Verify the number of registrants was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1));
        }

        [Fact]
        public void AddRegistrant_Fail_SenderIsNotOwner()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify adding a registrant fails
            var result = airdrop.AddRegistrant(RegistrantTwo);
            Assert.False(result);

            // Verify that RegistrantTwo's status is still Not_Enrolled
            var status = airdrop.GetAccountStatus(RegistrantTwo);
            Assert.Equal((uint)Status.NOT_ENROLLED, status);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderAndRegistrantAreBothOwner()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify registration fails when owner tries to register owner
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
            var expectedStatus = (uint)Status.FUNDED;

            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                TransferResult = new TransferResult(success: true)
            };

            // Verify the withdrawal succeeds
            var result = MockWithdrawExecute(withdrawParams);
            Assert.True(result);
            
            // Verify the senders status has been updated
            var status = airdrop.GetAccountStatus(sender);
            Assert.Equal(expectedStatus, status);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            Address sender = Registrant;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                TransferResult = new TransferResult(success: true)
            };

            // Verify registration is not closed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            Assert.False(registrationIsClosed);

            // Verify the withdrawal fails
            var result = MockWithdrawExecute(withdrawParams);
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_IncorrectAccountStatus()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.FUNDED,
                ExpectedAmountToDistribute = TotalSupply,
                TransferResult = new TransferResult(success: true)
            };

            // Verify the withdrawal fails with Funded Status
            var result = MockWithdrawExecute(withdrawParams);
            Assert.False(result);

            // Update the status of the params to Not Enrolled
            withdrawParams.Status = (uint)Status.NOT_ENROLLED;
            result = MockWithdrawExecute(withdrawParams);

            // Verify the withdrawal fails with Not Enrolled Status
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_TokenContractAddressTransferFailure()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                TransferResult = new TransferResult(success: false)
            };

            // Contract should fail
            var result = MockWithdrawExecute(withdrawParams);
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_AmountToDistributeIsZero()
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            ulong totalSupply = 0;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, totalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.ENROLLED,
                ExpectedAmountToDistribute = 0,
                TransferResult = new TransferResult(success: false)
            };

            // Verify that the withdrawal fails
            var result = MockWithdrawExecute(withdrawParams);
            Assert.False(result);

            // Verify the amountToDistribute was checked and remains 0
            MockPersistentState.Verify(x => x.GetUInt64("AmountToDistribute"));
            Assert.Equal((ulong)0, airdrop.GetAmountToDistribute());
        }

        [Fact]
        public void Withdraw_Fail_DoesNotUpdateStatus()
        {
            Address sender = Registrant;
            CurrentBlock = EndBlock + 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(sender, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Set parameters used within the MockWithdrawExecute method
            var withdrawParams = new MockWithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = (uint)Status.ENROLLED,
                ExpectedAmountToDistribute = TotalSupply,
                TransferResult = new TransferResult(success: false)
            };

            // Contract should fail
            var result = MockWithdrawExecute(withdrawParams);
            Assert.False(result);

            // Verify the account status is still Enrolled
            var accountStatus = airdrop.GetAccountStatus(Registrant);
            Assert.Equal((uint)Status.ENROLLED, accountStatus);
        }

        /// <summary>
        /// Mocks the withdraw method in the contract, difference is using the MockInternalExecutor.
        /// </summary>
        /// <param name="withdrawParams">see <see cref="MockWithdrawExecuteParams"/></param>
        /// <returns>Returns bool success</returns>
        private bool MockWithdrawExecute(MockWithdrawExecuteParams withdrawParams)
        {
            // Set status for sender specified by params
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{withdrawParams.Sender}")).Returns(withdrawParams.Status);
            // Get the acount status from the contract method
            var accountStatus = withdrawParams.Airdrop.GetAccountStatus(withdrawParams.Sender);
            // Ensure the status was retrieved from persistant state
            MockPersistentState.Verify(x => x.GetUInt32($"Status:{withdrawParams.Sender}"));
            // Fail if account status is not enrolled
            if (accountStatus != (uint)Status.ENROLLED)
            {
                return false;
            }

            // Check if the airdrops registration is closed.
            var registrationIsClosed = withdrawParams.Airdrop.IsRegistrationClosed();
            // Ensure persistent state is checked
            MockPersistentState.Verify(x => x.GetBool(nameof(RegistrationIsClosed)));
            // If registration is closed, fail
            if (registrationIsClosed == false)
            {
                return false;
            }

            // Get the amount to distribute from the contract method
            var amountToDistribute = withdrawParams.Airdrop.GetAmountToDistribute();
            // Ensure the amount to distribute was retrieved from persistant state
            MockPersistentState.Verify(x => x.GetUInt64("AmountToDistribute"));
            // Assert the amount to distibute is whats expected
            Assert.True(amountToDistribute == withdrawParams.ExpectedAmountToDistribute);
            // Create transfer params to use in mock call
            var transferParams = new object[] { withdrawParams.Sender, amountToDistribute };

            // Mock the calls expected result
            MockInternalExecutor.Setup(x => x
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferTo", transferParams, 10_000))
                .Returns(withdrawParams.TransferResult);

            // Make call and retrieve the result
            var result = MockContractState.Object.InternalTransactionExecutor
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferTo", transferParams, 10_000);
            // Fail if result was not successful
            if (result.Success == false)
            {
                return false;
            }

            // Act as normal method would, set FUNDED account status
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{withdrawParams.Sender}")).Returns((uint)Status.FUNDED);
            // Act as normal method would, log the new status
            MockContractLogger.Setup(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = withdrawParams.Sender, Status = (uint)Status.FUNDED })).Verifiable();

            return true;
        }
        #endregion

        #region Registration Is Closed Tests

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfPersistentStateIsTrue()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Set RegistrationIsClosed to true
            MockPersistentState.Setup(x => x.GetBool(nameof(RegistrationIsClosed))).Returns(true);
            // Get RegistrationIsClosed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert true
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsTrue_IfCurrentBlockIsGreaterThanEndBlock()
        {
            CurrentBlock = EndBlock + 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Get RegistrationIsClosed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert true
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanOrEqualToEndBlock()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Get RegistrationIsClosed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert False
            Assert.False(registrationIsClosed);
            // Set CurrentBlock equal to Endblock
            MockContractState.Setup(b => b.Block.Number).Returns(EndBlock);
            // Get RegistrationIsClosed
            registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert False
            Assert.False(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Success()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Assert False
            Assert.False(airdrop.IsRegistrationClosed());
            // Close Registration
            var result = airdrop.CloseRegistration();
            // Assert True
            Assert.True(result);
            // Verify RegistrationIsClosed was set to True
            MockPersistentState.Verify(x => x.SetBool(nameof(RegistrationIsClosed), true));
            // Set RegistrationIsClosed to true
            MockPersistentState.Setup(x => x.GetBool(nameof(RegistrationIsClosed))).Returns(true);
            // Get RegistrationIsClosed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert True
            Assert.True(registrationIsClosed);
        }

        [Fact]
        public void CloseRegistration_Fail_SenderIsNotOwner()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Assert False
            Assert.False(airdrop.IsRegistrationClosed());
            // Close Registration
            var result = airdrop.CloseRegistration();
            // Assert False
            Assert.False(result);
            // Get RegistrationIsClosed
            var registrationIsClosed = airdrop.IsRegistrationClosed();
            // Assert False
            Assert.False(registrationIsClosed);
        }
        #endregion

        #region Amount To Distribute Tests
        [Fact]
        public void AmountToDistribute_ReturnsCorrectAmount()
        {
            CurrentBlock = EndBlock + 1;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Calc totalSupply = 100_000, numberOfRegistrants = 10, expectedAmountToDistribute = 10_000
            CalculateAmountToDistribute(airdrop, 100_000, 10, 10_000);
            // Calc totalSupply = 10, numberOfRegistrants = 3, expectedAmountToDistribute = 3
            CalculateAmountToDistribute(airdrop, 10, 3, 3);
            // Calc totalSupply = 10, numberOfRegistrants = 4, expectedAmountToDistribute = 2
            CalculateAmountToDistribute(airdrop, 10, 4, 2);
            // Calc totalSupply = 100_000_000_000, numberOfRegistrants = 5456, expectedAmountToDistribute = 18328445
            CalculateAmountToDistribute(airdrop, 100_000_000_000, 5456, 18328445);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetAmountIfRegistrationOpen()
        {
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Calc totalSupply = 100_000, numberOfRegistrants = 10, expectedAmountToDistribute = 0
            CalculateAmountToDistribute(airdrop, 100_000, 10, 0);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetIfAlreadyCalculated()
        {
            CurrentBlock = EndBlock + 1;
            ulong expectedAmount = 10_000;
            // Initialize Airdrop Tests
            var airdrop = InitializeTest(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Calc totalSupply = 100_000, numberOfRegistrants = 10, expectedAmountToDistribute = 10_000
            CalculateAmountToDistribute(airdrop, 100_000, 10, expectedAmount);

            // verify the amount was called from persistant state
            MockPersistentState.Verify(x => x.GetUInt64("AmountToDistribute"));
            // verify the amount was set with the correct amount
            MockPersistentState.Verify(x => x.SetUInt64("AmountToDistribute", expectedAmount));
            // Set the amount in persistant state
            MockPersistentState.Setup(x => x.GetUInt64("AmountToDistribute")).Returns(expectedAmount);

            // Get the AmountToDistribute
            ulong amountToDistribute = airdrop.GetAmountToDistribute();
            // Starting with 100_000 tokens / 10 registrants = 10_000 each
            Assert.Equal(expectedAmount, amountToDistribute);

            // Calc totalSupply = 100_000, numberOfRegistrants = 1, expectedAmountToDistribute = 10_000
            CalculateAmountToDistribute(airdrop, 100_000, 1, expectedAmount);
            // Get the amountToDistribute again
            amountToDistribute = airdrop.GetAmountToDistribute();
            // Should equal the amount before, ignoring any new changes
            Assert.Equal(expectedAmount, amountToDistribute);
        }

        /// <summary>
        /// Takes parameters, sets persistant state, and asserts that the amountToDistribute equals the expectedAmountToDistribute.
        /// </summary>
        /// <param name="airdrop">The airdrop instance to work with</param>
        /// <param name="totalSupply">totalSupply to calculate against</param>
        /// <param name="numberOfRegistrants">numberOfRegistrants to calculate against</param>
        /// <param name="expectedAmountToDistribute">expected amountToDistribute used in Assert</param>
        private void CalculateAmountToDistribute(Airdrop airdrop, ulong totalSupply, ulong numberOfRegistrants, ulong expectedAmountToDistribute)
        {
            // Set TotalSupply from parameters in persistent state
            MockPersistentState.Setup(x => x.GetUInt64(nameof(TotalSupply))).Returns(totalSupply);
            // Set NumberOfRegistrants from parameters in persistent state
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);
            // Get amountToDistribute
            var amountToDistribute = airdrop.GetAmountToDistribute();
            // Assert the expected amount equals the actual
            Assert.Equal(expectedAmountToDistribute, amountToDistribute);
        }
        #endregion

        #region Helpers

        private class TransferResult : ITransferResult
        {
            public TransferResult(bool success = true)
            {
                Success = success;
            }

            public object ReturnValue => null;
            public bool Success { get; set; }
        }

        private struct MockWithdrawExecuteParams
        {
            public Address Sender;
            public Airdrop Airdrop;
            public uint Status;
            public ulong ExpectedAmountToDistribute;
            public TransferResult TransferResult;
        }

        /// <summary>
        /// Initializes an aidrop instance and sets mocks accordingly to interact with Contract and Persistent State.
        /// </summary>
        /// <param name="sender">Address of the sender of the message</param>
        /// <param name="owner">Owner of the contract</param>
        /// <param name="currentBlock">CurrentBlock transaction will run on</param>
        /// <param name="endBlock">Endblock of the airdrop registration period</param>
        /// <param name="totalSupply">TotalSupply that will be airdropped</param>
        /// <returns>Airdrop instance</returns>
        private Airdrop InitializeTest(Address sender, Address owner, ulong currentBlock, ulong endBlock, ulong totalSupply)
        {
            // Set the new call from the sender to the contract
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, sender, 0));
            // Block.Number returns currentBlock supplied by param
            MockContractState.Setup(b => b.Block.Number).Returns(currentBlock);
            // EndBlock returns endblock supplied by param
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(endBlock);
            // TotalSupply returns totalSupply supplied by param
            MockPersistentState.Setup(x => x.GetUInt64(nameof(TotalSupply))).Returns(totalSupply);
            // Owner returns owner supplied by param
            MockPersistentState.Setup(x => x.GetAddress(nameof(Owner))).Returns(owner);
            // TokenContractAddress returns tokenContractAddress supplied by param
            MockPersistentState.Setup(x => x.GetAddress(nameof(TokenContractAddress))).Returns(TokenContractAddress);

            // NumberOfRegistrants returns NumberOfRegistrants from this test class
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(NumberOfRegistrants);
            // NumberOfRegistrants, when set, increments the NumberOfRegistrations in this test class
            MockPersistentState.Setup(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()))
                .Callback<string, ulong>((key, value) => { NumberOfRegistrants += value; });

            // Status returns Status from the registrations dictionary or default Not Enrolled
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}"))
                .Returns<string>(key => Registrations.ContainsKey(key) ? Registrations[key] : (uint)Status.NOT_ENROLLED);
            // Status, when set, updates the registrations dictionary or adds a new key/value pair
            MockPersistentState.Setup(x => x.SetUInt32($"Status:{Registrant}", It.IsAny<uint>()))
                .Callback<string, uint>((key, value) =>
                {
                    if (Registrations.ContainsKey(key)) Registrations[key] = value;
                    else Registrations.Add(key, value);
                });

            // Return the newly created airdrop instance
            return new Airdrop(MockContractState.Object, TokenContractAddress, totalSupply, endBlock);
        }

        #endregion
    }
}