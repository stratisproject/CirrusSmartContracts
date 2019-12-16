using System;
using Stratis.SmartContracts;


//command to compile: 
//Stratis.SmartContracts.Tools.Sct validate WheelGame.cs -sb
public class Wheel : SmartContract
{
  public bool IsGameStarted
  {
    get => this.PersistentState.GetBool(nameof(this.IsGameStarted));
    private set => this.PersistentState.SetBool(nameof(this.IsGameStarted), value);
  }

  //every new bid starts new round, this variable counts them
  public uint RoundCounter
  {
    get => this.PersistentState.GetUInt32(nameof(this.RoundCounter));
    private set => this.PersistentState.SetUInt32(nameof(this.RoundCounter), value);
  }

  //block number at which game will be completed
  public ulong TimeoutBlock
  {
    get => this.PersistentState.GetUInt64(nameof(this.TimeoutBlock));
    private set => this.PersistentState.SetUInt64(nameof(this.TimeoutBlock), value);
  }

  //game pool
  public ulong Staked
  {
    get => this.PersistentState.GetUInt64(nameof(this.Staked));
    private set => this.PersistentState.SetUInt64(nameof(this.Staked), value);
  }

  //a bid required to participate in a game
  public ulong Bid
  {
    get => this.PersistentState.GetUInt64(nameof(this.Bid));
    private set => this.PersistentState.SetUInt64(nameof(this.Bid), value);
  }

  //how much blocks one needs to wait to take the game pool
  public byte BlockDelay
  {
    get => (byte)this.PersistentState.GetChar(nameof(this.BlockDelay));
    private set => this.PersistentState.SetChar(nameof(this.BlockDelay), (char)value);
  }

  //an address of the last participant commited a bid
  public Address LastBidOwner
  {
    get => this.PersistentState.GetAddress(nameof(this.LastBidOwner));
    private set => this.PersistentState.SetAddress(nameof(this.LastBidOwner), value);
  }

  public Wheel(ISmartContractState smartContractState, ulong bidSat, byte blockDelay)
  : base(smartContractState)
  {
    Bid = bidSat;
    BlockDelay = blockDelay;

    GoToStartState();
  }

  private void GoToStartState()
  {
    IsGameStarted = false;
    Staked = 0;
    LastBidOwner = Address.Zero;
    TimeoutBlock = this.Block.Number - 1;
    RoundCounter = 0;
  }

  public ulong GetBalance(Address address)
  {
    return PersistentState.GetUInt64($"balance:{address}");
  }

  private void SetBalance(Address address, ulong value)
  {
    PersistentState.SetUInt64($"balance:{address}", value);
  }

  public void StakeBid()
  {
    if (!IsGameStarted)
    {
      StartRound();
    }
    else if (IsTimeout())
    {
      ulong amount = GetBalance(this.Message.Sender);
      ulong newAmount = amount + Staked;
      SetBalance(LastBidOwner, newAmount);
      Log(new WinnerLog { Winner = LastBidOwner, Amount = Staked });

      StartRound();
    }
    else if (IsGameStarted)
    {
      //it doesn't make sense to make a bid if you are already LastBidOwner
      //Assert(LastBidOwner != this.Message.Sender);

      Assert(Bid == this.Message.Value);
      LastBidOwner = this.Message.Sender;
      Staked = checked(Staked + this.Message.Value);
      RefreshBlockTimeout();
      ++RoundCounter;
    }
    else
    {
      Assert(false, "Something is definitely wrong. It never should have happenned");
    }

    Log(new NewBidLog { Owner = this.Message.Sender, BlockNumber = this.Block.Number });
  }

  private bool IsTimeout()
  {
    return this.Block.Number >= TimeoutBlock && IsGameStarted;
  }

  private void StartRound()
  {
    Staked = 0;
    RoundCounter = 1;
    RefreshBlockTimeout();
    LastBidOwner = this.Message.Sender;
    Staked = this.Message.Value;
    IsGameStarted = true;
  }

  private void RefreshBlockTimeout()
  {
    TimeoutBlock = this.Block.Number + this.BlockDelay;
  }

  public bool Withdraw()
  {
    //withdraw money and move game to Start state
    if (IsTimeout())
    {
      ulong amount = GetBalance(this.Message.Sender);
      ulong toWithdraw = checked(amount + Staked);
      ulong staked = Staked;

      Assert(toWithdraw > 0);

      SetBalance(this.Message.Sender, 0);
      Staked = 0;

      ITransferResult transferResult = Transfer(this.Message.Sender, toWithdraw);

      if (!transferResult.Success)
      {
        this.SetBalance(this.Message.Sender, amount);
        Staked = staked;
      }

      Log(new WinnerLog { Winner = this.Message.Sender, Amount = staked });

      GoToStartState();

      return transferResult.Success;
    }
    else
    {
      ulong amount = GetBalance(this.Message.Sender);
      Assert(amount > 0);
      SetBalance(this.Message.Sender, 0);

      ITransferResult transferResult = Transfer(this.Message.Sender, amount);

      if (!transferResult.Success)
        this.SetBalance(this.Message.Sender, amount);

      return transferResult.Success;
    }
  }

  //is used to display history of winners
  public struct WinnerLog
  {
    [Index]
    public Address Winner;
    public ulong Amount;
  }

  //is ised to diplay history of participants
  public struct NewBidLog
  {
    [Index]
    public Address Owner;
    public ulong BlockNumber;
  }
}