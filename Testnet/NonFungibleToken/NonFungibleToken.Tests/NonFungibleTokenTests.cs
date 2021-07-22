using DividendTokenContract.Tests;
using Moq;
using NonFungibleTokenContract.Tests;
using Stratis.SmartContracts;
using System;
using Xunit;

public class NonFungibleTokenTests
{
    private Mock<ISmartContractState> smartContractStateMock;
    private Mock<IContractLogger> contractLoggerMock;
    private InMemoryState state;
    private Mock<IInternalTransactionExecutor> internalTransactionExecutorMock;
    private Address contractAddress;
    private string name;
    private string symbol;

    public NonFungibleTokenTests()
    {
        this.contractLoggerMock = new Mock<IContractLogger>();
        this.smartContractStateMock = new Mock<ISmartContractState>();
        this.internalTransactionExecutorMock = new Mock<IInternalTransactionExecutor>();
        this.state = new InMemoryState();
        this.smartContractStateMock.Setup(s => s.PersistentState).Returns(this.state);
        this.smartContractStateMock.Setup(s => s.ContractLogger).Returns(this.contractLoggerMock.Object);
        this.smartContractStateMock.Setup(x => x.InternalTransactionExecutor).Returns(this.internalTransactionExecutorMock.Object);
        this.contractAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
        this.name = "Non-Fungible Token";
        this.symbol = "NFT";
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
        Assert.True(this.state.GetBool("SupportedInterface:5"));
        Assert.Equal(this.name, nonFungibleToken.Name);
        Assert.Equal(this.symbol, nonFungibleToken.Symbol);
        Assert.Equal(owner, nonFungibleToken.Owner);
        Assert.Equal(1ul, this.state.GetUInt64("NextTokenId"));
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
    public void TokenByIndex_TokenNotFound_ThrowsAssertException()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));
        this.state.SetUInt64("TotalSupply", 5);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TokenByIndex(5));
    }

    [Fact]
    public void TokenByIndex_TokenFound_ReturnsTokenId()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));
        this.state.SetUInt64("TotalSupply", 5);
        this.state.SetUInt64("TokenByIndex:4", 4);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Equal(4ul, nonFungibleToken.TokenByIndex(4));
    }

    [Fact]
    public void TokenOfOwnerByIndex_TokenNotFound_ThrowsAssertException()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));
        this.state.SetUInt64($"OwnerToNFTokenCount:{owner}", 5);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TokenOfOwnerByIndex(owner, 5));
    }

    [Fact]
    public void TokenOfOwnerByIndex_TokenFound_ReturnsTokenId()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        this.smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(this.contractAddress, owner, 0));
        this.state.SetUInt64($"OwnerToNFTokenCount:{owner}", 4);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{owner}:3", 4);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Equal(4ul, nonFungibleToken.TokenOfOwnerByIndex(owner, 3));
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 15);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));

        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));

        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));

        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"persistentState.GetUInt64:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetAddress("IdToApproval:1", approvalAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        this.internalTransactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        this.state.IsContractResult = true;
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

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.True(this.state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal((ulong)0, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.Equal((ulong)1, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));

        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));

        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        this.state.IsContractResult = true;
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
        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void MintAll_CalledByNonOwner_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var userAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(userAddress);

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(userAddress, 1));
    }

    [Fact]
    public void MintAll_ToAdressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(Address.Zero, 1));
    }

    [Fact]
    public void MintAll_AmountIsZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = this.CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(ownerAddress, 0));
    }

    [Fact]
    public void MintAll_MintingNewToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Mint(targetAddress, 1);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.True(this.state.ContainsKey("IndexByToken:1"));
        Assert.Equal(0ul, this.state.GetUInt64("IndexByToken:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        Assert.True(this.state.ContainsKey($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64("TokenByIndex:0"));
        Assert.Equal(2ul, this.state.GetUInt64("NextTokenId"));
        Assert.Equal(1ul, this.state.GetUInt64("TotalSupply"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void MintAll_MintingAtLeast2NewToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Mint(targetAddress, 2);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:1"));
        Assert.Equal(2ul, this.state.GetUInt64($"OwnerToNFTokenCount:{targetAddress}"));
        Assert.True(this.state.ContainsKey("IndexByToken:1"));
        Assert.Equal(0ul, this.state.GetUInt64("IndexByToken:1"));
        Assert.Equal(1ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:0"));
        Assert.True(this.state.ContainsKey($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:1"));
        Assert.Equal(1ul, this.state.GetUInt64("TokenByIndex:0"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }), Times.Once);

        Assert.Equal(targetAddress, this.state.GetAddress("IdToOwner:2"));
        Assert.True(this.state.ContainsKey("IndexByToken:2"));
        Assert.Equal(1ul, this.state.GetUInt64("IndexByToken:2"));
        Assert.Equal(2ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{targetAddress}:1"));
        Assert.True(this.state.ContainsKey($"IndexOfOwnerByToken:{targetAddress}:2"));
        Assert.Equal(1ul, this.state.GetUInt64($"IndexOfOwnerByToken:{targetAddress}:2"));
        Assert.Equal(2ul, this.state.GetUInt64("TokenByIndex:1"));

        Assert.Equal(3ul, this.state.GetUInt64("NextTokenId"));
        Assert.Equal(2ul, this.state.GetUInt64("TotalSupply"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 2 }), Times.Once);
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
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64("IndexByToken:1", 0);
        this.state.SetUInt64("TokenByIndex:0", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);
        this.state.SetUInt64("TotalSupply", 1);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Burn(1);

        Assert.False(this.state.ContainsKey("IdToOwner:1"));
        Assert.Equal(0ul, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.False(this.state.ContainsKey("IndexByToken:1"));
        Assert.False(this.state.ContainsKey("TokenByIndex:0"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:0"));
        Assert.Equal(0ul, this.state.GetUInt64("TotalSupply"));

        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = Address.Zero, TokenId = 1 }));
    }

    [Fact]
    public void Burn_BurningAToken_WhenTotalSupplyIsAtLeast2_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var secondOwnerAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        this.smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        this.state.SetAddress("IdToOwner:1", ownerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{ownerAddress}", 1);
        this.state.SetUInt64("IndexByToken:1", 0);
        this.state.SetUInt64("TokenByIndex:0", 1);
        this.state.SetUInt64($"IndexOfOwnerByToken:{ownerAddress}:1", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{ownerAddress}:0", 1);

        this.state.SetAddress("IdToOwner:2", secondOwnerAddress);
        this.state.SetUInt64($"OwnerToNFTokenCount:{secondOwnerAddress}", 1);
        this.state.SetUInt64("IndexByToken:2", 1);
        this.state.SetUInt64("TokenByIndex:1", 2);
        this.state.SetUInt64($"IndexOfOwnerByToken:{secondOwnerAddress}:2", 0);
        this.state.SetUInt64($"TokenOfOwnerByIndex:{secondOwnerAddress}:0", 2);

        this.state.SetUInt64("TotalSupply", 2);
        var nonFungibleToken = this.CreateNonFungibleToken();

        nonFungibleToken.Burn(1);

        Assert.False(this.state.ContainsKey("IdToOwner:1"));
        Assert.Equal(0ul, this.state.GetUInt64($"OwnerToNFTokenCount:{ownerAddress}"));
        Assert.False(this.state.ContainsKey("IndexByToken:1"));
        Assert.False(this.state.ContainsKey($"IndexOfOwnerByToken:{ownerAddress}:1"));
        Assert.False(this.state.ContainsKey($"TokenOfOwnerByIndex:{ownerAddress}:1"));

        Assert.Equal(2ul, this.state.GetUInt64("TokenByIndex:0"));
        Assert.Equal(secondOwnerAddress, this.state.GetAddress("IdToOwner:2"));
        Assert.Equal(1ul, this.state.GetUInt64($"OwnerToNFTokenCount:{secondOwnerAddress}"));
        Assert.Equal(0ul, this.state.GetUInt64("IndexByToken:2"));
        Assert.Equal(0ul, this.state.GetUInt64($"IndexOfOwnerByToken:{secondOwnerAddress}:2"));
        Assert.Equal(2ul, this.state.GetUInt64($"TokenOfOwnerByIndex:{secondOwnerAddress}:0"));

        Assert.Equal(1ul, this.state.GetUInt64("TotalSupply"));
        this.contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = Address.Zero, TokenId = 1 }));
    }

    private NonFungibleToken CreateNonFungibleToken()
    {
        return new NonFungibleToken(this.smartContractStateMock.Object, this.name, this.symbol);
    }
}