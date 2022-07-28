using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Xunit;

namespace NonFungibleTicketContract.Tests
{
    public class NonFungibleTicketTests
    {
        private readonly Mock<ISmartContractState> smartContractStateMock;
        private readonly Mock<IContractLogger> contractLoggerMock;
        private readonly Mock<IMessage> message;
        private readonly Mock<IInternalTransactionExecutor> transactionExecutorMock;
        private InMemoryState state;
        private Address owner;
        private string name;
        private string symbol;

        public NonFungibleTicketTests()
        {
            this.contractLoggerMock = new Mock<IContractLogger>();
            this.smartContractStateMock = new Mock<ISmartContractState>();
            this.transactionExecutorMock = new Mock<IInternalTransactionExecutor>();
            this.message = new Mock<IMessage>();
            this.state = new InMemoryState();
            this.smartContractStateMock.Setup(s => s.PersistentState).Returns(this.state);
            this.smartContractStateMock.Setup(s => s.ContractLogger).Returns(this.contractLoggerMock.Object);
            this.smartContractStateMock.Setup(s => s.InternalTransactionExecutor).Returns(this.transactionExecutorMock.Object);
            this.smartContractStateMock.Setup(s => s.Message).Returns(this.message.Object);
            this.owner = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.name = "Non-Fungible Ticket";
            this.symbol = "NFT";
            smartContractStateMock.Setup(m => m.Message.Sender).Returns(owner);
        }
        
        [Fact]
        public void Constructor_Sets_Values()
        {
            CreateNonFungibleTicket();
            Assert.True(state.GetBool($"SupportedInterface:{(int)TokenInterface.IRedeemableTicketPerks}"));
            Assert.True(state.GetBool("OwnerOnlyMinting"));
        }

        [Fact]
        public void GetRedemptions_TokenNotMinted_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.Invoking(token => token.GetRedemptions(5))
               .Should()
               .ThrowExactly<SmartContractAssertException>()
               .WithMessage("Token id does not exist");
        }

        [Fact]
        public void GetRedemptions_TokenMintedButNothingRedeemed_DefaultValue()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.GetRedemptions(1).Should().BeEquivalentTo(new bool[256]);
        }

        [Fact]
        public void GetRedemptions_SomePerksRedeemed_ReturnRedeemedPerks()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.RedeemPerks(1, new byte[] { 1, 4, 8 });
            var redeemedPerks = new bool[256];
            redeemedPerks[1] = true;
            redeemedPerks[4] = true;
            redeemedPerks[8] = true;
            nft.GetRedemptions(1).Should().BeEquivalentTo(redeemedPerks);
        }

        [Fact]
        public void IsRedeemed_TokenNotMinted_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.Invoking(token => token.IsRedeemed(5, 0))
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("Token id does not exist");
        }

        [Fact]
        public void IsRedeemed_TokenMintedButNotRedeemed_DefaultValue()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.IsRedeemed(1, 5).Should().Be(false);
        }

        [Fact]
        public void IsRedeemed_TokenRedeemed_ReturnTrue()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.RedeemPerks(1, new byte[] { 5 });
            nft.IsRedeemed(1, 5).Should().Be(true);
        }

        [Fact]
        public void RedeemPerks_TokenNotMinted_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.Invoking(token => token.RedeemPerks(5, new byte[] { 0 }))
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("Token id does not exist");
        }

        [Fact]
        public void RedeemPerks_NotContractOwner_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            message.SetupGet(callTo => callTo.Sender).Returns("0x0000000000000050000000003000000400000008".HexToAddress());
            nft.Invoking(token => token.RedeemPerks(5, new byte[] { 0 }))
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("The method is owner only.");
        }

        [Fact]
        public void RedeemPerks_NoPerksToRedeem_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.Invoking(token => token.RedeemPerks(1, Array.Empty<byte>()))
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("Must provide at least one perk to redeem.");
        }

        [Fact]
        public void RedeemPerks_PerkAlreadyRedeemed_AssertFailure()
        {
            var nft = CreateNonFungibleTicket();
            message.SetupGet(callTo => callTo.Sender).Returns(owner);
            nft.Mint("0x0000000000000000000000000000000000000005".HexToAddress(), "https://nft.data/1");
            nft.RedeemPerks(1, new byte[] { 4 });
            nft.Invoking(token => token.RedeemPerks(1, new byte[] { 1, 4, 8 }))
                .Should()
                .ThrowExactly<SmartContractAssertException>()
                .WithMessage("Perk at index 4 already redeemed.");
        }
        
        private NonFungibleTicket CreateNonFungibleTicket() => new NonFungibleTicket(smartContractStateMock.Object, name, symbol);
    }
}