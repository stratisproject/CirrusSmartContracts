﻿using DividendTokenContract.Tests;
using FluentAssertions;
using Moq;
using Moq.Language.Flow;
using NonFungibleTokenContract.Tests;
using Stratis.SmartContracts;
using System;
using System.Linq;
using Xunit;
using static NonFungibleToken;

public class NonFungibleTokenTests
{
    private Mock<ISmartContractState> smartContractStateMock;
    private Mock<IContractLogger> contractLoggerMock;
    private InMemoryState state;
    private Mock<IInternalTransactionExecutor> transactionExecutorMock;
    private Address contractAddress;
    private string name;
    private string symbol;
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
        this.ownerOnlyMinting = true;
    }

    public string GetTokenURI(UInt256 tokenId) => $"https://example.com/api/tokens/{tokenId}";

    [Fact]
    public void Constructor_Sets_Values()
    {
        var owner = "0x0000000000000000000000000000000000000005".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(owner);

        var royaltyPercent = 10_000u;

        var nonFungibleToken = CreateNonFungibleToken(owner, royaltyPercent);

        Assert.True(state.GetBool("SupportedInterface:1"));
        Assert.True(state.GetBool("SupportedInterface:2"));
        Assert.False(state.GetBool("SupportedInterface:3"));
        Assert.True(state.GetBool("SupportedInterface:4"));
        Assert.False(state.GetBool("SupportedInterface:5"));
        Assert.True(state.GetBool("SupportedInterface:6"));
        Assert.Equal(name, nonFungibleToken.Name);
        Assert.Equal(symbol, nonFungibleToken.Symbol);
        Assert.Equal(owner, nonFungibleToken.Owner);
        Assert.Equal(owner, state.GetAddress("RoyaltyRecipient"));
        Assert.Equal(royaltyPercent, state.GetUInt32("RoyaltyPercent"));

        Assert.Equal(ownerOnlyMinting, state.GetBool("OwnerOnlyMinting"));
    }

    [Fact]
    public void Constructor_SupportedInterface_6_Returns_False_When_Either_RoyaltyParameters_Are_Default()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var royaltyAmount = 0u;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(owner);

        var nonFungibleToken = CreateNonFungibleToken(owner, royaltyAmount);

        Assert.False(state.GetBool("SupportedInterface:6"));
    }

    [Fact]
    public void SetPendingOwner_Success()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, owner, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SetPendingOwner(newOwner);

        nonFungibleToken.Owner
                        .Should()
                        .Be(owner);

        nonFungibleToken.PendingOwner
                .Should()
                .Be(newOwner);

        var log = new OwnershipTransferRequestedLog { CurrentOwner = owner, PendingOwner = newOwner };
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), log));
    }

    [Fact]
    public void SetPendingOwner_Called_By_NonOwner_Fails()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, owner, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, newOwner, 0));

        nonFungibleToken.Invoking(c => c.SetPendingOwner(newOwner))
                        .Should()
                        .ThrowExactly<SmartContractAssertException>()
                        .WithMessage("The method is owner only.");
    }

    [Fact]
    public void ClaimOwnership_Not_Called_By_NewOwner_Fails()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, owner, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SetPendingOwner(newOwner);

        nonFungibleToken.Invoking(c => c.ClaimOwnership())
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("ClaimOwnership must be called by the new(pending) owner.");
    }

    [Fact]
    public void ClaimOwnership_Success()
    {
        var owner = "0x0000000000000000000000000000000000000002".HexToAddress();
        var newOwner = "0x0000000000000000000000000000000000000003".HexToAddress();

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, owner, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SetPendingOwner(newOwner);

        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, newOwner, 0));

        nonFungibleToken.ClaimOwnership();

        nonFungibleToken.Owner
                .Should()
                .Be(newOwner);

        nonFungibleToken.PendingOwner
                        .Should()
                        .Be(Address.Zero);

        var log = new OwnershipTransferedLog { PreviousOwner = owner, NewOwner = newOwner };
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), log));
    }

    [Fact]
    public void SupportsInterface_InterfaceSupported_ReturnsTrue()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(2);

        Assert.True(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceSetToFalseSupported_ReturnsFalse()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();
        state.SetBool("SupportedInterface:2", false);

        var result = nonFungibleToken.SupportsInterface(3);

        Assert.False(result);
    }

    [Fact]
    public void SupportsInterface_InterfaceNotSupported_ReturnsFalse()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.SupportsInterface(999);

        Assert.False(result);
    }


    [Fact]
    public void GetApproved_NotValidNFToken_OwnerAddressZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.GetApproved(1));
    }

    [Fact]
    public void GetApproved_ApprovalNotInStorage_ReturnsZeroAddress()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));
        state.SetAddress("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(Address.Zero, result);
    }

    [Fact]
    public void GetApproved_ApprovalInStorage_ReturnsAddress()
    {

        var approvalAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", "0x0000000000000000000000000000000000000005".HexToAddress());
        state.SetAddress("IdToApproval:1", approvalAddress);

        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();
        var result = nonFungibleToken.GetApproved(1);

        Assert.Equal(approvalAddress, result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsTrue_ReturnsTrue()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", true);
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.True(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorInStateAsFalse_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddresss}", false);
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void IsApprovedForAll_OwnerToOperatorNotInState_ReturnsFalse()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddresss = "0x0000000000000000000000000000000000000007".HexToAddress();
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.IsApprovedForAll(ownerAddress, operatorAddresss);

        Assert.False(result);
    }

    [Fact]
    public void OwnerOf_IdToOwnerNotInStorage_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenMappedToAddressZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        state.SetAddress("IdToOwner:1", Address.Zero);
        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.OwnerOf(1));
    }

    [Fact]
    public void OwnerOf_NFTokenExistsWithOwner_ReturnsOwnerAddress()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.OwnerOf(1);

        Assert.Equal(ownerAddress, result);
    }

    [Fact]
    public void BalanceOf_OwnerZero_ThrowsException()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => { nonFungibleToken.BalanceOf(Address.Zero); });
    }

    [Fact]
    public void BalanceOf_NftTokenCountNotInStorage_ReturnsZero()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();

        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BalanceOf_OwnerNftTokenCountInStorage_ReturnsTokenCount()
    {
        var sender = "0x0000000000000000000000000000000000000002".HexToAddress();
        smartContractStateMock.SetupGet(m => m.Message).Returns(new Message(contractAddress, sender, 0));

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetUInt256($"Balance:{ownerAddress}", 15);
        var nonFungibleToken = CreateNonFungibleToken();

        var result = nonFungibleToken.BalanceOf(ownerAddress);

        Assert.Equal(15, result);
    }

    [Fact]
    public void SetApprovalForAll_SetsMessageSender_ToOperatorApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SetApprovalForAll(operatorAddress, true);

        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalForAllLog { Owner = ownerAddress, Operator = operatorAddress, Approved = true }));
    }

    [Fact]
    public void Approve_TokenOwnerNotMessageSenderOrOperator_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_SwitchesOwnerToApprovedForNFToken()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.Equal(state.GetAddress("IdToApproval:1"), someAddress);
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_NFTokenOwnerSameAsMessageSender_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(ownerAddress, 1));
    }

    [Fact]
    public void Approve_ValidApproval_ByApprovedOperator_SwitchesOwnerToApprovedForNFToken()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.Approve(someAddress, 1);

        Assert.Equal(state.GetAddress("IdToApproval:1"), someAddress);
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.ApprovalLog { Owner = ownerAddress, Approved = someAddress, TokenId = 1 }));
    }

    [Fact]
    public void Approve_InvalidNFToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var someAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", Address.Zero);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Approve(someAddress, 1));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", Address.Zero);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(Address.Zero, targetAddress, 1));
    }


    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetAddress("IdToApproval:1", approvalAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void TransferFrom_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void TransferFrom_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.TransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetAddress("IdToApproval:1", approvalAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1).Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance::{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetAddress("IdToApproval:1", approvalAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, approvalAddress, ownerAddress, 1).Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, operatorAddress, ownerAddress, 1).Returns(TransferResult.Transferred(true));


        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", Address.Zero);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(Address.Zero, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1).Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    private IReturnsThrows<IInternalTransactionExecutor, ITransferResult> SetupForOnNonFungibleTokenReceived(Address targetAddress, Address @operator, Address from, UInt256 tokenId)
    {
        return transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", It.IsAny<object[]>(), 0ul))
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
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        SetupForOnNonFungibleTokenReceived(targetAddress, ownerAddress, ownerAddress, 1).Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1));
    }

    [Fact]
    public void SafeTransferFrom_NoDataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetAddress("IdToApproval:1", approvalAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractFalse_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff });

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));

        transactionExecutorMock.Verify(t => t.Call(It.IsAny<ISmartContractState>(), It.IsAny<Address>(), It.IsAny<ulong>(), "OnNonFungibleTokenReceived", It.IsAny<object[]>(), It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSender_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (UInt256)1, data };

        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_MessageSenderApprovedForTokenIdByOwner_TransfersTokenFrom_To_ClearsApproval()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var approvalAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetAddress("IdToApproval:1", approvalAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(approvalAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { approvalAddress, ownerAddress, (UInt256)1, data };

        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTrue_ValidTokenTransfer_MessageSenderApprovedOwnerToOperator_TransfersTokenFrom_To()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var operatorAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}", true);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(operatorAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { operatorAddress, ownerAddress, (UInt256)1, data };

        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.True(state.GetBool($"OwnerToOperator:{ownerAddress}:{operatorAddress}"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_MessageSenderNotAllowedToCall_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var invalidSenderAddress = "0x0000000000000000000000000000000000000015".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(invalidSenderAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_NFTokenOwnerZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", Address.Zero);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(Address.Zero);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(Address.Zero, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_TokenDoesNotBelongToFrom_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var notOwningAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(notOwningAddress, targetAddress, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ValidTokenTransfer_ToContractReturnsFalse_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();

        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (UInt256)1, data };

        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0))
                                    .Returns(TransferResult.Transferred(false));

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToContractTrue_ContractCallReturnsTruthyObject_CannotCastToBool_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.IsContractResult = true;
        var nonFungibleToken = CreateNonFungibleToken();


        var data = new byte[] { 12 };
        var callParamsExpected = new object[] { ownerAddress, ownerAddress, (UInt256)1, data };
        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", callParamsExpected, 0))
                                    .Returns(TransferResult.Transferred(1));

        Assert.Throws<InvalidCastException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, targetAddress, 1, data));
    }

    [Fact]
    public void SafeTransferFrom_DataProvided_ToAddressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.SafeTransferFrom(ownerAddress, Address.Zero, 1, new byte[1] { 0xff }));
    }

    [Fact]
    public void Mint_CalledByNonOwner_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var userAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(userAddress);

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(userAddress, GetTokenURI(1)));
    }

    [Fact]
    public void Mint_ToAdressZero_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Mint(Address.Zero, GetTokenURI(1)));
    }

    [Fact]
    public void Mint_MintingNewToken_Called_By_None_Owner_When_OwnerOnlyMintingFalse_Success()
    {
        ownerOnlyMinting = false;

        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();

        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(targetAddress);
        nonFungibleToken.Mint(targetAddress, GetTokenURI(1));

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));
        Assert.Equal(GetTokenURI(1), nonFungibleToken.TokenURI(1));
        Assert.Equal(1, state.GetUInt256("TokenIdCounter"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void Mint_MintingNewToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.Mint(targetAddress, GetTokenURI(1));

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));
        Assert.Equal(GetTokenURI(1), nonFungibleToken.TokenURI(1));
        Assert.Null(nonFungibleToken.TokenURI(2));
        Assert.Equal(1, state.GetUInt256("TokenIdCounter"));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void SafeMint_MintingNewToken_When_Destination_Is_Contract_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        state.IsContractResult = true;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken();
        var data = new byte[] { 12 };
        var parameter = new object[] { ownerAddress, Address.Zero, (UInt256)1, data };
        transactionExecutorMock.Setup(t => t.Call(It.IsAny<ISmartContractState>(), targetAddress, 0, "OnNonFungibleTokenReceived", parameter, 0))
                                    .Returns(TransferResult.Transferred(true));

        nonFungibleToken.SafeMint(targetAddress, GetTokenURI(1), data);

        Assert.Equal(targetAddress, state.GetAddress("IdToOwner:1"));
        Assert.Equal(1, state.GetUInt256($"Balance:{targetAddress}"));
        Assert.Equal(1, state.GetUInt256("TokenIdCounter"));
        Assert.Equal(GetTokenURI(1), nonFungibleToken.TokenURI(1));
        Assert.Null(nonFungibleToken.TokenURI(2));

        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = Address.Zero, To = targetAddress, TokenId = 1 }));
    }

    [Fact]
    public void Burn_NoneExistingToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Burn(0));
    }

    [Fact]
    public void Burn_BurningNotOwnedToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var anotherTokenOwner = "0x0000000000000000000000000000000000000007".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.SetAddress("IdToOwner:0", anotherTokenOwner);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.Burn(0));
    }

    [Fact]
    public void Burn_BurningAToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.Burn(1);

        Assert.Equal(Address.Zero, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Null(nonFungibleToken.TokenURI(1));
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new NonFungibleToken.TransferLog { From = ownerAddress, To = Address.Zero, TokenId = 1 }));
    }

    [Fact]
    public void RoyaltyInfo_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var tokenId = 1;
        uint royaltyPercentage = 1234;
        var salePrice = ulong.MaxValue;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken(ownerAddress, royaltyPercentage);

        nonFungibleToken.Mint(targetAddress, GetTokenURI(tokenId));

        var royaltyInfo = nonFungibleToken.RoyaltyInfo(tokenId, salePrice);

        Assert.True(royaltyInfo.Length == 2);
        var recipient = (Address)royaltyInfo[0];
        var royaltyAmount = (ulong)royaltyInfo[1];

        Assert.True(royaltyAmount > 0);
        Assert.Equal(ownerAddress, recipient);
        // Expected value is 2276328218695758669.291 sats, but obviously the .291 isn't possible
        Assert.Equal(2276328218695758669UL, royaltyAmount);
    }

    [Fact]
    public void Set_Invalid_RoyaltyPercent_Fails()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        uint royaltyPercentage = 10001;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        Assert.Throws<SmartContractAssertException>(() => CreateNonFungibleToken(ownerAddress, royaltyPercentage));
    }

    [Fact]
    public void RoyaltyInfo_RoyaltyPercent_0()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var tokenId = 1;
        uint royaltyPercentage = 0;
        var salePrice = ulong.MaxValue;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken(ownerAddress, royaltyPercentage);

        nonFungibleToken.Mint(targetAddress, GetTokenURI(tokenId));

        var royaltyInfo = nonFungibleToken.RoyaltyInfo(tokenId, salePrice);

        Assert.True(royaltyInfo.Length == 2);
        var recipient = (Address)royaltyInfo[0];
        var royaltyAmount = (ulong)royaltyInfo[1];

        Assert.Equal(ownerAddress, recipient);
        Assert.Equal(0UL, royaltyAmount);
    }

    [Fact]
    public void RoyaltyInfo_RoyaltyPercent_100()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var targetAddress = "0x0000000000000000000000000000000000000007".HexToAddress();
        var tokenId = 1;
        uint royaltyPercentage = 10000;
        var salePrice = ulong.MaxValue;

        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);

        var nonFungibleToken = CreateNonFungibleToken(ownerAddress, royaltyPercentage);

        nonFungibleToken.Mint(targetAddress, GetTokenURI(tokenId));

        var royaltyInfo = nonFungibleToken.RoyaltyInfo(tokenId, salePrice);

        Assert.True(royaltyInfo.Length == 2);
        var recipient = (Address)royaltyInfo[0];
        var royaltyAmount = (ulong)royaltyInfo[1];

        Assert.Equal(ownerAddress, recipient);
        Assert.Equal(salePrice, royaltyAmount);
    }

    private NonFungibleToken CreateNonFungibleToken(Address royaltyRecipent = default, uint royaltyPercent = default)
    {
        return new NonFungibleToken(smartContractStateMock.Object, name, symbol, ownerOnlyMinting, royaltyRecipent, royaltyPercent);
    }
}