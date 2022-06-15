using DividendTokenContract.Tests;
using Moq;
using NonFungibleTokenContract.Tests;
using Stratis.SmartContracts;
using Xunit;

public class InterFluxNonFungibleTokenTests
{
    private Mock<ISmartContractState> smartContractStateMock;
    private Mock<IContractLogger> contractLoggerMock;
    private InMemoryState state;
    private Mock<IInternalTransactionExecutor> transactionExecutorMock;
    private Address contractAddress;
    private string name;
    private string symbol;
    private bool ownerOnlyMinting;
    private string nativeChain;
    private string nativeAddress;

    public InterFluxNonFungibleTokenTests()
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
        this.nativeChain = "Ethereum";
        this.nativeAddress = "0x495f947276749ce646f68ac8c248420045cb7b5e";
    }
    
    [Fact]
    public void Constructor_Sets_Values()
    {
        var owner = "0x0000000000000000000000000000000000000005".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(owner);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.True(state.GetBool("SupportedInterface:1"));
        Assert.True(state.GetBool("SupportedInterface:2"));
        Assert.False(state.GetBool("SupportedInterface:3"));
        Assert.True(state.GetBool("SupportedInterface:4"));
        Assert.False(state.GetBool("SupportedInterface:5"));
        Assert.Equal(name, nonFungibleToken.Name);
        Assert.Equal(symbol, nonFungibleToken.Symbol);
        Assert.Equal(owner, nonFungibleToken.Owner);
        Assert.Equal(ownerOnlyMinting, state.GetBool("OwnerOnlyMinting"));
        Assert.Equal(nativeChain, nonFungibleToken.NativeChain);
        Assert.Equal(nativeAddress, nonFungibleToken.NativeAddress);
    }

    [Fact]
    public void BurnWithMetadata_NoneExistingToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.BurnWithMetadata(0, "Metadata"));
    }

    [Fact]
    public void BurnWithMetadata_BurningNotOwnedToken_ThrowsException()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        var anotherTokenOwner = "0x0000000000000000000000000000000000000007".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.SetAddress("IdToOwner:0", anotherTokenOwner);

        var nonFungibleToken = CreateNonFungibleToken();

        Assert.Throws<SmartContractAssertException>(() => nonFungibleToken.BurnWithMetadata(0, "Metadata"));
    }

    [Fact]
    public void BurnWithMetadata_BurningAToken_Success()
    {
        var ownerAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        smartContractStateMock.Setup(m => m.Message.Sender).Returns(ownerAddress);
        state.SetAddress("IdToOwner:1", ownerAddress);
        state.SetUInt256($"Balance:{ownerAddress}", 1);

        var nonFungibleToken = CreateNonFungibleToken();

        nonFungibleToken.BurnWithMetadata(1, "Metadata");

        Assert.Equal(Address.Zero, state.GetAddress("IdToOwner:1"));
        Assert.Equal(0, state.GetUInt256($"Balance:{ownerAddress}"));
        Assert.Null(nonFungibleToken.TokenURI(1));
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxNonFungibleToken.TransferLog() { From = ownerAddress, To = Address.Zero, TokenId = 1 }));
        contractLoggerMock.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxNonFungibleToken.BurnMetadata() { From = ownerAddress, Amount = 1, Metadata = "Metadata"}));
    }

    private InterFluxNonFungibleToken CreateNonFungibleToken()
    {
        return new InterFluxNonFungibleToken(smartContractStateMock.Object, name, symbol, ownerOnlyMinting, nativeChain, nativeAddress);
    }
}
