using System.Collections.Generic;
using UnityEngine;

namespace EggTest.Shared
{
    public interface IMessage
    {
        double SentTime { get; }
    }

    public abstract class MessageBase : IMessage
    {
        public double SentTime { get; set; }
    }

    public sealed class PlayerInputMessage : MessageBase
    {
        public PlayerId PlayerId;
        public int Sequence;
        public Vector2 Direction;
    }

    public sealed class MatchStartedMessage : MessageBase
    {
        public float DurationSeconds;
        public List<PlayerProfile> Players = new List<PlayerProfile>();
    }

    public sealed class EggSpawnedMessage : MessageBase
    {
        public EggSnapshot Egg;
    }

    public sealed class EggCollectedMessage : MessageBase
    {
        public EggId EggId;
        public PlayerId CollectorId;
        public int NewScore;
    }

    public sealed class WorldSnapshotMessage : MessageBase
    {
        public int SnapshotSequence;
        public double ServerTime;
        public float RemainingTime;
        public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
        public List<EggSnapshot> Eggs = new List<EggSnapshot>();
    }

    public sealed class MatchEndedMessage : MessageBase
    {
        public List<ScoreEntry> FinalScores = new List<ScoreEntry>();
        public List<PlayerId> WinnerIds = new List<PlayerId>();
    }
}
