using FluentAssertions;
using Stratis.SmartContracts;
using Xunit;

namespace Multisig.Tests
{
    public class MultisigTests : BaseContractTest
    {
        [Fact]
        public void CreatesMultisigContract_Success()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressOne).Should().BeTrue();
            multiSigContract.IsOwner(AddressTwo).Should().BeTrue();
            multiSigContract.IsOwner(AddressThree).Should().BeTrue();

            multiSigContract.OwnerCount.Should().Be(3);
            multiSigContract.Required.Should().Be(2);
            multiSigContract.TransactionCount.Should().Be(0);
        }

        [Fact]
        public void IsOwner_NonOwnerAddress_ReturnsFalse()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressFour).Should().BeFalse();
            multiSigContract.IsOwner(AddressFive).Should().BeFalse();
            multiSigContract.IsOwner(AddressSix).Should().BeFalse();
        }

        /// <summary>
        /// This is important although counter-intuitive. We regard the contract as 'owning' itself,
        /// but it is not considered one of the individual owner addresses that are allowed to submit and confirm transactions.
        /// The contract's ownership of itself gives it entirely different privileges that are tested elsewhere.
        /// </summary>
        [Fact]
        public void IsOwner_ContractAddress_ReturnsFalse()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(Contract).Should().BeFalse();
        }

        [Fact]
        public void AddOwner_ByContractAddress_Succeeds()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, Contract);

            multiSigContract.AddOwner(AddressFour);
        }

        [Fact]
        public void AddOwner_ByNonContractAddress_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressFour).Should().BeFalse();

            multiSigContract
                .Invoking(v => v.AddOwner(AddressFour))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can only be called by the contract itself.");
        }

        [Fact]
        public void AddOwner_ForAlreadyExistingOwner_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressOne).Should().BeTrue();

            SetupMessage(Contract, Contract);

            multiSigContract
                .Invoking(v => v.AddOwner(AddressOne))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't add an existing owner.");
        }

        [Fact]
        public void IsOwner_AfterAddingOwner_ReturnsTrue()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressFour).Should().BeFalse();

            SetupMessage(Contract, Contract);

            multiSigContract.AddOwner(AddressFour);

            multiSigContract.IsOwner(AddressFour).Should().BeTrue();
        }

        [Fact]
        public void RemoveOwner_ForNonexistentOwner_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressOne).Should().BeTrue();

            SetupMessage(Contract, Contract);

            multiSigContract
                .Invoking(v => v.RemoveOwner(AddressFive))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't remove a non-owner.");
        }

        [Fact]
        public void RemoveOwner_ByContractAddress_Succeeds()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, Contract);

            multiSigContract.RemoveOwner(AddressOne);
        }

        [Fact]
        public void RemoveOwner_ByNonContractAddress_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            multiSigContract
                .Invoking(v => v.RemoveOwner(AddressOne))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can only be called by the contract itself.");
        }

        [Fact]
        public void RemoveOwner_BelowQuorum_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsOwner(AddressOne).Should().BeTrue();

            SetupMessage(Contract, Contract);

            multiSigContract.RemoveOwner(AddressOne);

            multiSigContract
                .Invoking(v => v.RemoveOwner(AddressTwo))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't have less owners than the minimum confirmation requirement.");
        }

        [Fact]
        public void OwnerCount_AfterAddOwner_Increases()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.OwnerCount.Should().Be(3);

            SetupMessage(Contract, Contract);

            multiSigContract.AddOwner(AddressFour);

            multiSigContract.OwnerCount.Should().Be(4);
        }

        [Fact]
        public void OwnerCount_AfterRemoveOwner_Decreases()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.OwnerCount.Should().Be(3);

            SetupMessage(Contract, Contract);

            multiSigContract.RemoveOwner(AddressOne);

            multiSigContract.OwnerCount.Should().Be(2);
        }

        [Fact]
        public void TransactionCount_AfterSubmission_Increases()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            multiSigContract.TransactionCount.Should().Be(1);
        }

        [Fact]
        public void Submit_ByOwner_ReturnsTransactionId()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            transactionId.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Submit_ByNonOwner_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressFour);

            multiSigContract
                .Invoking(v => v.Submit(Contract, "TestMethod", new byte[] { }))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Must be one of the contract owners.");
        }

        [Fact]
        public void Confirm_WithSameAddressTwice_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            // Note: submission is an implicit confirmation by the submitter

            multiSigContract
                .Invoking(v => v.Confirm(transactionId))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Sender has already confirmed the transaction.");
        }

        [Fact]
        public void Confirm_ByNonOwner_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            SetupMessage(Contract, AddressFour);

            multiSigContract
                .Invoking(v => v.Confirm(transactionId))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Sender is not authorized to confirm.");
        }

        [Fact]
        public void Confirm_ForNonexistentTransaction_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            multiSigContract
                .Invoking(v => v.Confirm(1))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Transaction ID does not exist.");
        }

        [Fact]
        public void Confirmations_ForSubmittedTransaction_ReturnsOne()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            // Note: submission is an implicit confirmation by the submitter

            multiSigContract.Confirmations(transactionId).Should().Be(1);
        }

        [Fact]
        public void Confirmations_AfterConfirmingTransaction_Increases()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            SetupMessage(Contract, AddressTwo);

            multiSigContract.Confirm(transactionId);

            multiSigContract.Confirmations(transactionId).Should().Be(2);
        }

        [Fact]
        public void IsConfirmedBy_ForSubmittedTransaction_ReturnsTrue()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            // Note: submission is an implicit confirmation by the submitter

            multiSigContract.IsConfirmedBy(transactionId, AddressOne).Should().BeTrue();
        }

        [Fact]
        public void IsConfirmedBy_ForExecutedTransaction_ReturnsTrue()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            SetupMessage(Contract, AddressTwo);

            multiSigContract.Confirm(transactionId);

            multiSigContract.IsConfirmedBy(transactionId, AddressOne).Should().BeTrue();
            multiSigContract.IsConfirmedBy(transactionId, AddressTwo).Should().BeTrue();
        }

        [Fact]
        public void IsConfirmedBy_ForNonexistentTransaction_ReturnsFalse()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract.IsConfirmedBy(1, AddressOne).Should().BeFalse();
        }

        [Fact]
        public void GetTransaction_ForNonexistentTransaction_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract
                .Invoking(v => v.GetTransaction(1))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't retrieve a transaction that doesn't exist yet.");
        }

        [Fact]
        public void GetTransaction_AfterSubmittingTransaction_ReturnsTransaction()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            MultisigContract.Transaction result = multiSigContract.GetTransaction(transactionId);

            result.Destination.Should().Be(Contract);
            result.Executed.Should().BeFalse();
            result.Value.Should().Be(0);
            result.MethodName.Should().Be("TestMethod");
            result.Parameters.Should().BeEmpty();
        }

        [Fact]
        public void GetTransaction_AfterSufficientlyConfirmingTransaction_ReturnsExecutedTransaction()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, AddressOne);

            ulong transactionId = multiSigContract.Submit(Contract, "TestMethod", new byte[] { });

            SetupMessage(Contract, AddressTwo);

            multiSigContract.Confirm(transactionId);

            MultisigContract.Transaction result = multiSigContract.GetTransaction(transactionId);

            result.Destination.Should().Be(Contract);
            result.Executed.Should().BeTrue();
        }

        [Fact]
        public void ChangeRequirement_ByContractAddress_Succeeds()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, Contract);

            multiSigContract.ChangeRequirement(3);
        }

        [Fact]
        public void ChangeRequirement_ByNonContractAddress_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            multiSigContract
                .Invoking(v => v.ChangeRequirement(3))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can only be called by the contract itself.");
        }

        [Fact]
        public void ChangeRequirement_ToZero_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, Contract);

            multiSigContract
                .Invoking(v => v.ChangeRequirement(0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't have a quorum of less than 1.");
        }

        [Fact]
        public void ChangeRequirement_HigherThanOwnerCount_Throws()
        {
            var multiSigContract = CreateNewMultisigContract();

            SetupMessage(Contract, Contract);

            multiSigContract
                .Invoking(v => v.ChangeRequirement(4))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("Can't set quorum higher than the number of owners.");
        }
    }
}
