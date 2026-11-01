using System;

namespace CallerRetroBall.Logic
{
    /// <summary>I51–I60 game feel: the numbers, kept apart so the tests can pin them.</summary>
    public static class GameFeelPlus
    {
        /// <summary>I51: a defender this close (m) and closing at least this fast (m/s) is a close-out worth faking.</summary>
        public const float FakeCloseRange = 1.6f;
        public const float FakeClosingSpeed = 1.5f;
        /// <summary>I51: at most this many AI fakes a possession (they wise up anyway).</summary>
        public const int MaxAiFakes = 1;

        /// <summary>I51: chance an AI shooter fakes a hard close-out. Smarter AI uses it more.</summary>
        public static float FakeChance(float decisionQuality) => 0.15f + 0.45f * Clamp01(decisionQuality);

        /// <summary>I52: a heated-up teammate this open (m) gets the ball.</summary>
        public const float HotFeedOpenness = 1.2f;

        /// <summary>I54: an offensive board inside finish range goes straight back up within this long (s).</summary>
        public const float PutbackWindow = 0.8f;

        /// <summary>I55: smothered means a defender this close (m) for this long (s); then the AI calls a screen.</summary>
        public const float SmotherDistance = 0.9f;
        public const float SmotherSeconds = 1.6f;

        /// <summary>I56: below this stamina (0..1) a player stops attacking off the dribble as much.</summary>
        public const float TiredStamina = 0.4f;

        public static float TiredDrivePenalty(float stamina) => Math.Max(0f, TiredStamina - stamina) * 0.8f;

        /// <summary>I57: a catch this open (m) and this fresh (s) leans towards shooting it.</summary>
        public const float CatchShootOpenness = 1.8f;
        public const float CatchShootWindow = 0.6f;
        public const float CatchShootLean = 0.15f;

        public static float CatchAndShootLean(float sinceCatch, float openness) =>
            sinceCatch >= 0f && sinceCatch <= CatchShootWindow && openness >= CatchShootOpenness ? CatchShootLean : 0f;

        /// <summary>I53/I59: a team is at game point when one basket from the arc wins it.</summary>
        public static bool IsGamePoint(int score, int targetScore, int arcPoints) =>
            targetScore > 0 && targetScore < 99 && score < targetScore && targetScore - score <= Math.Max(1, arcPoints);

        /// <summary>I53: at game point the go-to scorer leans towards taking his shot.</summary>
        public const float GoToShotLean = 0.1f;
        /// <summary>I59: at game point his on-ball defender plays this much tighter (m), and off the ball leans this far (0..1) towards denying him.</summary>
        public const float GamePointTighten = 0.25f;
        public const float DenyPull = 0.45f;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    public sealed partial class MatchSimulation
    {
        private int _lastRebounder = -1;
        private bool _screenCalledThisTrip;
        private float _lastReboundAt = -99f;

        /// <summary>I51: AI pump fakes this game.</summary>
        public int AiFakes { get; private set; }
        /// <summary>I52/I53: passes made to a hot hand or to the go-to scorer at game point.</summary>
        public int ScorerFeeds { get; private set; }
        /// <summary>I54: putbacks this game.</summary>
        public int Putbacks { get; private set; }
        /// <summary>I55: screens called because the handler was smothered.</summary>
        public int ScreenCalls { get; private set; }

        // ------------------------------------------------------------------ I53 / I59 game point

        public bool IsGamePoint(int team)
        {
            if (team < 0 || team > 1) return false;
            return GameFeelPlus.IsGamePoint(Score[team], Setup.Rules.targetScore, Scoring.PointsFor(ShotZone.BeyondArc, Setup.Rules));
        }

        /// <summary>A team's go-to scorer: points this game, then shooting and clutch ratings break ties.</summary>
        public int GoToScorer(int team)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team != team || IsBenched(i)) continue;
                var a = Players[i].Def.attributes;
                float v = Stats[i].points + 0.05f * a.shooting + 0.03f * a.clutch;
                if (v > bestScore)
                {
                    bestScore = v;
                    best = i;
                }
            }
            return best;
        }

        private bool IsGoToAtGamePoint(PlayerRuntimeState p) => IsGamePoint(p.Team) && GoToScorer(p.Team) == p.Index;

        // ------------------------------------------------------------------ I52 / I53 feeding the scorer

        private void FeedTheScorer(PlayerRuntimeState p, ref int target, ref float uPass)
        {
            int pick = -1;
            for (int i = 0; i < Players.Length; i++)
            {
                if (i == p.Index || Players[i].Team != p.Team || IsBenched(i) || !IsHeatedUp(i)) continue;
                if (OpennessOf(Players[i]) >= GameFeelPlus.HotFeedOpenness) { pick = i; break; }
            }
            if (pick < 0 && IsGamePoint(p.Team))
            {
                int go = GoToScorer(p.Team);
                if (go >= 0 && go != p.Index && OpennessOf(Players[go]) >= GameFeelPlus.HotFeedOpenness) pick = go;
            }
            if (pick < 0) return;
            // A person on your team who's open still gets the ball first (the pass-back rule above).
            int human = HumanIndexOf(p.Team);
            if (human >= 0 && target == human && pick != human) return;
            if (target != pick) ScorerFeedPending = pick;
            target = pick;
            uPass = Math.Max(uPass, 0.45f);
        }

        /// <summary>Set when the pass choice was changed to feed the scorer; counted if that pass is thrown.</summary>
        private int ScorerFeedPending = -1;

        // ------------------------------------------------------------------ I51 pump fakes

        private bool TryAiPumpFake(PlayerRuntimeState p, AiState s, DifficultyDef profile, float dist)
        {
            if (dist <= GameFeel.FinishRange(Setup.Shot) || _pumpFakes >= GameFeelPlus.MaxAiFakes || Setup.PassiveOpponents) return false;
            int d = FakeTarget(p);
            if (d < 0) return false;
            var def = Players[d];
            var toShooter = p.Position - def.Position;
            float gap = toShooter.Magnitude;
            if (gap > GameFeelPlus.FakeCloseRange || gap < 0.01f) return false;
            float closing = Vec2.Dot(def.Motion.velocity, toShooter / gap);
            if (closing < GameFeelPlus.FakeClosingSpeed) return false;
            if (_rng.NextFloat() > GameFeelPlus.FakeChance(profile.decisionQuality)) return false;
            AiFakes++;
            PumpFake(p.Index);
            // Bit or not, read it again straight away: a bitten defender leaves him open to rise up or go by.
            s.NextDecision = Time + 0.15f;
            return true;
        }

        // ------------------------------------------------------------------ I54 putbacks

        private bool TryPutback(PlayerRuntimeState p, AiState s, DifficultyDef profile, float dist)
        {
            if (_lastRebounder != p.Index || Time - _lastReboundAt > GameFeelPlus.PutbackWindow) return false;
            if (dist > GameFeel.FinishRange(Setup.Shot) || !CanShoot(p.Index)) return false;
            _lastRebounder = -1;
            Putbacks++;
            s.Intent = AiIntent.Hold;
            s.Target = p.Position;
            BeginCharge(p.Index, SampleReleaseMeter(p, profile));
            return true;
        }

        // ------------------------------------------------------------------ I55 call for a screen

        private bool TryCallForScreen(PlayerRuntimeState p, AiState s, DifficultyDef profile, float dist)
        {
            if (NearestOpponentDistance(p) > GameFeelPlus.SmotherDistance)
            {
                s.SmotheredSince = -1f;
                return false;
            }
            if (s.SmotheredSince < 0f) s.SmotheredSince = Time;
            if (Time - s.SmotheredSince < GameFeelPlus.SmotherSeconds) return false;
            bool aiCalls = p.Team != Setup.HumanTeam || Setup.Demo || Coaching && PendingPlay == PlayCall.None;
            if (!aiCalls || _play != PlayCall.None || dist < 3f || ShotClock < 4f || _screenCalledThisTrip) return false;
            _screenCalledThisTrip = true; // once a trip: then he has to make something happen
            s.SmotheredSince = -1f;
            if (!StartPlay(p.Team, PlayCall.PickAndRoll, p.Index)) return false;
            ScreenCalls++;
            s.Intent = AiIntent.Hold;
            s.Target = p.Position;
            return true;
        }

        // ------------------------------------------------------------------ I57 catch and shoot

        private float CatchAndShootLean(PlayerRuntimeState p) =>
            _lastCatcher == p.Index ? GameFeelPlus.CatchAndShootLean(Time - _lastCatchTime, OpennessOf(p)) : 0f;

        /// <summary>An open AI catcher reads it the moment the ball lands instead of after his reaction time.</summary>
        private void ReadyToCatchAndShoot(int catcher)
        {
            if (catcher < 0 || IsHumanControlled(catcher) || Players[catcher].Team != OffenseTeam) return;
            if (ScorerFeedPending == catcher) ScorerFeeds++;
            ScorerFeedPending = -1;
            if (OpennessOf(Players[catcher]) >= GameFeelPlus.CatchShootOpenness)
                _ai[catcher].NextDecision = Math.Min(_ai[catcher].NextDecision, Time);
        }
    }
}
