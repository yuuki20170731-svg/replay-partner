using System.Collections.Generic;
using UnityEngine;

namespace ReplayPartner
{
    public enum ReplayCommand { Up, Down, Left, Right, Wait }
    public enum ReplayMode { Playing, Recording, Cleared, Failed }
    public sealed class ReplayActor
    {
        public Vector2 Position;
        public bool Blocked;
        public char BlockedCell;
        public int InvulnerableUntil;
        public ReplayActor(Vector2 position) { Position = position; }
    }
    // Fixed 60 Hz input replay; rendering never determines collisions.
    public sealed class ReplaySimulation
    {
        public const float StepSeconds = 1f / 60f;
        public const float WalkSpeed = 5f;
        public const float ActorRadius = 0.22f;
        public const int RecordingLimit = 1800;
        public readonly ReplayStage Stage;
        public readonly Vector2Int Start;
        private readonly List<List<Vector2>> recordings = new List<List<Vector2>>();
        private readonly List<List<Vector2>> paths = new List<List<Vector2>>();
        private readonly List<Vector2> currentPath = new List<Vector2>();
        public IReadOnlyList<Vector2> RecordedPath(int index) => paths[index];
        private readonly List<Vector2> current = new List<Vector2>();
        private readonly List<ReplayActor> clones = new List<ReplayActor>();
        private readonly List<Vector2Int> crates = new List<Vector2Int>();
        private readonly HashSet<char> open = new HashSet<char>();
        private readonly List<Vector2Int> switchesA, switchesB, doorsA, doorsB;
        private ReplayActor player;
        private int invulnerableUntil;
        public ReplayMode Mode { get; private set; }
        public int Tick { get; private set; }
        public int Health { get; private set; }
        public int TrapPhase => Tick % 180;
        public bool TrapsActive => TrapPhase < 60;
        public bool TrapsWarning => TrapPhase >= 150;
        public string Feedback { get; private set; }
        public Vector2 Player => player.Position;
        public IReadOnlyList<ReplayActor> Clones => clones;
        public IReadOnlyList<Vector2Int> Crates => crates;
        public int RecordingsCount => recordings.Count;
        public int Remaining => RecordingLimit - current.Count;
        public bool OpenA => open.Contains('a');
        public bool OpenB => open.Contains('b');
        public bool HasKey { get; private set; }
        public bool NeedsKey { get; }
        public bool HasTraps { get; }
        public float TrapCountdown => (TrapsActive ? 60 - TrapPhase : 180 - TrapPhase) * StepSeconds;
        public int RecordingLength(int index) => recordings[index].Count;
        public ReplaySimulation(ReplayStage stage)
        {
            Stage = stage; NeedsKey = Find('K').Count > 0;
            HasTraps = Find('T').Count > 0;
            var starts = Find('P'); Start = starts.Count == 0 ? new Vector2Int(1, 1) : starts[0];
            switchesA = Find('A'); switchesB = Find('B'); doorsA = Find('a'); doorsB = Find('b');
            ResetRoom();
        }
        public char Cell(Vector2Int p)
        {
            if (p.y < 0 || p.y >= Stage.Map.Length || p.x < 0 || p.x >= Stage.Map[p.y].Length) return '#';
            return Stage.Map[p.y][p.x];
        }
        public static Vector2Int TileAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x + 0.5f), Mathf.FloorToInt(p.y + 0.5f));
        public bool IsOpen(char door) => open.Contains(door);
        public bool IsCloneDone(int index) => Tick >= recordings[index].Count;
        private List<Vector2Int> Find(char symbol)
        {
            var found = new List<Vector2Int>();
            for (int y = 0; y < Stage.Map.Length; y++)
                for (int x = 0; x < Stage.Map[y].Length; x++)
                    if (Stage.Map[y][x] == symbol) found.Add(new Vector2Int(x, y));
            return found;
        }
        private static bool Near(Vector2 a, Vector2 b, float r) => (a - b).sqrMagnitude <= r * r;
        public bool Occupied(Vector2Int p)
        {
            if (Near(player.Position, p, 0.44f)) return true;
            foreach (var clone in clones) if (Near(clone.Position, p, 0.44f)) return true;
            return crates.Contains(p);
        }
        private bool Held(List<Vector2Int> points)
        {
            foreach (var p in points)
            {
                if (Overlaps(player.Position, p)) return true;
                foreach (var clone in clones) if (Overlaps(clone.Position, p)) return true;
                if (crates.Contains(p)) return true;
            }
            return false;
        }
        private void UpdateDoors()
        {
            bool a = Held(doorsA), b = Held(doorsB);
            foreach (var p in switchesA) a |= Occupied(p);
            foreach (var p in switchesB) b |= Occupied(p);
            if (a) open.Add('a'); else open.Remove('a');
            if (b) open.Add('b'); else open.Remove('b');
        }
        private void ResetRoom()
        {
            Tick = 0; Health = 3; HasKey = false; invulnerableUntil = 0;
            player = new ReplayActor(Start); clones.Clear();
            foreach (var unused in recordings) clones.Add(new ReplayActor(Start));
            crates.Clear(); crates.AddRange(Find('C'));
            open.Clear(); UpdateDoors(); Mode = ReplayMode.Playing; Feedback = "部屋を巻き戻しました";
        }
        private bool Solid(Vector2Int p)
        {
            char c = Cell(p);
            return c == '#' || ((c == 'a' || c == 'b') && !open.Contains(c)) || (c == 'E' && NeedsKey && !HasKey);
        }
        private static bool Overlaps(Vector2 p, Vector2Int tile) =>
            Mathf.Abs(p.x - tile.x) < 0.5f + ActorRadius - 0.001f && Mathf.Abs(p.y - tile.y) < 0.5f + ActorRadius - 0.001f;
        private bool CanStand(Vector2 target, ReplayActor actor, Vector2 direction)
        {
            var low = TileAt(target - Vector2.one * ActorRadius);
            var high = TileAt(target + Vector2.one * ActorRadius);
            for (int y = low.y; y <= high.y; y++)
                for (int x = low.x; x <= high.x; x++)
                {
                    var tile = new Vector2Int(x, y);
                    if (Solid(tile) && Overlaps(target, tile)) { actor.BlockedCell = Cell(tile); return false; }
                }
            for (int i = 0; i < crates.Count; i++)
            {
                if (!Overlaps(target, crates[i])) continue;
                var delta = new Vector2Int(direction.x == 0 ? 0 : (direction.x > 0 ? 1 : -1), direction.y == 0 ? 0 : (direction.y > 0 ? 1 : -1));
                var beyond = crates[i] + delta;
                bool aligned = delta.x != 0 ? Mathf.Abs(actor.Position.y - crates[i].y) < 0.27f : Mathf.Abs(actor.Position.x - crates[i].x) < 0.27f;
                if (!aligned || Solid(beyond) || crates.Contains(beyond) || Near(player.Position, beyond, 0.7f)) return false;
                foreach (var clone in clones) if (Near(clone.Position, beyond, 0.7f)) return false;
                crates[i] = beyond;
            }
            return true;
        }
        private void Move(ReplayActor actor, Vector2 input)
        {
            actor.Blocked = false; actor.BlockedCell = '\0';
            Vector2 amount = Vector2.ClampMagnitude(input, 1f) * (WalkSpeed * StepSeconds);
            if (amount.x != 0)
            {
                Vector2 target = actor.Position + new Vector2(amount.x, 0);
                if (CanStand(target, actor, new Vector2(amount.x, 0))) actor.Position = target; else actor.Blocked = true;
            }
            if (amount.y != 0)
            {
                Vector2 target = actor.Position + new Vector2(0, amount.y);
                if (CanStand(target, actor, new Vector2(0, amount.y))) actor.Position = target; else actor.Blocked = true;
            }
        }
        public void Advance(Vector2 input)
        {
            if (Mode == ReplayMode.Cleared || Mode == ReplayMode.Failed) return;
            input = Vector2.ClampMagnitude(input, 1f); UpdateDoors();
            for (int i = 0; i < clones.Count; i++)
            {
                Move(clones[i], Tick < recordings[i].Count ? recordings[i][Tick] : Vector2.zero);
                if ((Tick + 1) % 180 < 60 && Tick + 1 >= clones[i].InvulnerableUntil && Cell(TileAt(clones[i].Position)) == 'T')
                {
                    clones[i].Position = Start;
                    clones[i].InvulnerableUntil = Tick + 61;
                }
                UpdateDoors();
            }
            Move(player, input);
            char tile = Cell(TileAt(player.Position));
            if (tile == 'K') HasKey = true;
            bool recording = Mode == ReplayMode.Recording;
            if (recording) current.Add(input);
            Tick++; UpdateDoors();
            if (Tick >= invulnerableUntil)
            {
                Feedback = player.Blocked ? (player.BlockedCell == 'E' ? "出口には鍵が必要です" : "ここは通れません") : "";
                if (tile == 'T' && TrapsActive)
                {
                    Health--; player.Position = Start; invulnerableUntil = Tick + 60;
                    Feedback = Health > 0 ? "床の罠に触れた！入口に戻りました。予告を見て進もう" : "力尽きました。記録を使って再挑戦できます";
                    if (Health <= 0) Mode = ReplayMode.Failed;
                    UpdateDoors();
                }
            }
            if (recording) currentPath.Add(player.Position);
            if (Mode == ReplayMode.Playing && tile == 'E' && (!NeedsKey || HasKey)) { Mode = ReplayMode.Cleared; Feedback = "ステージクリア"; }
            if (Mode == ReplayMode.Recording && current.Count >= RecordingLimit) Commit();
        }
        // Adapter for route tests: 12 fixed steps equal one tile at walking speed.
        public void Step(ReplayCommand command)
        {
            Vector2 input = command == ReplayCommand.Up ? Vector2.down : command == ReplayCommand.Down ? Vector2.up :
                command == ReplayCommand.Left ? Vector2.left : command == ReplayCommand.Right ? Vector2.right : Vector2.zero;
            for (int i = 0; i < 12; i++) Advance(input);
        }
        public bool Begin()
        {
            if (Mode != ReplayMode.Playing || recordings.Count >= 2) return false;
            current.Clear(); currentPath.Clear(); currentPath.Add(Start); ResetRoom(); Mode = ReplayMode.Recording; Feedback = "記録中 — 記録キーでもう一度確定"; return true;
        }
        public bool Commit()
        {
            if (Mode != ReplayMode.Recording || current.Count == 0) return false;
            recordings.Add(new List<Vector2>(current)); paths.Add(new List<Vector2>(currentPath)); current.Clear(); currentPath.Clear(); ResetRoom(); return true;
        }
        public void Cancel() { if (Mode == ReplayMode.Recording) { current.Clear(); currentPath.Clear(); ResetRoom(); } }
        public void Retry() { current.Clear(); currentPath.Clear(); ResetRoom(); }
        public void Undo() { if (Mode != ReplayMode.Recording && recordings.Count > 0) { paths.RemoveAt(paths.Count - 1); recordings.RemoveAt(recordings.Count - 1); ResetRoom(); } }
        public void Restart() { recordings.Clear(); paths.Clear(); current.Clear(); currentPath.Clear(); ResetRoom(); }
    }
}
