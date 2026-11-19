using System;
using Mejnun.Audio;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class GameAudioMapper
    {
        private const int MaxQueuedDealSounds = 12;
        private const float DealSoundSpacing = 0.06f;
        private const float TimerWarningSeconds = 10f;
        private const float TimerTickSeconds = 9f;

        private readonly IAudioService audio;
        private GameEvents events;
        private Func<int, bool> isHumanSeat;
        private int humanCount;
        private int queuedDealSounds;
        private float dealSoundCooldown;
        private bool cardMovedPending;
        private bool meldValidPending;
        private bool meldInvalidPending;
        private bool goDownPending;
        private bool hasTimerSample;
        private float lastTimerRemaining;
        private int lastTickSecond = int.MaxValue;
        private bool warningPosted;
        private bool timeoutPosted;
        private bool turnSoundsFromState;

        public GameAudioMapper(IAudioService audio)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        }

        public void Bind(MejnunGame game, Func<int, bool> humanSeatPredicate = null, int numberOfHumans = 0,
            bool useStateDrivenTurnSounds = false)
        {
            var nextEvents = game?.Events;
            if (events == nextEvents)
            {
                isHumanSeat = humanSeatPredicate ?? isHumanSeat;
                turnSoundsFromState = useStateDrivenTurnSounds;
                if (numberOfHumans > 0)
                {
                    humanCount = numberOfHumans;
                }

                return;
            }

            Unbind();
            if (nextEvents == null)
            {
                return;
            }

            events = nextEvents;
            isHumanSeat = humanSeatPredicate ?? (_ => true);
            humanCount = numberOfHumans > 0 ? numberOfHumans : game.Setup.SeatCount;
            turnSoundsFromState = useStateDrivenTurnSounds;

            events.DealerChosen += OnDealerChosen;
            events.DeckFacesAssigned += OnDeckFacesAssigned;
            events.CardDealt += OnCardDealt;
            events.TurnStarted += OnTurnStarted;
            events.CardMoved += OnCardMoved;
            events.MeldBecameValid += OnMeldBecameValid;
            events.MeldBecameInvalid += OnMeldBecameInvalid;
            events.WentDown += OnWentDown;
            events.DownRevealed += OnDownRevealed;
            events.Undone += OnUndone;
            events.BoardReverted += OnBoardReverted;
            events.Drew += OnDrew;
            events.Passed += OnPassed;
            events.TurnEnded += OnTurnEnded;
            events.Won += OnWon;
            events.Stalemate += OnStalemate;

            queuedDealSounds = 0;
            dealSoundCooldown = 0f;
            ResetTimer();
        }

        public void Unbind()
        {
            if (events != null)
            {
                events.DealerChosen -= OnDealerChosen;
                events.DeckFacesAssigned -= OnDeckFacesAssigned;
                events.CardDealt -= OnCardDealt;
                events.TurnStarted -= OnTurnStarted;
                events.CardMoved -= OnCardMoved;
                events.MeldBecameValid -= OnMeldBecameValid;
                events.MeldBecameInvalid -= OnMeldBecameInvalid;
                events.WentDown -= OnWentDown;
                events.DownRevealed -= OnDownRevealed;
                events.Undone -= OnUndone;
                events.BoardReverted -= OnBoardReverted;
                events.Drew -= OnDrew;
                events.Passed -= OnPassed;
                events.TurnEnded -= OnTurnEnded;
                events.Won -= OnWon;
                events.Stalemate -= OnStalemate;
            }

            events = null;
            turnSoundsFromState = false;
            queuedDealSounds = 0;
            cardMovedPending = false;
            meldValidPending = false;
            meldInvalidPending = false;
            goDownPending = false;
        }

        public void AdvanceFrame(float deltaTime)
        {
            if (events == null)
            {
                return;
            }

            if (cardMovedPending)
            {
                audio.Post(AudioIds.PlayCardDrop);
                cardMovedPending = false;
            }

            if (meldValidPending)
            {
                audio.Post(AudioIds.PlayMeldValid);
                meldValidPending = false;
            }

            if (meldInvalidPending)
            {
                audio.Post(AudioIds.PlayMeldInvalid);
                meldInvalidPending = false;
            }

            if (goDownPending)
            {
                audio.Post(AudioIds.PlayGoDown);
                goDownPending = false;
            }

            if (queuedDealSounds == 0)
            {
                return;
            }

            dealSoundCooldown -= Math.Max(0f, deltaTime);
            if (dealSoundCooldown <= 0f)
            {
                audio.Post(AudioIds.PlayCardDeal);
                queuedDealSounds--;
                dealSoundCooldown = DealSoundSpacing;
            }
        }

        public void ReportTurnTimer(float remainingSeconds, float totalSeconds)
        {
            if (totalSeconds <= 0f)
            {
                audio.SetRtpc(AudioIds.TimerTension, 0f);
                ResetTimer();
                return;
            }

            var remaining = Math.Max(0f, Math.Min(remainingSeconds, totalSeconds));
            if (hasTimerSample && remaining > lastTimerRemaining + 0.01f)
            {
                lastTickSecond = int.MaxValue;
                warningPosted = false;
                timeoutPosted = false;
            }

            hasTimerSample = true;
            lastTimerRemaining = remaining;
            audio.SetRtpc(AudioIds.TimerTension, 1f - remaining / totalSeconds);

            if (!warningPosted && remaining <= TimerWarningSeconds && remaining > 0f)
            {
                audio.Post(AudioIds.PlayTimerWarning);
                warningPosted = true;
            }

            var second = (int)Math.Ceiling(remaining);
            if (remaining > 0f && remaining <= TimerTickSeconds && second != lastTickSecond)
            {
                audio.Post(AudioIds.PlayTimerTick);
                lastTickSecond = second;
            }
        }

        public void ReportTimeout()
        {
            if (timeoutPosted)
            {
                return;
            }

            timeoutPosted = true;
            audio.SetRtpc(AudioIds.TimerTension, 1f);
            audio.Post(AudioIds.PlayTimerHalas);
        }

        public void ReportTurnStarted(int seat)
        {
            audio.Post(isHumanSeat(seat) ? AudioIds.PlayTurnStartPlayer : AudioIds.PlayTurnStartBot);
        }

        public void ReportDropCue()
        {
            cardMovedPending = false;
            audio.Post(AudioIds.PlayCardDrop);
        }

        private void OnDealerChosen(int seat)
        {
            audio.Post(AudioIds.PlayDealerChosen);
        }

        private void OnDeckFacesAssigned(string deckZero, string deckOne)
        {
            audio.Post(AudioIds.PlayCardShuffle);
        }

        private void OnCardDealt(int seat, Card card)
        {
            if (queuedDealSounds < MaxQueuedDealSounds)
            {
                queuedDealSounds++;
            }
        }

        private void OnTurnStarted(int seat, int turnNumber)
        {
            if (!turnSoundsFromState)
            {
                ReportTurnStarted(seat);
            }
        }

        private void OnCardMoved(Card card, CardTarget from, CardTarget to)
        {
            cardMovedPending = true;
        }

        private void OnMeldBecameValid(int meldId, MeldKind kind)
        {
            meldValidPending = true;
        }

        private void OnMeldBecameInvalid(int meldId, InvalidReason reason)
        {
            meldInvalidPending = true;
        }

        private void OnWentDown(int seat)
        {
            goDownPending = true;
        }

        private void OnDownRevealed(int seat, System.Collections.Generic.IReadOnlyList<int> meldIds)
        {
            goDownPending = true;
        }

        private void OnUndone(int seat)
        {
            audio.Post(AudioIds.PlayUndo);
        }

        private void OnBoardReverted(int seat)
        {
            audio.Post(AudioIds.PlayBoardRevert);
        }

        private void OnDrew(int seat, Card card)
        {
            audio.Post(AudioIds.PlayCardDraw);
        }

        private void OnPassed(int seat)
        {
            audio.Post(AudioIds.PlayPass);
        }

        private void OnTurnEnded(int seat, int nextSeat)
        {
            audio.Post(AudioIds.PlayTurnEnd);
        }

        private void OnWon(GameResult result)
        {
            var winnerIsHuman = result.Winner.HasValue && isHumanSeat(result.Winner.Value);
            if (!winnerIsHuman && humanCount == 1)
            {
                audio.Post(AudioIds.PlayLoseStinger);
                return;
            }

            if (result.Kind == GameEndKind.EmptyHand && winnerIsHuman)
            {
                audio.SetSwitch(AudioIds.ShoutType, AudioIds.ShoutDefault);
                audio.Post(AudioIds.PlayMejnunShout);
            }

            audio.Post(AudioIds.PlayWinStinger);
        }

        private void OnStalemate(GameResult result)
        {
            audio.Post(AudioIds.PlayStalemateStinger);
        }

        private void ResetTimer()
        {
            hasTimerSample = false;
            lastTimerRemaining = 0f;
            lastTickSecond = int.MaxValue;
            warningPosted = false;
            timeoutPosted = false;
        }
    }
}