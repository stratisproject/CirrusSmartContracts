using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static Airdrop;

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
        private readonly ulong NumberOfRegistrants;
        private ulong CurrentBlock;
        private const string EnrolledStatus = "ENROLLED";
        private const string FundedStatus = "FUNDED";

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
        }

        /// <summary>
        /// Initializes an aidrop instance and sets mocks accordingly to interact with Contract and Persistent State.
        /// </summary>
        /// <param name="sender">Address of the sender of the message</param>
        /// <param name="owner">Owner of the contract</param>
        /// <param name="currentBlock">CurrentBlock transaction will run on</param>
        /// <param name="endBlock">Endblock of the airdrop registration period</param>
        /// <param name="totalSupply">TotalSupply that will be airdropped</param>
        /// <returns><see cref="Airdrop"/> instance</returns>
        private Airdrop NewAirdrop(Address sender, Address owner, ulong currentBlock, ulong endBlock, ulong totalSupply)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, sender, 0));
            MockContractState.Setup(b => b.Block.Number).Returns(currentBlock);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(endBlock);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(TotalSupply))).Returns(totalSupply);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Owner))).Returns(owner);
            MockPersistentState.Setup(x => x.GetAddress(nameof(TokenContractAddress))).Returns(TokenContractAddress);

            return new Airdrop(MockContractState.Object, TokenContractAddress, totalSupply, endBlock);
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

        [Fact]
        public void ContractCreationFails_IfTotalSupplyIs_0()
        {
            // Setup Owner to deploy the contract
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, Owner, 0));
            // Assert that an exception is thrown if the total supply is set to 0
            Assert.Throws<SmartContractAssertException>(() => new Airdrop(MockContractState.Object, TokenContractAddress, 0, EndBlock));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(EnrolledStatus)]
        [InlineData(FundedStatus)]
        public void AccountStatus_Returns_CorrectStatus(string expectedStatus)
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);
            MockPersistentState.Setup(x => x.GetString($"Status:{Registrant}")).Returns(expectedStatus);

            Assert.Equal(expectedStatus, airdrop.GetAccountStatus(Registrant));
            MockPersistentState.Verify(x => x.GetString($"Status:{Registrant}"), Times.Once);
        }

        #region Register Tests

        [Fact]
        public void Register_Success()
        {
            var expectedStatus = EnrolledStatus;
            ulong expectedNumberOfRegistrants = 1;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.True(airdrop.Register());

            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", expectedStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), expectedNumberOfRegistrants), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = expectedStatus }), Times.Once);
        }

        [Fact]
        public void Register_Success_WithEndBlock_0_UntilRegistrationIsClosed()
        {
            ulong endBlock = 0;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, endBlock, TotalSupply);

            // First Registrant should succeed
            Assert.True(airdrop.Register());

            // Second Registrant should succeed
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));
            Assert.True(airdrop.CanRegister);
            Assert.True(airdrop.Register());

            // Set registration to closed by setting endblock
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(CurrentBlock - 1);

            // Register with a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantThree, 0));
            Assert.False(airdrop.CanRegister);
            Assert.False(airdrop.Register());

            // Make sure things were only ran twice for successes
            MockPersistentState.Verify(x => x.SetString(It.IsAny<string>(), EnrolledStatus), Times.Exactly(2));
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Exactly(2));
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<StatusLog>()), Times.Exactly(2));
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            CurrentBlock = EndBlock;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration fails
            Assert.False(airdrop.Register());
            Assert.False(airdrop.CanRegister);

            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", EnrolledStatus), Times.Never);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Never);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = EnrolledStatus }), Times.Never);
        }

        [Fact]
        public void Register_Fail_AccountAlreadyEnrolled()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.True(airdrop.Register());
            // Set an account to Enrolled status
            MockPersistentState.Setup(x => x.GetString($"Status:{Registrant}")).Returns(EnrolledStatus);

            // Make sure registration is open but registration fails
            Assert.True(airdrop.CanRegister);
            Assert.False(airdrop.Register());

            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", EnrolledStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = EnrolledStatus }), Times.Once);
        }

        [Fact]
        public void Register_Fail_RegistrantIsOwner()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.Equal(airdrop.Owner, airdrop.Message.Sender);
            Assert.False(airdrop.Register());

            MockPersistentState.Verify(x => x.SetString($"Status:{Owner}", EnrolledStatus), Times.Never);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Never);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Owner, Status = EnrolledStatus }), Times.Never);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        public void Register_Fail_NumberOfRegistrants_IsGreaterThanOrEqualTo_TotalSupply(ulong totalSupply, ulong numberOfRegistrants)
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, totalSupply);

            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);

            // Verify the status remains Not Enrolled or null
            var accountStatus = airdrop.GetAccountStatus(Registrant);
            Assert.True(string.IsNullOrWhiteSpace(accountStatus));

            // Verify the registrant can register
            Assert.True(airdrop.CanRegister);

            // Verify the totalSupply was set
            Assert.Equal(totalSupply, airdrop.TotalSupply);

            // Verify the number of registrants was set
            Assert.Equal(numberOfRegistrants, airdrop.NumberOfRegistrants);

            // Verify totalSupply is greater than or equal to number of registrants
            Assert.True(numberOfRegistrants >= totalSupply);

            // Verify the registration fails
            Assert.False(airdrop.Register());

            // Verify nothing was updated
            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", EnrolledStatus), Times.Never);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Never);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = EnrolledStatus }), Times.Never);
        }

        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Register with the first registrant
            Assert.True(airdrop.Register());
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1));
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));
            Assert.True(airdrop.Register());
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 2));
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(2);

            // New call from a new registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantThree, 0));
            Assert.True(airdrop.Register());
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 3));
        }

        [Fact]
        public void Register_Fail_NumberOfRegistrantsNotIncremented()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify that the registration succeeds
            Assert.True(airdrop.Register());

            // Verify Status and NumberOfRegistrants were set
            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", EnrolledStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Once);

            // Set Persistant state of status and numberOfRegistrants
            MockPersistentState.Setup(x => x.GetString($"Status:{Registrant}")).Returns(EnrolledStatus);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);

            // Verify registration fails if user attempts again
            Assert.False(airdrop.Register());

            // Verify that NumberOfRegistrants was only set once ever
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Once);

            // Create a new message from a different registrant
            MockContractState.Setup(x => x.Message).Returns(new Message(AirdropContractAddress, RegistrantTwo, 0));

            // Verify the registration succeeds with the second registrant
            Assert.True(airdrop.Register());

            // Verify the numberOfRegistrants was set twice ever
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Exactly(2));

            // Verify the second time numberOfRegistrants was set, it was incremented to 2
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 2), Times.Once);
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify the registration succeeds
            var result = airdrop.AddRegistrant(Registrant);
            Assert.True(result);

            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", EnrolledStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), 1), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = EnrolledStatus }), Times.Once);

        }

        [Fact]
        public void AddRegistrant_Fail_SenderIsNotOwner()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.NotEqual(airdrop.Message.Sender, airdrop.Owner);

            // Verify adding a registrant fails
            Assert.False(airdrop.AddRegistrant(RegistrantTwo));

            MockPersistentState.Verify(x => x.SetString($"Status:{RegistrantTwo}", EnrolledStatus), Times.Never);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Never);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = RegistrantTwo, Status = EnrolledStatus }), Times.Never);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderAndRegistrantAreBothOwner()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.Equal(airdrop.Message.Sender, Owner);

            // Verify registration fails when owner tries to register owner
            Assert.False(airdrop.AddRegistrant(Owner));

            MockPersistentState.Verify(x => x.SetString($"Status:{Owner}", EnrolledStatus), Times.Never);
            MockPersistentState.Verify(x => x.SetUInt64(nameof(NumberOfRegistrants), It.IsAny<ulong>()), Times.Never);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Owner, Status = EnrolledStatus }), Times.Never);
        }

        #endregion

        #region Withdraw Tests

        [Fact]
        public void Withdraw_Success()
        {
            CurrentBlock = EndBlock;
            var sender = Registrant;
            var expectedStatus = FundedStatus;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Setup sender as already registered
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetString($"Status:{sender}")).Returns(EnrolledStatus);

            // Mock contract call
            MockInternalExecutor.Setup(s =>
                s.Call(
                    It.IsAny<ISmartContractState>(),
                    It.IsAny<Address>(),
                    It.IsAny<ulong>(),
                    "TransferFrom",
                    It.IsAny<object[]>(),
                    It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));

            // Withdraw should succeed
            Assert.True(airdrop.Withdraw());

            // Verify the withdraw method made the call, set the new status, and logged the result
            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Once);
            MockPersistentState.Verify(x => x.SetString($"Status:{sender}", expectedStatus));
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = expectedStatus }), Times.Once);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            var sender = Registrant;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the number of registrants and the senders status
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetString($"Status:{sender}")).Returns(EnrolledStatus);

            // Verify registration is open
            Assert.True(airdrop.CanRegister);

            // Verify the withdrawal fails
            Assert.False(airdrop.Withdraw());

            // Verify the call was never made
            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s =>
                s.Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Never);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetString($"Status:{sender}", FundedStatus), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = FundedStatus }), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(FundedStatus)]
        public void Withdraw_Fail_IncorrectAccountStatus(string status)
        {
            CurrentBlock = EndBlock;
            var sender = Registrant;
            var airdrop = NewAirdrop(sender, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Setup sender as incorrect status for withdraw
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetString($"Status:{sender}")).Returns(status);

            // Verify withdraw fails
            Assert.False(airdrop.Withdraw());

            // Verify the call was never made
            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Never);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetString($"Status:{sender}", FundedStatus), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = sender, Status = FundedStatus }), Times.Never);
        }

        [Fact]
        public void Withdraw_Fail_TokenContractAddressTransferFailure()
        {
            CurrentBlock = EndBlock;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set the initial NumberOfRegistrants to 1
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(1);
            MockPersistentState.Setup(x => x.GetString($"Status:{Registrant}")).Returns(EnrolledStatus);

            // Mock the contract call to return a failure
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

            // Verify the call was made only once
            var amountToDistribute = airdrop.GetAmountToDistribute();
            var transferFromParams = new object[] { Owner, Registrant, amountToDistribute };
            MockInternalExecutor.Verify(s => s
                .Call(MockContractState.Object, TokenContractAddress, amountToDistribute, "TransferFrom", transferFromParams, 0), Times.Once);

            // Make sure the status was never changed
            MockPersistentState.Verify(x => x.SetString($"Status:{Registrant}", FundedStatus), Times.Never);

            // Make sure the status update was never logged
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = Registrant, Status = FundedStatus }), Times.Never);
        }

        #endregion

        #region Registration Is Closed Tests

        [Theory]
        [InlineData(10, 10)]
        [InlineData(11, 10)]
        public void CanRegister_IsFalse_IfCurrentBlock_IsGreaterThan_OrEqualTo_EndBlock(ulong currentBlock, ulong endBlock)
        {
            var airdrop = NewAirdrop(Owner, Owner, currentBlock, endBlock, TotalSupply);

            Assert.False(airdrop.CanRegister);
            Assert.False(airdrop.Register());
        }

        [Fact]
        public void CanRegister_IsTrue_IfCurrentBlock_IsLessThan_EndBlock()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.True(airdrop.CanRegister);
            Assert.True(airdrop.Register());
        }

        [Fact]
        public void CloseRegistration_Success()
        {
            var airdrop = NewAirdrop(Owner, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Verify owner is sender, canRegister is true initially, and close registration succeeds
            Assert.Equal(Owner, airdrop.Message.Sender);
            Assert.True(airdrop.CanRegister);
            Assert.True(airdrop.CloseRegistration());

            MockPersistentState.Verify(x => x.SetUInt64(nameof(EndBlock), CurrentBlock), Times.Once);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndBlock))).Returns(CurrentBlock);

            // Verify canRegiter is now false and registration fails
            Assert.False(airdrop.CanRegister);
            Assert.False(airdrop.AddRegistrant(Registrant));
        }

        [Fact]
        public void CloseRegistration_Fail_SenderIsNotOwner()
        {
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Users can register
            Assert.True(airdrop.CanRegister);
            // Sender is not owner
            Assert.NotEqual(Owner, airdrop.Message.Sender);
            // Close registration fails
            Assert.False(airdrop.CloseRegistration());
            // Users can still register
            Assert.True(airdrop.CanRegister);
            // Endblock still equal to original endblock
            Assert.Equal(EndBlock, airdrop.EndBlock);
            // Verify endblock was never changed
            MockPersistentState.Verify(x => x.SetUInt64(nameof(EndBlock), CurrentBlock), Times.Never);
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
            CurrentBlock = EndBlock;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, totalSupply);

            // Set NumberOfRegistrants from parameters
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(numberOfRegistrants);

            // Assert the expected amount equals the actual
            Assert.Equal(expectedAmountToDistribute, airdrop.GetAmountToDistribute());

            // verify the amountToDistribute was successfully set
            MockPersistentState.Verify(x => x.SetUInt64("AmountToDistribute", expectedAmountToDistribute), Times.Once);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetAmountIfRegistrationOpen()
        {
            ulong expectedAmountToDistribute = 0;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set NumberOfRegistrants to 10
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            // Assert the expected amount equals the actual
            Assert.Equal(expectedAmountToDistribute, airdrop.GetAmountToDistribute());

            // Verify this is never set
            MockPersistentState.Verify(x => x.SetUInt64("AmountToDistribute", It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void AmountToDistribute_Returns_0_IfNoRegistrants()
        {
            CurrentBlock = EndBlock;
            ulong expectedAmountToDistribute = 0;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            Assert.False(airdrop.CanRegister);

            // Set NumberOfRegistrants to 0
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(0);

            // Assert the expected amount equals the actual
            Assert.Equal(expectedAmountToDistribute, airdrop.GetAmountToDistribute());

            // Verify this is never set
            MockPersistentState.Verify(x => x.SetUInt64("AmountToDistribute", It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void AmountToDistribute_DoesNotSetIfAlreadyCalculated()
        {
            CurrentBlock = EndBlock;
            ulong expectedAmountToDistribute = 10_000;
            var airdrop = NewAirdrop(Registrant, Owner, CurrentBlock, EndBlock, TotalSupply);

            // Set NumberOfRegistrants to 10
            MockPersistentState.Setup(x => x.GetUInt64(nameof(NumberOfRegistrants))).Returns(10);

            // Get the amount the first time
            var amountToDistribute = airdrop.GetAmountToDistribute();

            // Assert the amounts match
            Assert.Equal(expectedAmountToDistribute, amountToDistribute);

            // Simulate AmountToDistrubute to already set to expectedAmount
            MockPersistentState.Setup(x => x.GetUInt64("AmountToDistribute")).Returns(expectedAmountToDistribute);

            // Run again a second time
            amountToDistribute = airdrop.GetAmountToDistribute();

            // Verify that the amount was only ever set once
            MockPersistentState.Verify(x => x.SetUInt64("AmountToDistribute", expectedAmountToDistribute), Times.Once);
        }

        #endregion
    }
}