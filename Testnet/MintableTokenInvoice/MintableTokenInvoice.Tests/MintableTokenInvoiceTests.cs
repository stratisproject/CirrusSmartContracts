namespace MintableTokenInvoiceTests;

using FluentAssertions;
using Moq;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

// Claim as defined by Identity Server.
public class Claim
{
    public Claim()
    {
    }

    public string Key { get; set; }

    public string Description { get; set; }

    public bool IsRevoked { get; set; }
}

public struct Invoice
{
    public string Symbol;
    public UInt256 Amount;
    public Address To;
    public string Outcome;
    public bool IsAuthorized;
}

/// <summary>
/// These tests validate the functionality that differs between the original standard token and the extended version.
/// </summary>
public class MintableTokenInvoiceTests : BaseContractTest
{
    [Fact]
    public void Constructor_Assigns_Owner()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        // Verify that PersistentState was called with the contract owner
        mintableTokenInvoice.Owner.Should().Be(this.Owner);
    }

    [Fact]
    public void TransferOwnership_Succeeds_For_Owner()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();
        mintableTokenInvoice.SetNewOwner(this.AddressOne);
        this.SetupMessage(this.Contract, this.AddressOne);
        mintableTokenInvoice.ClaimOwnership();
        mintableTokenInvoice.Owner.Should().Be(this.AddressOne);
    }

    [Fact]
    public void TransferOwnership_Fails_For_NonOwner()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();
        this.SetupMessage(this.Contract, this.AddressOne);
        Assert.ThrowsAny<SmartContractAssertException>(() => mintableTokenInvoice.SetNewOwner(this.AddressTwo));
    }

    [Fact]
    public void CanDeserializeClaim()
    {
        // First serialize the claim data as the IdentityServer would do it when calling "AddClaim".
        var claim = new Claim() { Key = "Identity Approved", Description = "Identity Approved", IsRevoked = false };

        var bytes = new ASCIIEncoder().DecodeData(JsonConvert.SerializeObject(claim));

        var json = this.Serializer.ToString(bytes);

        Assert.Contains("Identity Approved", json);
    }

    [Fact]
    public void CanCreateInvoice()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        UInt128 uniqueNumber = 1;

        this.SetupMessage(this.Contract, this.AddressOne);

        var claim = new Claim() { Description = "Identity Approved", IsRevoked = false, Key = "Identity Approved" };
        var claimBytes = new ASCIIEncoder().DecodeData(JsonConvert.SerializeObject(claim));

        this.MockInternalExecutor
            .Setup(x => x.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<ulong>()))
            .Returns((ISmartContractState state, Address address, ulong amount, string methodName, object[] args, ulong gasLimit) =>
            {
                return TransferResult.Transferred(claimBytes);
            });

        var transactionReference = mintableTokenInvoice.CreateInvoice("GBPT", 100, uniqueNumber);
        var invoiceReference = mintableTokenInvoice.GetInvoiceReference(transactionReference);

        Assert.Equal("REF925543804354", transactionReference.ToString());

        var invoiceBytes = mintableTokenInvoice.RetrieveInvoice(invoiceReference, true);
        var invoice = this.Serializer.ToStruct<Invoice>(invoiceBytes);

        Assert.Equal(100, invoice.Amount);
        Assert.Equal("GBPT", invoice.Symbol);
        Assert.Equal(this.AddressOne, invoice.To);
        Assert.True(invoice.IsAuthorized);
        Assert.Null(invoice.Outcome);
    }


    [Fact]
    public void CantCreateInvoiceIfNotKYCed()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        UInt128 uniqueNumber = 1;

        this.SetupMessage(this.Contract, this.AddressOne);

        this.MockInternalExecutor
            .Setup(x => x.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<ulong>()))
            .Returns((ISmartContractState state, Address address, ulong amount, string methodName, object[] args, ulong gasLimit) =>
            {
                return null;
            });

        var ex = Assert.Throws<SmartContractAssertException>(() => mintableTokenInvoice.CreateInvoice("GBPT", 100, uniqueNumber));
        Assert.Contains("verification", ex.Message);
    }

    [Fact]
    public void CantCreateInvoiceIfNotAuthorized()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        UInt128 uniqueNumber = 1;

        this.SetupMessage(this.Contract, this.AddressOne);

        var claim = new Claim() { Description = "Identity Approved", IsRevoked = false, Key = "Identity Approved" };
        var claimBytes = new ASCIIEncoder().DecodeData(JsonConvert.SerializeObject(claim));

        this.MockInternalExecutor
            .Setup(x => x.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<ulong>()))
            .Returns((ISmartContractState state, Address address, ulong amount, string methodName, object[] args, ulong gasLimit) =>
            {
                return TransferResult.Transferred(claimBytes);
            });

        var ex = Assert.Throws<SmartContractAssertException>(() => mintableTokenInvoice.CreateInvoice("GBPT", 2000, uniqueNumber));
        Assert.Contains("authorization", ex.Message);
    }
}