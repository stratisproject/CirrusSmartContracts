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
            CurrentBlock = 100;
            Registrations = new Dictionary<string, uint>();
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(TotalSupply), TotalSupply));
            Assert.Equal(TotalSupply, airdrop.TotalSupply);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(EndBlock), EndBlock));
            Assert.Equal(EndBlock, airdrop.EndBlock);

            MockPersistentState.Verify(x => x.SetAddress(nameof(TokenContractAddress), TokenContractAddress));
            Assert.Equal(TokenContractAddress, airdrop.TokenContractAddress);

            MockPersistentState.Verify(x => x.SetAddress(nameof(Owner), Owner));
            Assert.Equal(Owner, airdrop.Owner);
        }

        [Theory]
        [InlineData((uint)Status.NOT_ENROLLED)]
        [InlineData((uint)Status.ENROLLED)]
        [InlineData((uint)Status.FUNDED)]
        public void AccountStatus_Returns_CorrectStatus(uint expectedStatus)
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}")).Returns(expectedStatus);

            Assert.Equal(expectedStatus, airdrop.GetAccountStatus(Registrant));
            MockPersistentState.Verify(x => x.GetUInt32($"Status:{Registrant}"));
        }

        #region Register Tests

        [Fact]
        public void Register_Success()
        {
            var expectedStatus = (uint)Status.ENROLLED;
            ulong expectedNumberOfRegistrants = 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify Register Succeeds
            Assert.True(airdrop.Register());

            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", expectedStatus));
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), expectedNumberOfRegistrants));
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = expectedStatus }), Times.Once);
        }

        [Fact]
        public void Register_Success_WithEndBlock_0_UntilRegistrationIsClosed()
        {
            ulong endBlock = 0;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, endBlock, TotalSupply);

            Assert.True(airdrop.Register());

            // Register with a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));
            Assert.True(airdrop.CanRegister);
            Assert.True(airdrop.Register());

            // Set registration to closed
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(CurrentBlock - 1);

            // Register with a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantThree, 0));
            Assert.False(airdrop.CanRegister);
            Assert.False(airdrop.Register());
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration fails
            Assert.False(airdrop.Register());
            Assert.False(airdrop.CanRegister);
        }

        [Fact]
        public void Register_Fail_AccountAlreadyEnrolled()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration succeeds and the new status is set
            Assert.True(airdrop.Register());
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", (uint)Status.ENROLLED));
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}")).Returns((uint)Status.ENROLLED);

            // Attempt Registration again with the same address
            Assert.False(airdrop.Register());
        }

        [Fact]
        public void Register_Fail_RegistrantIsOwner()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.Equal(airdrop.Owner, airdrop.Message.Sender);
            Assert.False(airdrop.Register());
        }

        [Fact]
        public void Register_Fail_NumberOfRegistrants_IsGreaterThanOrEqualTo_TotalSupply()
        {
            ulong totalSupply = 1;
            ulong numberOfRegistrants = 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, totalSupply);
            // Set the numberOfRegistrants
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);

            // Verify the registration fails
            Assert.False(airdrop.Register());

            // Verify that status, registrationIsClosed, totalSupply and numberOfRegistrations were fetched
            MockPersistentState.Verify(x => x.GetUInt32($"Status:{Registrant}"));
            MockPersistentState.Verify(x => x.GetUInt64(nameof(TotalSupply)));
            MockPersistentState.Verify(x => x.GetUInt64(nameof(NumberOfRegistrants)));

            // Verify registration is not closed
            Assert.True(airdrop.CanRegister);

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
            Assert.False(airdrop.Register());
            // Verify the number of registrants is 1
            numRegistrants = airdrop.NumberOfRegistrants;
            Assert.Equal((ulong)2, numRegistrants);
        }

        // Failing FIX
        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Register with the first registrant
            Assert.True(airdrop.Register());
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1));
            //Assert.Equal((ulong)1, NumberOfRegistrants);

            //MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(NumberOfRegistrants);
            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));
            // Register with the second registrant
            Assert.True(airdrop.Register());
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 2));
            //Assert.Equal((ulong)2, NumberOfRegistrants);

            //MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(NumberOfRegistrants);
            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantThree, 0));
            // Register with the third registrant
            Assert.True(airdrop.Register());
            // Check that the number was incremented by 1
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 3));
            //Assert.Equal((ulong)3, NumberOfRegistrants);
        }

        // Failing FIX
        [Fact] 
        public void Register_Fail_NumberOfRegistrantsNotIncremented()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Check that NumberOfRegistrants starts at 0
            Assert.Equal((ulong)0, NumberOfRegistrants);

            // Verify that the registration succeeds
            Assert.True(airdrop.Register());

            // Verify the NumberOfRegistrants was incremented by 1
            Assert.Equal((ulong)1, NumberOfRegistrants);

            // Verify registration fails if user attempts again
            Assert.False(airdrop.Register());

            // Verify that NumberOfRegistrants was only set once ever
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Once);
            // Verify NumberOfRegistrants is still 1
            Assert.Equal((ulong)1, NumberOfRegistrants);

            // Create a new message from a different registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));

            // Verify the registration succeeds
            Assert.True(airdrop.Register());

            // Verify the numberOfRegistrants was incremented by 1
            Assert.Equal((ulong)2, NumberOfRegistrants);
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

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
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

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
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

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

            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{sender}")).Returns((uint)Status.ENROLLED);

            MockInternalExecutor.Setup(s =>
                s.Call(
                    It.IsAny<ISmartContractState>(),
                    It.IsAny<Address>(),
                    It.IsAny<ulong>(),
                    "TransferFrom",
                    It.IsAny<object[]>(),
                    It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));

            Assert.True(airdrop.Withdraw());

            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Once);

            MockPersistentState.Verify(x => x.SetUInt32($"Status:{sender}", expectedStatus));
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = expectedStatus }), Times.Once);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            Address sender = Registrant;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the number of registrants and the senders status
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{sender}")).Returns((uint)Status.ENROLLED);

            // Verify registration is not closed
            Assert.True(airdrop.CanRegister);

            // Verify the withdrawal fails
            Assert.False(airdrop.Withdraw());

            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Never);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{sender}", (uint)Status.FUNDED), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = (uint)Status.FUNDED }), Times.Never);
        }

        [Theory]
        [InlineData((uint)Status.NOT_ENROLLED)]
        [InlineData((uint)Status.FUNDED)]
        public void Withdraw_Fail_IncorrectAccountStatus(uint status)
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{sender}")).Returns(status);

            Assert.False(airdrop.Withdraw());

            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Never);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{sender}", (uint)Status.FUNDED), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = (uint)Status.FUNDED }), Times.Never);
        }

        [Fact]
        public void Withdraw_Fail_TokenContractAddressTransferFailure()
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}")).Returns((uint)Status.ENROLLED);

            MockInternalExecutor.Setup(s =>
                s.Call(
                    It.IsAny<ISmartContractState>(),
                    It.IsAny<Address>(),
                    It.IsAny<ulong>(),
                    "TransferFrom",
                    It.IsAny<object[]>(),
                    It.IsAny<ulong>()))
                .Returns(TransferResult.Failed());

            // Contract should fail
            Assert.Throws<SmartContractAssertException>(() => airdrop.Withdraw());

            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Once);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", (uint)Status.FUNDED), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = (uint)Status.FUNDED }), Times.Never);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(0, 1)]
        public void Withdraw_Fail_AmountToDistributeIsZero(ulong numberOfRegistrants, ulong totalSupply)
        {
            CurrentBlock = EndBlock + 1;
            Address sender = Registrant;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, totalSupply);

            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);
            MockPersistentState.Setup(x => x.GetUInt32($"Status:{sender}")).Returns((uint)Status.ENROLLED);

            // Verify that the withdrawal fails
            Assert.False(airdrop.Withdraw());

            // Verify the amountToDistribute was checked and remains 0
            Assert.Equal((ulong)0, airdrop.GetAmountToDistribute());

            ulong amountToDistribute = 0;
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Never);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetUInt32($"Status:{Registrant}", (uint)Status.FUNDED), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = (uint)Status.FUNDED }), Times.Never);
        }

        #endregion

        #region Registration Is Closed Tests
        [Fact]
        public void RegistrationIsClosed_IsTrue_IfCurrentBlockIsGreaterThanEndBlock()
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Assert false
            Assert.False(airdrop.CanRegister);
        }

        [Fact]
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanOrEqualToEndBlock()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Assert True
            Assert.True(airdrop.CanRegister);
            // Set CurrentBlock equal to Endblock
            MockContractState.Setup(b => b.Block.Number).Returns(EndBlock);
            // Assert True
            Assert.True(airdrop.CanRegister);
        }

        // Fix manual set
        [Fact] 
        public void CloseRegistration_Success()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.True(airdrop.CanRegister);
            Assert.True(airdrop.CloseRegistration());

            var newEndBlock = CurrentBlock - 1;
            MockPersistentState.Verify(x => x.SetUInt64(nameof(EndBlock), newEndBlock));
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(newEndBlock);

            Assert.False(airdrop.CanRegister);
        }

        // Refactor, more specifics
        [Fact]
        public void CloseRegistration_Fail_SenderIsNotOwner()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.True(airdrop.CanRegister);
            Assert.False(airdrop.CloseRegistration());
            Assert.True(airdrop.CanRegister);
        }
        #endregion

        #region Amount To Distribute Tests

        [Theory]
        [InlineData(100_000, 10, 10_000)]
        [InlineData(10, 3, 3)]
        [InlineData(10, 4, 2)]
        [InlineData(100_000_000_000, 5_456, 18_328_445)]
        public void AmountToDistribute_ReturnsCorrectAmount(ulong totalSupply, ulong numberOfRegistrants, ulong expectedAmountToDistribute)
        {
            CurrentBlock = EndBlock + 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, totalSupply);

            CalculateAmountToDistribute(airdrop, totalSupply, numberOfRegistrants, expectedAmountToDistribute);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetAmountIfRegistrationOpen()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            // Calc totalSupply = 100_000, numberOfRegistrants = 10, expectedAmountToDistribute = 0
            CalculateAmountToDistribute(airdrop, 100_000, 10, 0);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetIfAlreadyCalculated()
        {
            CurrentBlock = EndBlock + 1;
            ulong expectedAmount = 10_000;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
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

        /// <summary>
        /// Initializes an aidrop instance and sets mocks accordingly to interact with Contract and Persistent State.
        /// </summary>
        /// <param name="sender">Address of the sender of the message</param>
        /// <param name="owner">Owner of the contract</param>
        /// <param name="currentBlock">CurrentBlock transaction will run on</param>
        /// <param name="endBlock">Endblock of the airdrop registration period</param>
        /// <param name="totalSupply">TotalSupply that will be airdropped</param>
        /// <returns>Airdrop instance</returns>
        private Airdrop NewAirdrop(Address sender, Address owner, ulong currentBlock, ulong endBlock, ulong totalSupply)
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
            //MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(NumberOfRegistrants);
            // NumberOfRegistrants, when set, increments the NumberOfRegistrations in this test class
            //MockPersistentState.Setup(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()))
            //    .Callback<string, ulong>((key, value) => NumberOfRegistrants = value);

            //// Status returns Status from the registrations dictionary or default Not Enrolled
            //MockPersistentState.Setup(x => x.GetUInt32($"Status:{Registrant}"))
            //    .Returns<string>(key => Registrations.ContainsKey(key) ? Registrations[key] : (uint)Status.NOT_ENROLLED);
            //// Status, when set, updates the registrations dictionary or adds a new key/value pair
            //MockPersistentState.Setup(x => x.SetUInt32($"Status:{Registrant}", It.IsAny<uint>()))
            //    .Callback<string, uint>((key, value) =>
            //    {
            //        if (Registrations.ContainsKey(key)) Registrations[key] = value;
            //        else Registrations.Add(key, value);
            //    });

            MockPersistentState.Invocations.Clear();
            MockInternalExecutor.Invocations.Clear();

            // Return the newly created airdrop instance
            return new Airdrop(MockContractState.Object, TokenContractAddress, totalSupply, endBlock);
        }

        #endregion
    }
}