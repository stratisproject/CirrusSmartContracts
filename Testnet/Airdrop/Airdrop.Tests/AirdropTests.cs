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
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            // Verify that PersistentState was called with the total supply, name, symbol and endblock
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.TotalSupply), this.totalSupply));
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.EndBlock), this.endBlock));
            this.mockPersistentState.Verify(s => s.SetAddress(nameof(Airdrop.TokenContractAddress), this.tokenContract));
            this.mockPersistentState.Verify(s => s.SetAddress(nameof(Airdrop.Owner), this.airdropContractOwner));
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
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.ENROLLED));

            Assert.True(result);

            // Verfies the new status was set
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.ENROLLED));
            // Verify the new status was logged
            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = this.registrant, Status = Status.ENROLLED }));
        }

        [Fact]
        public void Register_Success_WithEndBlock_0_UntilRegistrationIsClosed()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(0);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, 0);

            var result = airdrop.Register();

            Assert.True(result);

            this.mockPersistentState.Setup(e => e.GetBool("RegistrationIsClosed")).Returns(true);

            result = airdrop.Register();

            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_AirdropClosed()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

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
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            // Verify the status was set for the registrant to Enrolled
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.ENROLLED));

            // Assert status succeeds
            Assert.True(result);

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{this.registrant}")).Returns(Status.ENROLLED);

            result = airdrop.Register();

            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_RegistrantIsOwner()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.Register();

            // Assert status succeeds
            Assert.False(result);
        }

        [Fact]
        public void Register_Fail_MoreRegistrantsThanSupply()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(10);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(1);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, 1, this.endBlock);

            var result = airdrop.Register();
            Assert.False(result);

            this.mockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{this.registrant}"));
            this.mockPersistentState.Verify(s => s.GetBool($"RegistrationIsClosed"));
            this.mockPersistentState.Verify(s => s.GetUInt64($"TotalSupply"));
            this.mockPersistentState.Verify(s => s.GetUInt64($"NumberOfRegistrants"));

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.False(registrationIsClosed);

            var accountStatus = airdrop.GetAccountStatus(this.registrant);
            Assert.Equal(Status.NOT_ENROLLED, accountStatus);

            var _totalSupply = airdrop.TotalSupply;
            Assert.Equal((ulong)1, _totalSupply);

            var numRegistrants = airdrop.NumberOfRegistrants;
            Assert.Equal((ulong)10, numRegistrants);
        }

        [Fact]
        public void Register_Success_NumberOfRegistrantsIncrement()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(this.index);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

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
        public void Register_Fail_NumberOfRegistrantsNotIncremented()
        {
            ulong totalNumberOfRegistrants = 0;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(() => { return totalNumberOfRegistrants; });
            this.mockPersistentState.Setup(e => e.SetUInt64("NumberOfRegistrants", It.IsAny<ulong>())).Callback<string, ulong>((key, value) => { totalNumberOfRegistrants = value; });
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            Dictionary<string, Status> registrations = new Dictionary<string, Status>();
            this.mockPersistentState.Setup(e => e.GetStruct<Status>($"Status:{this.registrant}"))
                .Returns<string>((key) =>
                {
                    if (registrations.ContainsKey(key))
                    {
                        return registrations[key];
                    }

                    return Status.NOT_ENROLLED;
                });

            this.mockPersistentState.Setup(e => e.SetStruct($"Status:{this.registrant}", It.IsAny<Status>()))
                .Callback<string, Status>((key, value) =>
                {
                    if (registrations.ContainsKey(key))
                    {
                        registrations[key] = value;
                    }
                    else
                    {
                        registrations.Add(key, value);
                    }
                });

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            Assert.Equal((ulong)0, totalNumberOfRegistrants);
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));

            // call first time with registrant that is not registered yet.
            var result = airdrop.Register();

            Assert.True(result);
            this.mockPersistentState.Verify(s => s.SetUInt64("NumberOfRegistrants", It.IsAny<ulong>()), Times.Once);
            Assert.Equal((ulong)1, totalNumberOfRegistrants);

            // call second time with same registrant that is already registered.
            result = airdrop.Register();
            Assert.False(result);
            Assert.Equal((ulong)1, totalNumberOfRegistrants);

            // verify it's not called more times than before. It should still be only once.
            this.mockPersistentState.Verify(s => s.SetUInt64("NumberOfRegistrants", It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void AddRegistrant_Success()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.AddRegistrant(this.registrant);

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.registrant}", Status.ENROLLED));

            Assert.True(result);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderIsNotOwner()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.AddRegistrant(this.registrant);

            Assert.False(result);
        }

        [Fact]
        public void AddRegistrant_Fail_SenderAndRegistrantAreBothOwner()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.airdropContractOwner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var result = airdrop.AddRegistrant(this.airdropContractOwner);

            Assert.False(result);
        }
        #endregion

        #region Withdraw Tests

        [Fact]
        public void Withdraw_Success()
        {
            Address sender = this.registrant;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, sender, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = this.totalSupply,
                transferResult = new TransferResult(success: true)
            };

            var result = WithdrawExecute(withdrawParams);

            Assert.True(result);
        }

        [Fact]
        public void Withdraw_Fail_AirdropStillOpen()
        {
            Address sender = this.registrant;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, sender, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = this.totalSupply,
                transferResult = new TransferResult(success: true)
            };

            var result = WithdrawExecute(withdrawParams);

            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_IncorrectAccountStatus()
        {
            Address sender = this.registrant;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, sender, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.FUNDED,
                ExpectedAmountToDistribute = this.totalSupply,
                transferResult = new TransferResult(success: true)
            };
            var result = WithdrawExecute(withdrawParams);
            Assert.False(result);

            withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.NOT_ENROLLED,
                ExpectedAmountToDistribute = this.totalSupply,
                transferResult = new TransferResult(success: true)
            };
            result = WithdrawExecute(withdrawParams);
            Assert.False(result);
        }

        [Fact]
        public void Withdraw_Fail_TokenContractTransferFailure()
        {
            Address sender = this.registrant;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, sender, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(this.totalSupply);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = this.totalSupply,
                transferResult = new TransferResult(success: false)
            };

            var result = WithdrawExecute(withdrawParams);

            // Contract should fail
            Assert.False(result);

            var accountStatus = airdrop.GetAccountStatus(this.registrant);

            // Status should not be set to funded for user
            Assert.Equal(Status.ENROLLED, accountStatus);
        }

        [Fact]
        public void Withdraw_Fail_AmountToDistributesZero()
        {
            Address sender = this.registrant;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, sender, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1_000_001);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(0);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var withdrawParams = new WithdrawExecuteParams
            {
                Sender = sender,
                Airdrop = airdrop,
                Status = Status.ENROLLED,
                ExpectedAmountToDistribute = 0,
                transferResult = new TransferResult(success: false)
            };

            var result = WithdrawExecute(withdrawParams);

            this.mockPersistentState.Verify(e => e.GetUInt64("AmountToDistribute"));
            Assert.Equal((ulong)0, airdrop.AmountToDistribute);

            // Contract should fail
            Assert.False(result);
        }

        private bool WithdrawExecute(WithdrawExecuteParams withdrawParams)
        {
            // Set status specified by param
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}")).Returns(withdrawParams.Status);
            // Get the acount status from the contract method
            var accountStatus = withdrawParams.Airdrop.GetAccountStatus(withdrawParams.Sender);
            // Ensure the status was retrieved from persistant state
            this.mockPersistentState.Verify(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}"));
            // Fail if account status is not enrolled
            if (accountStatus != Status.ENROLLED)
            {
                return false;
            }

            // Check if the airdrops registration is cold.
            var registrationIsClosed = withdrawParams.Airdrop.RegistrationIsClosed;
            // Ensure persistent state is checked
            this.mockPersistentState.Verify(s => s.GetBool($"RegistrationIsClosed"));
            // If registration is closed, fail
            if (registrationIsClosed == false)
            {
                return false;
            }

            // Get the amount to distribute from the contract method
            var amountToDistribute = withdrawParams.Airdrop.AmountToDistribute;
            // Ensure the amount to distribute was retrieved from persistant state
            this.mockPersistentState.Verify(s => s.GetUInt64($"AmountToDistribute"));
            // Assert the amount to distibute is whats expected
            Assert.True(amountToDistribute == withdrawParams.ExpectedAmountToDistribute);
            // Create transfer params to use in mock call
            var transferParams = new object[] { withdrawParams.Sender, amountToDistribute };

            // Mock the calls expected result
            this.mockInternalExecutor.Setup(e => e
                .Call(this.mockContractState.Object, this.tokenContract, amountToDistribute, "TransferTo", transferParams, 10_000))
                .Returns(withdrawParams.transferResult);

            // Make call and retrieve the result
            var result = this.mockContractState.Object.InternalTransactionExecutor
                .Call(this.mockContractState.Object, this.tokenContract, amountToDistribute, "TransferTo", transferParams, 10_000);
            // Fail if result was not successful
            if (result.Success == false)
            {
                return false;
            }

            // Act as normal method would, set FUNDED account status
            this.mockPersistentState.Setup(s => s.GetStruct<Status>($"Status:{withdrawParams.Sender}")).Returns(Status.FUNDED);
            // Act as normal method would, log the new status
            this.mockContractLogger.Setup(l => l.Log(It.IsAny<ISmartContractState>(), new StatusLog { Registrant = withdrawParams.Sender, Status = Status.FUNDED })).Verifiable();

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
        public void RegistrationIsClosed_IsFalse_IfCurrentBlockIsLessThanOrEqualToEndBlock()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.airdropContract, this.registrant, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(999_999);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

            var registrationIsClosed = airdrop.RegistrationIsClosed;
            Assert.False(registrationIsClosed);


            this.mockContractState.Setup(b => b.Block.Number).Returns(this.endBlock);

            registrationIsClosed = airdrop.RegistrationIsClosed;
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

            Assert.False(airdrop.RegistrationIsClosed);

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
            this.mockPersistentState.Setup(e => e.GetAddress("Owner")).Returns(this.airdropContractOwner);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.tokenContract, this.totalSupply, this.endBlock);

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


            // Starting with 10 tokens / 4 registrants = 2 each
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(10);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(4);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)2, amountToDistribute);


            // Starting with 100_000_000_000 tokens / 5456 registrants = 18328445 each
            this.mockPersistentState.Setup(e => e.GetUInt64("TotalSupply")).Returns(100_000_000_000);
            this.mockPersistentState.Setup(e => e.GetUInt64("NumberOfRegistrants")).Returns(5456);

            amountToDistribute = airdrop.AmountToDistribute;

            Assert.Equal((ulong)18328445, amountToDistribute);
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
    }
}