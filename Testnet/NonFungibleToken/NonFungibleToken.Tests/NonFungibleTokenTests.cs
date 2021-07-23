using System;
using System.Collections.Generic;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

public class NonFungibleTokenTests
{
    private Mock<ISmartContractState> smartContractStateMock;
    private Mock<IContractLogger> contractLoggerMock;
    private Mock<IPersistentState> persistentStateMock;
    private Dictionary<string, bool> supportedInterfaces;
    private Dictionary<string, Address> idToOwner;
    private Dictionary<string, Address> idToApproval;
    private Dictionary<string, bool> ownerToOperator;
    private Dictionary<string, ulong> ownerToNFTokenCount;
    private Mock<IInternalTransactionExecutor> internalTransactionExecutorMock;

    public NonFungibleTokenTests()
    {
        this.contractLoggerMock = new Mock<IContractLogger>();
        this.persistentStateMock = new Mock<IPersistentState>();
        this.smartContractStateMock = new Mock<ISmartContractState>();
        this.internalTransactionExecutorMock = new Mock<IInternalTransactionExecutor>();
        this.smartContractStateMock.Setup(s => s.PersistentState).Returns(this.persistentStateMock.Object);
        this.smartContractStateMock.Setup(s => s.ContractLogger).Returns(this.contractLoggerMock.Object);
        this.smartContractStateMock.Setup(x => x.InternalTransactionExecutor).Returns(this.internalTransactionExecutorMock.Object);

        this.supportedInterfaces = new Dictionary<string, bool>();
        this.idToOwner = new Dictionary<string, Address>();
        this.idToApproval = new Dictionary<string, Address>();
        this.ownerToOperator = new Dictionary<string, bool>();
        this.ownerToNFTokenCount = new Dictionary<string, ulong>();

        this.SetupPersistentState();
    }

    [Fact]
    public void Constructor_Sets_SupportedInterfaces()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Equal(3, this.supportedInterfaces.Count);
        Assert.True(this.supportedInterfaces["SupportedInterface:1"]);
        Assert.True(this.supportedInterfaces["SupportedInterface:2"]);
        Assert.False(this.supportedInterfaces["SupportedInterface:3"]);
    }

    [Fact]
    public void SupportsInterface_InterfaceSupported_ReturnsTrue()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(2);

        Assert.True(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceSetToFalseSupported_ReturnsFalse()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();
        this.supportedInterfaces["SupportedInterface:2"] = false;

        var result = nonFungibleToken.SupportsInterface(3);

        Assert.False(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceNotSupported_ReturnsFalse()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(4);

        Assert.False(result);
    }

    [Fact]
    public void GetApproved_NotValidNFToken_OwnerAddressZero_ThrowsException()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.GetApproved(1));
    }

    [Fact]
    public void GetApproved_ApprovalNotInStorage_ReturnsZeroAddress()
    {
        this.idToOwner.Add("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());
        this.idToApproval.Clear();

        var nonFungibleToken = this.CreateNonFungibleToken();
        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(Address.Zero, result);
    }

    [Fact]
    public void GetApproved_ApprovalInStorage_ReturnsAddress()
    {
        var approvalAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());
        this.idToApproval.Add("IdToApproval:1", approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();
        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(approvalAddress, result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsTrue_ReturnsTrue()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", true);

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.True(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsFalse_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", false);

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorNotInState_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.ownerToOperator.Clear();

        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void OwnerOf_IdToOwnerNotInStorage_ThrowsException()
    {
        this.idToOwner.Clear();
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenMappedToAddressZero_ThrowsException()
    {
        this.idToOwner.Add("IdToOwner:1", Address.Zero);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenExistsWithOwner_ReturnsOwnerAddress()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.OwnerOf(1);

        Assert.Equal(ownerAddress, result);
    }

    [Fact]
    public void BalanceOf_OwnerZero_ThrowsException()
    {
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => { nonFungibleToken.BalanceOf(Address.Zero); });
    }

    [Fact]
    public void BalanceOf_NftTokenCountNotInStorage_ReturnsZero()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.ownerToNFTokenCount.Clear();
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal((ulong)0, result);
    }

    [Fact]
    public void BalanceOf_OwnerNftTokenCountInStorage_ReturnsTokenCount()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 15);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal((ulong)15, result);
    }

    [Fact]
    public void SetApprovalForAll_SetsMessageSender_ToOperatorApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        nonFungibleToken.SetApprovalForAll(operatorAddress, true);

        Assert.NotEmpty(this.ownerToOperator);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalForAllLog { Owner = ownerAddress, Operator = operatorAddress, Approved = true }));
    }

    [Fact]
    public void Approve_TokenOwnerNotMessageSenderOrOperator_ThrowsException()
    {
        this.idToOwner.Clear();
        this.ownerToOperator.Clear();
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_SwitchesOwnerToApprovedForNFToken()
    {
        this.idToApproval.Clear();
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.NotEmpty(this.idToApproval);
        Assert.Equal(this.idToApproval["IdToApproval:1"], someAddress);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_NTFokenOwnerSameAsMessageSender_ThrowsException()
    {
        this.idToApproval.Clear();
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(ownerAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_ByApprovedOperator_SwitchesOwnerToApprovedForNFToken()
    {
        this.idToApproval.Clear();
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.NotEmpty(this.idToApproval);
        Assert.Equal(this.idToApproval["IdToApproval:1"], someAddress);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_InvalidNFToken_ThrowsException()
    {
        this.idToApproval.Clear();
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = Address.Zero;
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", Address.Zero);
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Add("IdToApproval:1", approvalAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Clear();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", Address.Zero);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Add("IdToApproval:1", approvalAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Clear();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.Empty((byte[])callParams[3]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Add("IdToApproval:1", approvalAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { approvalAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.Empty((byte[])callParams[3]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Clear();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { operatorAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.Empty((byte[])callParams[3]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", Address.Zero);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.Empty((byte[])callParams[3]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTruthyObject_CannotCastToBool_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.Empty((byte[])callParams[3]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Add("IdToApproval:1", approvalAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Clear();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(false);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.NotEmpty((byte[])callParams[3]);
                Assert.Equal(0xff, ((byte[])callParams[3])[0]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Add("IdToApproval:1", approvalAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { approvalAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.NotEmpty((byte[])callParams[3]);
                Assert.Equal(0xff, ((byte[])callParams[3])[0]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.idToApproval.Clear();
        this.ownerToOperator.Add($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { operatorAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.NotEmpty((byte[])callParams[3]);
                Assert.Equal(0xff, ((byte[])callParams[3])[0]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.idToOwner["IdToOwner:1"]);
        Assert.Empty(this.idToApproval);
        Assert.True(this.ownerToOperator[$"OwnerToOperator:{ownerAddress}:{operatorAddress}"]);
        Assert.Equal((ulong)0, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{ownerAddress}"]);
        Assert.Equal((ulong)1, this.ownerToNFTokenCount[$"OwnerToNFTokenCount:{targetAddress}"]);
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", Address.Zero);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.NotEmpty((byte[])callParams[3]);
                Assert.Equal(0xff, ((byte[])callParams[3])[0]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTruthyObject_CannotCastToBool_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.persistentStateMock.Setup(p => p.IsContract(targetAddress))
            .Returns(true);
        var nonFungibleToken = this.CreateNonFungibleToken();

        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (ulong)1, new byte[0] };
        this.internalTransactionExecutorMock.Setup(
            t => t.Call(
            It.IsAny<ISmartContractState>(),
            targetAddress,
            0,
            "OnNonFungibleTokenReceived",
            It.IsAny<object[]>(),
            It.IsAny<ulong>()))
            .Callback<ISmartContractState, Address, ulong, string, object[], ulong>((a, b, c, d, callParams, f) =>
            {
                Assert.Equal(callParamsExpected[0], callParams[0]);
                Assert.Equal(callParamsExpected[1], callParams[1]);
                Assert.Equal(callParamsExpected[2], callParams[2]);
                Assert.NotEmpty((byte[])callParams[3]);
                Assert.Equal(0xff, ((byte[])callParams[3])[0]);
                Assert.Equal(typeof(byte[]), callParams[3].GetType());
            })
            .Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.idToOwner.Add("IdToOwner:1", ownerAddress);
        this.ownerToNFTokenCount.Add($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1, new byte[1] { 0xff }));
    }

    private NonFungibleToken CreateNonFungibleToken()
    {
        return new NonFungibleToken(this.smartContractStateMock.Object);
    }

    private void SetupPersistentState()
    {
        this.SetupSupportedInterfaces();
        this.SetupIdToOwner();
        this.SetupIdToApproval();
        this.SetupOwnerToOperators();
        this.SetupOwnerToNFTokenCount();
    }

    private void SetupOwnerToNFTokenCount()
    {
        this.persistentStateMock.Setup(p => p.SetUInt64(It.Is<string>(s => s.StartsWith("OwnerToNFTokenCount:", StringComparison.Ordinal)), It.IsAny<ulong>()))
            .Callback<string, ulong>((key, value) =>
            {
                if (this.ownerToNFTokenCount.ContainsKey(key))
                {
                    this.ownerToNFTokenCount[key] = value;
                }
                else
                {
                    this.ownerToNFTokenCount.Add(key, value);
                }
            });
        this.persistentStateMock.Setup(p => p.GetUInt64(It.Is<string>(s => s.StartsWith("OwnerToNFTokenCount:"))))
            .Returns<string>((key) =>
            {
                if (this.ownerToNFTokenCount.ContainsKey(key))
                {
                    return this.ownerToNFTokenCount[key];
                }

                return default(ulong);
            });
    }

    private void SetupOwnerToOperators()
    {
        this.persistentStateMock.Setup(p => p.SetBool(It.Is<string>(s => s.StartsWith("OwnerToOperator:", StringComparison.Ordinal)), It.IsAny<bool>()))
            .Callback<string, bool>((key, value) =>
            {
                if (this.ownerToOperator.ContainsKey(key))
                {
                    this.ownerToOperator[key] = value;
                }
                else
                {
                    this.ownerToOperator.Add(key, value);
                }
            });
        this.persistentStateMock.Setup(p => p.GetBool(It.Is<string>(s => s.StartsWith("OwnerToOperator:"))))
            .Returns<string>((key) =>
            {
                if (this.ownerToOperator.ContainsKey(key))
                {
                    return this.ownerToOperator[key];
                }

                return default(bool);
            });
    }

    private void SetupIdToApproval()
    {
        this.persistentStateMock.Setup(p => p.SetAddress(It.Is<string>(s => s.StartsWith("IdToApproval:", StringComparison.Ordinal)), It.IsAny<Address>()))
           .Callback<string, Address>((key, value) =>
           {
               if (this.idToApproval.ContainsKey(key))
               {
                   this.idToApproval[key] = value;
               }
               else
               {
                   this.idToApproval.Add(key, value);
               }
           });
        this.persistentStateMock.Setup(p => p.GetAddress(It.Is<string>(s => s.StartsWith("IdToApproval:"))))
            .Returns<string>((key) =>
            {
                if (this.idToApproval.ContainsKey(key))
                {
                    return this.idToApproval[key];
                }

                return Address.Zero;
            });

        this.persistentStateMock.Setup(p => p.Clear(It.Is<string>(s => s.StartsWith("IdToApproval:"))))
           .Callback<string>((key) =>
           {
               this.idToApproval.Remove(key);
           });
    }

    private void SetupIdToOwner()
    {
        this.persistentStateMock.Setup(p => p.SetAddress(It.Is<string>(s => s.StartsWith("IdToOwner:", StringComparison.Ordinal)), It.IsAny<Address>()))
           .Callback<string, Address>((key, value) =>
           {
               if (this.idToOwner.ContainsKey(key))
               {
                   this.idToOwner[key] = value;
               }
               else
               {
                   this.idToOwner.Add(key, value);
               }
           });
        this.persistentStateMock.Setup(p => p.GetAddress(It.Is<string>(s => s.StartsWith("IdToOwner:"))))
            .Returns<string>((key) =>
            {
                if (this.idToOwner.ContainsKey(key))
                {
                    return this.idToOwner[key];
                }

                return Address.Zero;
            });

        this.persistentStateMock.Setup(p => p.Clear(It.Is<string>(s => s.StartsWith("IdToOwner:"))))
            .Callback<string>((key) =>
            {
                this.idToOwner.Remove(key);
            });
    }

    private void SetupSupportedInterfaces()
    {
        this.persistentStateMock.Setup(p => p.SetBool(It.Is<string>(s => s.StartsWith("SupportedInterface:", StringComparison.Ordinal)), It.IsAny<bool>()))
            .Callback<string, bool>((key, value) =>
            {
                if (this.supportedInterfaces.ContainsKey(key))
                {
                    this.supportedInterfaces[key] = value;
                }
                else
                {
                    this.supportedInterfaces.Add(key, value);
                }
            });
        this.persistentStateMock.Setup(p => p.GetBool(It.Is<string>(s => s.StartsWith("SupportedInterface:"))))
            .Returns<string>((key) =>
            {
                if (this.supportedInterfaces.ContainsKey(key))
                {
                    return this.supportedInterfaces[key];
                }

                return default(bool);
            });
    }
}