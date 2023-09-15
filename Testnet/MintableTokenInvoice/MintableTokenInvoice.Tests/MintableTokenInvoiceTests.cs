namespace MintableTokenInvoiceTests;

using DBreeze.Utils;
using FluentAssertions;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using System.Collections.Generic;
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

class ChameleonNetwork : Network
{
    public ChameleonNetwork(byte base58Prefix)
    {
        this.Base58Prefixes = new byte[][] { new byte[] { base58Prefix } };
    }
}

public struct Invoice
{
    public string Symbol;
    public UInt256 Amount;
    public UInt256 Fee;
    public Address To;
    public string TargetAddress;
    public string Network;
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
        mintableTokenInvoice.TransferOwnership(this.AddressOne);
        this.SetupMessage(this.Contract, this.AddressOne);
        mintableTokenInvoice.ClaimOwnership();
        mintableTokenInvoice.Owner.Should().Be(this.AddressOne);
    }

    [Fact]
    public void TransferOwnership_Fails_For_NonOwner()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();
        this.SetupMessage(this.Contract, this.AddressOne);
        Assert.ThrowsAny<SmartContractAssertException>(() => mintableTokenInvoice.TransferOwnership(this.AddressTwo));
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

        var transactionReference = mintableTokenInvoice.CreateInvoice("GBPT", 100, 0, uniqueNumber, "Address", "Network");
        var invoiceReference = mintableTokenInvoice.GetInvoiceReference(transactionReference);

        Assert.Equal("INV-1760-4750-2039", invoiceReference.ToString());

        // 42 is checksum for INV numbers.
        Assert.Equal(42UL, ulong.Parse(invoiceReference.Replace("-", string.Empty)[3..]) % 97);

        Assert.Equal("REF-5377-4902-2339", transactionReference.ToString());

        // 1 is checksum for REF numbers.
        Assert.Equal(1UL, ulong.Parse(transactionReference.Replace("-", string.Empty)[3..]) % 97);

        var invoiceBytes = mintableTokenInvoice.RetrieveInvoice(invoiceReference, true);
        var invoice = this.Serializer.ToStruct<Invoice>(invoiceBytes);

        Assert.Equal(100, invoice.Amount);
        Assert.Equal("GBPT", invoice.Symbol);
        Assert.Equal(this.AddressOne, invoice.To);
        Assert.True(invoice.IsAuthorized);
        Assert.Null(invoice.Outcome);
    }

    private struct SignatureTemplate
    {
        public UInt128 uniqueNumber;
        public string symbol;
        public UInt256 amount;
        public UInt256 fee;
        public string targetAddress;
        public string targetNetwork;
        public Address contract;
    }

    [Fact]
    public void CanCreateInvoiceFor()
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

        var key = new Key(new HexEncoder().DecodeData("c6edd54dd0671f1415a94ad388265c4465a8b328cc51a0a1fe770d910b48b0d1"));
        var address = key.PubKey.Hash.ToBytes().ToAddress();

        var template = new SignatureTemplate() { uniqueNumber = uniqueNumber, amount = 100, fee = 0, symbol = "GBPT", targetAddress = "Address", targetNetwork = "Network", contract = this.Contract };
        var message = new uint256(new InternalHashHelper().Keccak256(this.Serializer.Serialize(template)));

        var signature = key.SignCompact(message);

        var transactionReference = mintableTokenInvoice.CreateInvoiceFor(address, "GBPT", 100, uniqueNumber, "Address", "Network", signature);
        var invoiceReference = mintableTokenInvoice.GetInvoiceReference(transactionReference);

        Assert.Equal("INV-2724-4779-8084", invoiceReference.ToString());

        // 42 is checksum for INV numbers.
        Assert.Equal(42UL, ulong.Parse(invoiceReference.Replace("-", string.Empty)[3..]) % 97);

        Assert.Equal("REF-4623-8979-4313", transactionReference.ToString());

        // 1 is checksum for REF numbers.
        Assert.Equal(1UL, ulong.Parse(transactionReference.Replace("-", string.Empty)[3..]) % 97);

        var invoiceBytes = mintableTokenInvoice.RetrieveInvoice(invoiceReference, true);
        var invoice = this.Serializer.ToStruct<Invoice>(invoiceBytes);

        Assert.Equal(100, invoice.Amount);
        Assert.Equal("GBPT", invoice.Symbol);
        Assert.Equal(address, invoice.To);
        Assert.True(invoice.IsAuthorized);
        Assert.Null(invoice.Outcome);
    }


    [Fact]
    public void CanCreateInvoiceFromURL()
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

        var key = new Key(new HexEncoder().DecodeData("c6edd54dd0671f1415a94ad388265c4465a8b328cc51a0a1fe770d910b48b0d1"));
        var address = key.PubKey.Hash.ToBytes().ToAddress();

        var hexUniqueNumber = Encoders.Hex.EncodeData(uniqueNumber.ToBytes().Reverse());

        var contractAddress = this.Contract.ToUint160().ToBase58Address(new ChameleonNetwork(1));

        var url = $"webdemo.stratisplatform.com:7167/api/auth?uid={hexUniqueNumber}&symbol=GBPT&amount=0.01&fee=0&targetAddress=Address&targetNetwork=Network&contract={contractAddress}";
        var signature = key.SignMessage(url);
        byte[] signatureBytes = Encoders.Base64.DecodeData(signature);
        var transactionReference = mintableTokenInvoice.CreateInvoiceFromURL(address, url, signatureBytes);
        var invoiceReference = mintableTokenInvoice.GetInvoiceReference(transactionReference);

        Assert.Equal("INV-2724-4779-8084", invoiceReference.ToString());

        // 42 is checksum for INV numbers.
        Assert.Equal(42UL, ulong.Parse(invoiceReference.Replace("-", string.Empty)[3..]) % 97);

        Assert.Equal("REF-4623-8979-4313", transactionReference.ToString());

        // 1 is checksum for REF numbers.
        Assert.Equal(1UL, ulong.Parse(transactionReference.Replace("-", string.Empty)[3..]) % 97);

        var invoiceBytes = mintableTokenInvoice.RetrieveInvoice(invoiceReference, true);
        var invoice = this.Serializer.ToStruct<Invoice>(invoiceBytes);

        Assert.Equal(1000000, invoice.Amount);
        Assert.Equal("GBPT", invoice.Symbol);
        Assert.Equal(address, invoice.To);
        Assert.True(invoice.IsAuthorized);
        Assert.Null(invoice.Outcome);
    }

    /// <summary>
    /// Provides data for testing token minting transactions.
    /// </summary>
    /// <returns>A series of object arrays containing data for token minting transactions.</returns>
    public static IEnumerable<object[]> TransferResults()
    {
        yield return new object[] { TransferResult.Failed(), "Could not determine KYC status" };
        yield return new object[] { TransferResult.Empty(), "Your KYC status is not valid" };
    }

    [Theory]
    [MemberData(nameof(TransferResults))]
    public void CantCreateInvoiceIfNotKYCed(TransferResult result, string outcome)
    {
        TransferResult.Transferred(new byte[] { });
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        UInt128 uniqueNumber = 1;

        this.SetupMessage(this.Contract, this.AddressOne);

        this.MockInternalExecutor
            .Setup(x => x.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<ulong>()))
            .Returns((ISmartContractState state, Address address, ulong amount, string methodName, object[] args, ulong gasLimit) =>
            {
                return result;
            });

        var ex = Assert.Throws<SmartContractAssertException>(() => mintableTokenInvoice.CreateInvoice("GBPT", 100, 0, uniqueNumber, "Address", "Network"));
        Assert.Equal(outcome, ex.Message);
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

        var ex = mintableTokenInvoice.CreateInvoice("GBPT", 20000000, 0, uniqueNumber, "Address", "Network");
        Assert.Contains("authorization", ex);
    }

    [Fact]
    public void CantCreateInvoiceIfDidNotExist()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        UInt128 uniqueNumber = 1;

        var claim = new Claim() { Description = "Identity Approved", IsRevoked = false, Key = "Identity Approved" };
        var claimBytes = new ASCIIEncoder().DecodeData(JsonConvert.SerializeObject(claim));

        this.MockInternalExecutor
            .Setup(x => x.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<ulong>()))
            .Returns((ISmartContractState state, Address address, ulong amount, string methodName, object[] args, ulong gasLimit) =>
            {
                return TransferResult.Transferred(claimBytes);
            });


        // The minters will set this status for any payment reference that could not be processed.
        // We don't want to process these payments at a later stage as they may get refunded.
        mintableTokenInvoice.SetOutcome("REF-5377-4902-2339", "Payment could not be processed");

        this.SetupMessage(this.Contract, this.AddressOne);

        // Check that we don't "create" an invoice for a payment reference associated with an existing outcome.
        var ex = Assert.Throws<SmartContractAssertException>(() => mintableTokenInvoice.CreateInvoice("GBPT", 200, 0, uniqueNumber, "Address", "Network"));
        Assert.Contains("processed", ex.Message);
    }

    [Fact]
    public void CanSetIdentityContract()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        mintableTokenInvoice.SetIdentityContract(this.Contract);

        Assert.Equal(this.Contract, mintableTokenInvoice.IdentityContract);
    }

    [Fact]
    public void CanSetKYCProvider()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        mintableTokenInvoice.SetKYCProvider(2);

        Assert.Equal((uint)2, mintableTokenInvoice.KYCProvider);
    }

    [Fact]
    public void CanSetAuthorizationLimit()
    {
        var mintableTokenInvoice = this.CreateNewMintableTokenContract();

        mintableTokenInvoice.SetAuthorizationLimit(300);

        Assert.Equal((UInt256)300, mintableTokenInvoice.AuthorizationLimit);
    }
}