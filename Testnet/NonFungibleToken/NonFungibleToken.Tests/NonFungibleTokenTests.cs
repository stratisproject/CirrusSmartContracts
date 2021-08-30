using DividendTokenContract.Tests;
using Moq;
using Moq.Language.Flow;
using NonFungibleTokenContract.Tests;
using Stratis.SmartContracts;
using System;
using System.Linq;
using Xunit;

public class NonFungibleTokenTests
{
    private Mock<ISmartContractState> smartContractStateMock;
    private Mock<IContractLogger> contractLoggerMock;
    private InMemoryState state;
    private Mock<IInternalTransactionExecutor> transactionExecutorMock;
    private Address contractAddress;
    private string name;
    private string symbol;
    private string tokenURIFormat;
    private bool ownerOnlyMinting;

    public NonFungibleTokenTests()
    {
        this.contractLoggerMock = new Mock<IContractLogger>();
        this.smartContractStateMock = new Mock<ISmartContractState>();
        this.transactionExecutorMock = new Mock<IInternalTransactionExecutor>();
        this.state = new InMemoryState();
        this.smartContractStateMock.Setup(s => s.PersistentState).Returns(this.state);
        this.smartContractStateMock.Setup(s => s.ContractLogger).Returns(this.contractLoggerMock.Object);
        this.smartContractStateMock.Setup(x => x.InternalTransactionExecutor).Returns(this.transactionExecutorMock.Object);
        this.contractAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
        this.name = "Non-Fungible Token";
        this.symbol = "NFT";
        this.tokenURIFormat = "https://example.com/api/tokens/{0}/meta";
        this.ownerOnlyMinting = true;
    }

    [Fact]
    public void Constructor_Sets_Values()
    {
        var owner = "0x0000000000000000000000000000000000000005".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(owner);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.True(this.state.GetBool("SupportedInterface:1"));
        Assert.True(this.state.GetBool("SupportedInterface:2"));
        Assert.False(this.state.GetBool("SupportedInterface:3"));
        Assert.True(this.state.GetBool("SupportedInterface:4"));
        Assert.False(this.state.GetBool("SupportedInterface:5"));
        Assert.Equal(this.name, nonFungibleToken.Name);
        Assert.Equal(this.symbol, nonFungibleToken.Symbol);
        Assert.Equal(owner, nonFungibleToken.Owner);
        Assert.Equal(this.ownerOnlyMinting, this.state.GetBool("OwnerOnlyMinting"));
        Assert.Equal(this.tokenURIFormat, this.state.GetString("TokenURIFormat"));
    }

    [Fact]
    public void TransferOwnership_CalledByNonContractOwner_ThrowsException()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000004".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, someAddress, 0));
        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferOwnership(newOwner));
    }

    [Fact]
    public void TransferOwnership_CalledByZeroAddress_ThrowsException()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, Address.Zero, 0));
        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferOwnership(newOwner));
    }

    [Fact]
    public void TransferOwnership_CalledByContractOwner_Success()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferOwnership(newOwner);

        Assert.Equal(newOwner, nonFungibleToken.Owner);

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.OwnershipTransferedLog { PreviousOwner = owner, NewOwner = newOwner }));
    }

    [Fact]
    public void SupportsInterface_InterfaceSupported_ReturnsTrue()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(2);

        Assert.True(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceSetToFalseSupported_ReturnsFalse()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();
        this.state.SetBool("SupportedInterface:2", false);

        var result = nonFungibleToken.SupportsInterface(3);

        Assert.False(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceNotSupported_ReturnsFalse()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(6);

        Assert.False(result);
    }

    [Fact]
    public void TokenUri_Format_With_TokenId_Success()
    {
        this.tokenURIFormat = "https://examples.com/api/tokens/{0}/metadata";

        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();

        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.TokenURI(4);

        var expected = $"https://examples.com/api/tokens/4/metadata";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TokenUri_Format_With_TokenId_And_ContractAddress_Success()
    {
        this.tokenURIFormat = "https://examples.com/api/contracts/{1}/tokens/{0}/metadata";

        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();

        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.TokenURI(4);

        var expected = $"https://examples.com/api/contracts/{contractAddress}/tokens/4/metadata";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetApproved_NotValidNFToken_OwnerAddressZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.GetApproved(1));
    }

    [Fact]
    public void GetApproved_ApprovalNotInStorage_ReturnsZeroAddress()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));
        this.state.SetAddress("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(Address.Zero, result);
    }

    [Fact]
    public void GetApproved_ApprovalInStorage_ReturnsAddress()
    {

        var approvalAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());
        this.state.SetAddress("IdToApproval:1", approvalAddress);

        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();
        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(approvalAddress, result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsTrue_ReturnsTrue()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", true);
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.True(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsFalse_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", false);
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorNotInState_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void OwnerOf_IdToOwnerNotInStorage_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenMappedToAddressZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        this.state.SetAddress("IdToOwner:1", Address.Zero);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenExistsWithOwner_ReturnsOwnerAddress()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.OwnerOf(1);

        Assert.Equal(ownerAddress, result);
    }

    [Fact]
    public void BalanceOf_OwnerZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => { nonFungibleToken.BalanceOf(Address.Zero); });
    }

    [Fact]
    public void BalanceOf_NftTokenCountNotInStorage_ReturnsZero()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal((ulong)0, result);
    }

    [Fact]
    public void BalanceOf_OwnerNftTokenCountInStorage_ReturnsTokenCount()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetUInt64($"Balance:{ownerAddress}", 15);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal((ulong)15, result);
    }

    [Fact]
    public void SetApprovalForAll_SetsMessageSender_ToOperatorApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SetApprovalForAll(operatorAddress, true);

        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalForAllLog { Owner = ownerAddress, Operator = operatorAddress, Approved = true }));
    }

    [Fact]
    public void Approve_TokenOwnerNotMessageSenderOrOperator_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_SwitchesOwnerToApprovedForNFToken()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.Equal(this.state.GetAddress("IdToApproval:1"), someAddress);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_NTFokenOwnerSameAsMessageSender_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(ownerAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_ByApprovedOperator_SwitchesOwnerToApprovedForNFToken()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.Equal(this.state.GetAddress("IdToApproval:1"), someAddress);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_InvalidNFToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = Address.Zero;
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", Address.Zero);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", Address.Zero);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(Address.Zero, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1ul).Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"persistentState.GetUInt64:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, approvalAddress, ownerAddress, 1ul).Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, operatorAddress, ownerAddress, 1ul).Returns(TransferResult.Transferred(true));


        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", Address.Zero);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(Address.Zero, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1ul).Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    private IReturnsThrows<IInternalTransactionExecutor, ITransferResult> SetupForOnNonFungibleTokenReceived(Address targetAddress, Address @operator, Address from, ulong tokenId)
    {
        return this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", It.IsAny<object[]>(), 0ul))
                                            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
                                            {
                                                Assert.True(@operator.Equals(callParams[0]));
                                                Assert.True(from.Equals(callParams[1]));
                                                Assert.True(tokenId.Equals(callParams[2]));
                                                Assert.True(callParams[3] is byte[] bytes && bytes.Length == 0);
                                            });
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTruthyObject_CannotCastToBool_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1ul).Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, 1ul, data };

        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0ul))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { approvalAddress, ownerAddress, 1ul, data };

        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0ul))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { operatorAddress, ownerAddress, 1ul, data };

        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0ul))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"Balance:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"Balance:{targetAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", Address.Zero);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(Address.Zero, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, 1ul, data };

        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0ul))
                                    .Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTruthyObject_CannotCastToBool_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
        var nonFungibleToken = this.CreateNonFungibleToken();


        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, 1ul, data };
        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0ul))
                                    .Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void Mint_CalledByNonOwner_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var userAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(userAddress);

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(userAddress));
    }

    [Fact]
    public void Mint_ToAdressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(Address.Zero));
    }

    [Fact]
    public void Mint_MintingNewToken_Called_By_None_Owner_When_OwnerOnlyMintingFalse_Success()
    {
        this.ownerOnlyMinting = false;

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();

        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(targetAddress);
        nonFungibleToken.Mint(targetAddress);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"Balance:{targetAddress}"));
        Assert.Equal(1ul, this.state.GetUInt64("TokenIdCounter"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void Mint_MintingNewToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Mint(targetAddress);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"Balance:{targetAddress}"));
        Assert.Equal(1ul, this.state.GetUInt64("TokenIdCounter"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeMint_MintingNewToken_When_Destination_Is_Contract_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.IsContractResult = true;

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();
        var data = new byte[] { 12 };
        var parameter = new object[] { ownerAddress, Address.Zero, 1ul, data };
        this.transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", parameter, 0))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeMint(targetAddress, data);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"Balance:{targetAddress}"));
        Assert.Equal(1ul, this.state.GetUInt64("TokenIdCounter"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void Burn_NoneExistingToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Burn(0));
    }

    [Fact]
    public void Burn_BurningNotOwnedToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var anotherTokenOwner = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.SetAddress("IdToOwner:0", anotherTokenOwner);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Burn(0));
    }

    [Fact]
    public void Burn_BurningAToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"Balance:{ownerAddress}", 1);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Burn(1);

        Assert.Equal(Address.Zero, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(0ul, this.state.GetUInt64($"Balance:{ownerAddress}"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = Address.Zero, TokenId = 1 }));
    }

    private NonFungibleToken CreateNonFungibleToken()
    {
        return new NonFungibleToken(this.smartContractStateMock.Object, this.name, this.symbol, this.tokenURIFormat, this.ownerOnlyMinting);
    }
}