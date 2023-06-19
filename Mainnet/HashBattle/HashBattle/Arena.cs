using Stratis.SmartContracts;
using System;
using System.Text;

/// <summary>
/// A Stratis smart contract for running a game battle where owner will start the battle and maximum 4 users can enter a battle
/// </summary>
public class Arena : SmartContract
{
    private void SetBattle(ulong battleId, BattleMain battle)
    {
        State.SetStruct($"battle:{battleId}", battle);
    }
    public BattleMain GetBattle(ulong battleId)
    {
        return State.GetStruct<BattleMain>($"battle:{battleId}");
    }
    private void SetUser(ulong battleId, Address address, BattleUser user)
    {
        State.SetStruct($"user:{battleId}:{address}", user);
    }
    public BattleUser GetUser(ulong battleId, Address address)
    {
        return State.GetStruct<BattleUser>($"user:{battleId}:{address}");
    }
    private void SetHighestScorer(ulong battleId, BattleHighestScorer highestScorer)
    {
        State.SetStruct($"scorer:{battleId}", highestScorer);
    }
    public BattleHighestScorer GetHighestScorer(ulong battleId)
    {
        return State.GetStruct<BattleHighestScorer>($"scorer:{battleId}");
    }
    private void SetUserIndex(ulong battleId, uint userindex)
    {
        State.SetUInt32($"user:{battleId}", userindex);
    }
    private uint GetUserIndex(ulong battleId)
    {
        return State.GetUInt32($"user:{battleId}");
    }
    private void SetScoreSubmittedCount(ulong battleId, uint scoresubmitcount)
    {
        State.SetUInt32($"scoresubmit:{battleId}", scoresubmitcount);
    }
    private uint GetScoreSubmittedCount(ulong battleId)
    {
        return State.GetUInt32($"scoresubmit:{battleId}");
    }
    /// <summary>
    /// Set the address deploying the contract as battle owner
    /// </summary>
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    public Address PendingOwner
    {
        get => State.GetAddress(nameof(PendingOwner));
        private set => State.SetAddress(nameof(PendingOwner), value);
    }
    public uint MaxUsers
    {
        get => State.GetUInt32(nameof(MaxUsers));
        private set => State.SetUInt32(nameof(MaxUsers), value);
    }
    /// <summary>
    /// Set the unique battleId of each battle
    /// </summary>
    public ulong NextBattleId
    {
        get => State.GetUInt64(nameof(NextBattleId));
        private set => State.SetUInt64(nameof(NextBattleId), value);
    }

    public Arena(ISmartContractState smartContractState, uint maxUsers) : base(smartContractState)
    {
        Owner = Message.Sender;
        MaxUsers = maxUsers;
        NextBattleId = 1;
    }

    /// <summary>
    /// Only owner can set new owner and new owner will be in pending state 
    /// till new owner will call <see cref="ClaimOwnership"></see> method. 
    /// </summary>
    /// <param name="newOwner">The new owner which is going to be in pending state</param>
    public void SetPendingOwner(Address newOwner)
    {
        EnsureOwnerOnly();
        PendingOwner = newOwner;

        Log(new OwnershipTransferRequestedLog { CurrentOwner = Owner, PendingOwner = newOwner });
    }

    /// <summary>
    /// Waiting be called after new owner is requested by <see cref="SetPendingOwner"/> call.
    /// Pending owner will be new owner after successfull call. 
    /// </summary>
    public void ClaimOwnership()
    {
        var newOwner = PendingOwner;

        Assert(newOwner == Message.Sender, "ClaimOwnership must be called by the new(pending) owner.");

        var oldOwner = Owner;
        Owner = newOwner;
        PendingOwner = Address.Zero;

        Log(new OwnershipTransferedLog { PreviousOwner = oldOwner, NewOwner = newOwner });
    }
    /// <summary>
    /// Battle owner will start the battle
    /// </summary>
    public ulong StartBattle(ulong fee)
    {
        Assert(Message.Sender == Owner, "Only battle owner can start game.");
        Assert(fee < ulong.MaxValue / MaxUsers, "Fee is too high");

        var battleId = NextBattleId;
        NextBattleId += 1;

        var battle = new BattleMain
        {
            BattleId = battleId,
            Fee = fee,
            Users = new Address[MaxUsers]
        };
        SetBattle(battleId, battle);

        Log(new BattleStartedLog { BattleId = battleId, Address = Message.Sender });
        return battleId;
    }
    /// <summary>
    /// 4 different user will enter the battle
    /// </summary>
    public void EnterBattle(ulong battleId)
    {
        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle not found.");

        Assert(battle.Fee == Message.Value, "Battle fee is not matching with entry fee paid.");

        var user = GetUser(battleId, Message.Sender);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        SetUser(battleId, Message.Sender, user);

        var userindex = GetUserIndex(battleId);
        Assert(userindex != MaxUsers, "Max user reached for this battle.");
        battle.Users.SetValue(Message.Sender, userindex);
        SetUserIndex(battleId, userindex + 1);

        SetBattle(battleId, battle);

        Log(new BattleEnteredLog { BattleId = battleId, Address = Message.Sender });
    }
    /// <summary>
    /// 4 different user will end the battle and submit the score
    /// </summary>
    public void EndBattle(Address userAddress, ulong battleId, uint score)
    {
        Assert(Message.Sender == Owner, "Only battle owner can end game.");

        var ScoreSubmittedCount = GetScoreSubmittedCount(battleId);
        Assert(ScoreSubmittedCount < MaxUsers, "All users already submitted score.");

        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle not found.");

        var user = GetUser(battleId, userAddress);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.ScoreSubmitted = true;

        SetUser(battleId, userAddress, user);

        ScoreSubmittedCount += 1;
        SetScoreSubmittedCount(battleId, ScoreSubmittedCount);

        var highestScorer = GetHighestScorer(battleId);

        if (score > highestScorer.Score)
        {
            highestScorer.Score = score;
            highestScorer.HighestScorer = userAddress;
            highestScorer.HighestScoreCount = 1;

            SetHighestScorer(battleId, highestScorer);
        }
        else if (score == highestScorer.Score)
        {
            highestScorer.HighestScoreCount++;
            SetHighestScorer(battleId, highestScorer);
        }

        if (ScoreSubmittedCount == MaxUsers)
        {
            highestScorer = GetHighestScorer(battleId);
            if (highestScorer.HighestScoreCount > 1)
                CancelBattle(battle);
            else
                ProcessWinner(battle, highestScorer.HighestScorer);
        }

        Log(new BattleEndedLog { BattleId = battleId, Address = Message.Sender });
    }
    /// <summary>
    /// Get winner address
    /// </summary>
    public Address GetWinner(ulong battleId)
    {
        var battle = GetBattle(battleId);
        return battle.Winner;
    }
    /// <summary>
    /// Process winner when all user scores are submitted
    /// </summary>
    private void ProcessWinner(BattleMain battle, Address winnerAddress)
    {
        battle.Winner = winnerAddress;
        SetBattle(battle.BattleId, battle);
        ProcessPrize(battle);
    }
    /// <summary>
    /// Send 3/4 amount to winner and 1/4 amount to battle owner
    /// </summary>
    private void ProcessPrize(BattleMain battle)
    {
        var prize = battle.Fee * (MaxUsers - 1);
        Transfer(battle.Winner, prize);
        Transfer(Owner, battle.Fee);
    }
    /// <summary>
    /// Cancel battle and refund the fee amount
    /// </summary>
    private void CancelBattle(BattleMain battle)
    {
        battle.IsCancelled = true;
        SetBattle(battle.BattleId, battle);

        Transfer(battle.Users[0], battle.Fee);
        Transfer(battle.Users[1], battle.Fee);
        Transfer(battle.Users[2], battle.Fee);
        Transfer(battle.Users[3], battle.Fee);
    }
    private void EnsureOwnerOnly()
    {
        Assert(Message.Sender == Owner, "The method is owner only.");
    }
    public struct BattleMain
    {
        public ulong BattleId;
        public Address Winner;
        public Address[] Users;
        public ulong Fee;
        public bool IsCancelled;
    }
    public struct BattleUser
    {
        public bool ScoreSubmitted;
    }
    public struct BattleHighestScorer
    {
        public uint Score;
        public uint HighestScoreCount;
        public Address HighestScorer;
    }
    public struct OwnershipTransferedLog
    {
        [Index] public Address PreviousOwner;
        [Index] public Address NewOwner;
    }
    public struct OwnershipTransferRequestedLog
    {
        [Index] public Address CurrentOwner;
        [Index] public Address PendingOwner;
    }
    public struct BattleStartedLog
    {
        [Index] public ulong BattleId;
        [Index] public Address Address;
    }
    public struct BattleEnteredLog
    {
        [Index] public ulong BattleId;
        [Index] public Address Address;
    }
    public struct BattleEndedLog
    {
        [Index] public ulong BattleId;
        [Index] public Address Address;
    }
}