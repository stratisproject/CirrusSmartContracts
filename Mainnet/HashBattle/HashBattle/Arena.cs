using Stratis.SmartContracts;
using System;
using System.Text;

/// <summary>
/// A Stratis smart contract for running a game battle where owner will start the battle and maximum 4 users can enter a battle
/// </summary>
public class Arena : SmartContract
{
    public Arena(ISmartContractState smartContractState)
    : base(smartContractState)
    {
        BattleOwner = Message.Sender;
        NextBattleId = 1;
    }

    /// <summary>
    /// Set the address deploying the contract as battle owner
    /// </summary>
    private Address BattleOwner
    {
        get => PersistentState.GetAddress(nameof(BattleOwner));
        set => PersistentState.SetAddress(nameof(BattleOwner), value);
    }

    public Address PendingBattleOwner
    {
        get => PersistentState.GetAddress(nameof(PendingBattleOwner));
        private set => PersistentState.SetAddress(nameof(PendingBattleOwner), value);
    }

    public void SetPendingBattleOwnership(Address pendingBattleOwner)
    {
        Assert(Message.Sender == BattleOwner, "UNAUTHORIZED");

        PendingBattleOwner = pendingBattleOwner;

        Log(new SetPendingDeployerOwnershipLog { From = Message.Sender, To = pendingBattleOwner });
    }

    public void ClaimPendingbattleOwnership()
    {
        var pendingBattleOwner = PendingBattleOwner;

        Assert(Message.Sender == pendingBattleOwner, "UNAUTHORIZED");

        var oldOwner = BattleOwner;

        BattleOwner = pendingBattleOwner;
        PendingBattleOwner = Address.Zero;

        Log(new ClaimPendingDeployerOwnershipLog { From = oldOwner, To = pendingBattleOwner });
    }

    /// <summary>
    /// Set the unique battleid of each battle
    /// </summary>
    public ulong NextBattleId
    {
        get => PersistentState.GetUInt64(ArenaStateKeys.NextBattleId);
        private set => PersistentState.SetUInt64(ArenaStateKeys.NextBattleId, value);
    }

    /// <summary>
    /// Battle owner will start the battle
    /// </summary>
    public ulong StartBattle(ulong fee)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can start game.");

        ulong battleId = NextBattleId;
        NextBattleId += 1;

        var battle = new BattleMain();
        battle.BattleId = battleId;
        battle.Fee = fee;
        battle.Users = new Address[MaxUsers];
        SetBattle(battleId, battle);

        Log(new BattleEventLog { Event = "Start", BattleId = battleId, Address = Message.Sender });
        return battleId;
    }

    /// <summary>
    /// 4 different user will enter the battle
    /// </summary>
    public void EnterBattle(ulong battleId)
    {
        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle ended.");

        Assert(battle.Fee == Message.Value, "Battle fee is not matching with entry fee paid.");

        var user = GetUser(battleId, Message.Sender);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Address = Message.Sender;

        SetUser(battleId, Message.Sender, user);

        uint userindex = GetUserIndex(battleId);
        battle.Users.SetValue(user.Address, userindex);
        SetUserIndex(battleId, (userindex + 1));

        SetBattle(battleId, battle);

        Log(new BattleEventLog { Event = "Enter", BattleId = battleId, Address = Message.Sender });
    }

    /// <summary>
    /// 4 different user will end the battle and submit the score
    /// </summary>
    public void EndBattle(Address userAddress, ulong battleId, uint score)
    {
        Assert(Message.Sender == BattleOwner, "Only battle owner can end game.");

        var battle = GetBattle(battleId);

        Assert(battle.Winner == Address.Zero, "Battle ended.");

        var user = GetUser(battleId, userAddress);

        Assert(!user.ScoreSubmitted, "The user already submitted score.");

        user.Score = score;
        user.ScoreSubmitted = true;

        SetUser(battleId, userAddress, user);

        uint ScoreSubmittedCount = GetScoreSubmittedCount(battleId);
        ScoreSubmittedCount += 1;
        if (ScoreSubmittedCount == MaxUsers)
            ProcessWinner(battle);

        SetScoreSubmittedCount(battleId, ScoreSubmittedCount);
        Log(new BattleEventLog { Event = "End", BattleId = battleId, Address = Message.Sender });
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
    private void ProcessWinner(BattleMain battle)
    {
        uint winnerIndex = GetWinnerIndex(battle.BattleId, battle.Users);
        battle.Winner = battle.Users[winnerIndex];
        SetBattle(battle.BattleId, battle);
        ProcessPrize(battle.BattleId);
    }

    /// <summary>
    /// Get winner user index from battle users
    /// </summary>
    private uint GetWinnerIndex(ulong battleid, Address[] users)
    {
        uint winningScore = 0;
        uint winningScoreIndex = 0;
        for (uint i = 0; i < users.Length; i++)
        {
            var user = GetUser(battleid, users[i]);
            if (user.Score > winningScore)
            {
                winningScore = user.Score;
                winningScoreIndex = i;
            }
        }
        return winningScoreIndex;
    }

    /// <summary>
    /// Send 3/4 amount to winner and 1/4 amount to battle owner
    /// </summary>
    private void ProcessPrize(ulong battleid)
    {
        var battle = GetBattle(battleid);
        ulong prize = battle.Fee * (MaxUsers - 1);
        Transfer(battle.Winner, prize);
        Transfer(BattleOwner, battle.Fee);
    }
    private void SetUser(ulong battleid, Address address, BattleUser user)
    {
        PersistentState.SetStruct($"user:{battleid}:{address}", user);
    }
    private BattleUser GetUser(ulong battleid, Address address)
    {
        return PersistentState.GetStruct<BattleUser>($"user:{battleid}:{address}");
    }
    private void SetBattle(ulong battleid, BattleMain battle)
    {
        PersistentState.SetStruct($"battle:{battleid}", battle);
    }
    private BattleMain GetBattle(ulong battleid)
    {
        return PersistentState.GetStruct<BattleMain>($"battle:{battleid}");
    }
    private void SetUserIndex(ulong battleid, uint userindex)
    {
        PersistentState.SetUInt32($"user:{battleid}", userindex);
    }
    private uint GetUserIndex(ulong battleid)
    {
        return PersistentState.GetUInt32($"user:{battleid}");
    }
    private void SetScoreSubmittedCount(ulong battleid, uint scoresubmitcount)
    {
        PersistentState.SetUInt32($"scoresubmit:{battleid}", scoresubmitcount);
    }
    private uint GetScoreSubmittedCount(ulong battleid)
    {
        return PersistentState.GetUInt32($"scoresubmit:{battleid}");
    }

    private const uint MaxUsers = 4;
    public struct BattleMain
    {
        public ulong BattleId;
        public Address Winner;
        public Address[] Users;
        public ulong Fee;
    }
    public struct BattleUser
    {
        public Address Address;
        public uint Score;
        public bool ScoreSubmitted;
    }
    public struct ArenaStateKeys
    {
        public const string NextBattleId = "AA";
    }
    public struct ClaimPendingDeployerOwnershipLog
    {
        [Index] public Address From;
        [Index] public Address To;
    }
    public struct SetPendingDeployerOwnershipLog
    {
        [Index] public Address From;
        [Index] public Address To;
    }
    public struct BattleEventLog
    {
        [Index] public string Event;
        [Index] public ulong BattleId;
        [Index] public Address Address;
    }
}