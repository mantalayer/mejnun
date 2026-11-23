using System.Collections.Generic;
using System.Text;
using Mejnun.Audio;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mejnun.Game
{
    public sealed class AudioDirector : MonoBehaviour
    {
        private readonly HashSet<VisualElement> hookedRoots = new HashSet<VisualElement>();
        private IAudioService audioService;
        private GameAudioMapper mapper;
        private MatchFlow matchFlow;
        private TurnTimer turnTimer;
        private bool mainBankLoaded;
        private bool timerSampleActive;
        private bool onlineTurnSoundKnown;
        private int lastOnlineSoundSeat = -1;
        private int lastOnlineSoundTurn = -1;
        private bool remoteMoveSnapshotKnown;
        private ulong lastRemoteMoveSeed;
        private int lastRemoteMoveTurn = -1;
        private int lastRemoteMoveSeat = -1;
        private MejnunGame lastRemoteMoveGame;
        private string lastRemoteMoveFingerprint;
        private bool audioInitializationWarningLogged;
        private bool volumesAppliedAfterInitialization;
        private float audioInitializationStartedAt;
        private float nextBankLoadAttemptTime;
        private string currentMusic;
        private const float AudioInitializationWarningDelaySeconds = 5f;
        private const float BankLoadLogIntervalSeconds = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForSceneLoads()
        {
            WwiseAudioService.EnsureInitialized();
            WwiseAudioService.EnsureDedicatedListener();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WwiseAudioService.EnsureInitialized();
            WwiseAudioService.EnsureDedicatedListener();
            var flow = FindAnyObjectByType<MatchFlow>();
            if (flow != null && FindAnyObjectByType<AudioDirector>() == null)
            {
                flow.gameObject.AddComponent<AudioDirector>();
            }

            var menuCue = FindAnyObjectByType<MenuAudioCue>();
            if (menuCue != null && FindAnyObjectByType<AudioDirector>() == null)
            {
                menuCue.gameObject.AddComponent<AudioDirector>();
            }
        }

        private void Awake()
        {
            WwiseAudioService.EnsureInitialized();
            WwiseAudioService.EnsureDedicatedListener();
            audioInitializationStartedAt = Time.unscaledTime;
            audioService = new WwiseAudioService(gameObject);
            mapper = new GameAudioMapper(audioService);
            if (FindAnyObjectByType<MenuAudioCue>() != null)
            {
                audioService.SetState(AudioIds.GamePhase, AudioIds.PhaseMenu);
                SetMusic(AudioIds.MusicMenu);
            }
        }

        private void OnEnable()
        {
            volumesAppliedAfterInitialization = false;
            GameSettings.Changed += OnSettingsChanged;
            ApplyVolumes();
            FeedbackCues.Raised += OnFeedbackCue;
            AttachMatchFlow(FindAnyObjectByType<MatchFlow>());
            HookUiDocuments();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            FeedbackCues.Raised -= OnFeedbackCue;
            DetachMatchFlow();
            UnhookUiDocuments();
            mapper?.Unbind();
            audioService?.Post(AudioIds.StopMusic);
            currentMusic = null;
        }

        private void Update()
        {
            var engineInitialized = audioService != null && audioService.IsInitialized;
            if (engineInitialized && !volumesAppliedAfterInitialization)
            {
                ApplyVolumes();
                volumesAppliedAfterInitialization = true;
            }

            if (!engineInitialized && !audioInitializationWarningLogged &&
                Time.unscaledTime - audioInitializationStartedAt >= AudioInitializationWarningDelaySeconds)
            {
                audioInitializationWarningLogged = true;
                Debug.LogError("Wwise sound engine is not initialized; check AkInitializer and platform initialization settings.", this);
            }

            if (!mainBankLoaded && engineInitialized && Time.unscaledTime >= nextBankLoadAttemptTime)
            {
                var result = audioService.LoadBankResult("Main");
                if (result == "AK_Success" || result == "AK_BankAlreadyLoaded")
                {
                    mainBankLoaded = true;
                    ApplyVolumes();
                    Debug.Log("Wwise Main sound bank loaded.", this);
                }
                else
                {
                    Debug.LogError("Wwise Main sound bank failed to load: " + result, this);
                }

                nextBankLoadAttemptTime = Time.unscaledTime + BankLoadLogIntervalSeconds;
            }

            SyncTurnTimer();
            if (turnTimer != null && turnTimer.IsRunning)
            {
                mapper.ReportTurnTimer(turnTimer.RemainingSeconds, turnTimer.DurationSeconds);
                timerSampleActive = true;
            }
            else if (timerSampleActive)
            {
                mapper.ReportTurnTimer(0f, 0f);
                timerSampleActive = false;
            }

            mapper.AdvanceFrame(Time.unscaledDeltaTime);
            HookUiDocuments();
        }


        public void ReportTurnTimer(float remainingSeconds, float totalSeconds)
        {
            mapper?.ReportTurnTimer(remainingSeconds, totalSeconds);
        }

        public void ReportTimeout()
        {
            mapper?.ReportTimeout();
        }

        public void ConfigureAudioService(IAudioService service)
        {
            audioService = service;
            ApplyVolumes();
        }

        private void OnSettingsChanged(string key)
        {
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            audioService?.SetRtpc(AudioIds.VolumeMaster, GameSettings.VolumeMaster * 100f);
            audioService?.SetRtpc(AudioIds.VolumeMusic, GameSettings.VolumeMusic * 100f);
            audioService?.SetRtpc(AudioIds.VolumeSfx, GameSettings.VolumeSfx * 100f);
            audioService?.SetRtpc(AudioIds.VolumeVoice, GameSettings.VolumeVoice * 100f);
        }

        private void AttachMatchFlow(MatchFlow flow)
        {
            if (matchFlow == flow)
            {
                if (matchFlow != null)
                {
                    BindGame(matchFlow.Game);
                    UpdateScreen(matchFlow.Screen);
                }

                return;
            }

            DetachMatchFlow();
            matchFlow = flow;
            if (matchFlow == null)
            {
                return;
            }

            SyncTurnTimer();
            matchFlow.ScreenChanged += OnScreenChanged;
            matchFlow.ReactionReceived += OnReactionReceived;
            if (matchFlow.Controller != null)
            {
                matchFlow.Controller.Synchronized += OnSynchronized;
            }

            BindGame(matchFlow.Game);
            UpdateScreen(matchFlow.Screen);
        }

        private void DetachMatchFlow()
        {
            if (turnTimer != null)
            {
                turnTimer.TimedOut -= OnTurnTimerTimedOut;
                turnTimer = null;
            }

            if (timerSampleActive)
            {
                mapper?.ReportTurnTimer(0f, 0f);
                timerSampleActive = false;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged -= OnScreenChanged;
                matchFlow.ReactionReceived -= OnReactionReceived;
                if (matchFlow.Controller != null)
                {
                    matchFlow.Controller.Synchronized -= OnSynchronized;
                }
            }

            matchFlow = null;
            onlineTurnSoundKnown = false;
            ResetRemoteMoveTracking();
        }

        private void SyncTurnTimer()
        {
            var currentTimer = matchFlow == null ? null : matchFlow.TurnTimer;
            if (turnTimer == currentTimer)
            {
                return;
            }

            if (turnTimer != null)
            {
                turnTimer.TimedOut -= OnTurnTimerTimedOut;
            }

            turnTimer = currentTimer;
            if (turnTimer != null)
            {
                turnTimer.TimedOut += OnTurnTimerTimedOut;
            }
        }

        private void OnTurnTimerTimedOut()
        {
            ReportTimeout();
        }

        private void OnReactionReceived(int fromSeat, Reaction reaction)
        {
            if (reaction == Reaction.HurryUp)
            {
                audioService?.Post(AudioIds.PlayTimerWarning);
            }
        }

        private void OnScreenChanged(MatchFlowScreen screen)
        {
            BindGame(matchFlow == null ? null : matchFlow.Game);
            UpdateScreen(screen);
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            if (matchFlow != null)
            {
                BindGame(matchFlow.Game);
            }

            if (matchFlow != null && matchFlow.IsOnline && matchFlow.Game != null &&
                state != null && state.Phase == GamePhase.Playing)
            {
                TrackRemoteCardMoves(matchFlow.Game, state);
            }
            else
            {
                ResetRemoteMoveTracking();
            }

            if (matchFlow != null && matchFlow.IsOnline && state != null && state.Phase == GamePhase.Playing)
            {
                if (!onlineTurnSoundKnown || lastOnlineSoundSeat != state.CurrentSeat ||
                    lastOnlineSoundTurn != state.TurnNumber)
                {
                    mapper.ReportTurnStarted(state.CurrentSeat);
                    lastOnlineSoundSeat = state.CurrentSeat;
                    lastOnlineSoundTurn = state.TurnNumber;
                    onlineTurnSoundKnown = true;
                }
            }
            else
            {
                onlineTurnSoundKnown = false;
            }

            audioService.SetRtpc(AudioIds.BoardComplexity, Mathf.Clamp01(state.Board.Count / 6f));
        }

        private void TrackRemoteCardMoves(MejnunGame game, GameState state)
        {
            if (state.CurrentSeat < 0 || state.CurrentSeat >= state.Hands.Count)
            {
                ResetRemoteMoveTracking();
                return;
            }

            var seed = game.Setup.Seed;
            var fingerprint = CreateBoardFingerprint(state);
            if (remoteMoveSnapshotKnown && lastRemoteMoveSeed == seed &&
                lastRemoteMoveTurn == state.TurnNumber && lastRemoteMoveSeat == state.CurrentSeat &&
                lastRemoteMoveGame != game && lastRemoteMoveFingerprint != fingerprint &&
                !matchFlow.IsLocalSeat(state.CurrentSeat))
            {
                mapper.ReportRemoteCardMoved();
            }

            remoteMoveSnapshotKnown = true;
            lastRemoteMoveSeed = seed;
            lastRemoteMoveTurn = state.TurnNumber;
            lastRemoteMoveSeat = state.CurrentSeat;
            lastRemoteMoveGame = game;
            lastRemoteMoveFingerprint = fingerprint;
        }

        private static string CreateBoardFingerprint(GameState state)
        {
            var fingerprint = new StringBuilder();
            fingerprint.Append(state.Board.Count).Append('|');
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                fingerprint.Append(meld.Id).Append(':');
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    fingerprint.Append(meld.Cards[cardIndex].Id).Append(',');
                }

                fingerprint.Append(';');
            }

            fingerprint.Append('|').Append(state.Hands.Count).Append(':');
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                fingerprint.Append(state.Hands[seat].Count).Append(',');
            }

            return fingerprint.ToString();
        }

        private void ResetRemoteMoveTracking()
        {
            remoteMoveSnapshotKnown = false;
            lastRemoteMoveTurn = -1;
            lastRemoteMoveSeat = -1;
            lastRemoteMoveGame = null;
            lastRemoteMoveFingerprint = null;
        }

        private void BindGame(MejnunGame game)
        {
            if (game == null)
            {
                mapper.Unbind();
                return;
            }

            var online = matchFlow != null && matchFlow.IsOnline;
            mapper.Bind(game, seat => online
                    ? matchFlow.IsLocalSeat(seat)
                    : matchFlow == null || !matchFlow.IsBotSeat(seat),
                online ? 1 : matchFlow == null ? game.Setup.SeatCount : matchFlow.HumanCount, online);
        }

        private void UpdateScreen(MatchFlowScreen screen)
        {
            switch (screen)
            {
                case MatchFlowScreen.Idle:
                    audioService.SetState(AudioIds.GamePhase, AudioIds.PhaseMenu);
                    SetMusic(AudioIds.MusicMenu);
                    break;
                case MatchFlowScreen.DealerQuestion:
                    audioService.SetState(AudioIds.GamePhase, AudioIds.PhaseDealing);
                    SetMusic(AudioIds.MusicGame);
                    break;
                case MatchFlowScreen.Results:
                    audioService.SetState(AudioIds.GamePhase, AudioIds.PhaseResults);
                    SetMusic(AudioIds.MusicGame);
                    break;
                default:
                    audioService.SetState(AudioIds.GamePhase, AudioIds.PhasePlaying);
                    SetMusic(AudioIds.MusicGame);
                    break;
            }
        }

        private void SetMusic(string eventName)
        {
            if (currentMusic == eventName)
            {
                return;
            }

            if (!string.IsNullOrEmpty(currentMusic))
            {
                audioService.Post(AudioIds.StopMusic);
            }

            audioService.Post(eventName);
            currentMusic = eventName;
        }

        private void OnFeedbackCue(FeedbackCue cue)
        {
            switch (cue)
            {
                case FeedbackCue.Pickup:
                    audioService.Post(AudioIds.PlayCardPickup);
                    break;
                case FeedbackCue.LongPress:
                    audioService.Post(AudioIds.PlayCardLongPress);
                    break;
                case FeedbackCue.Drop:
                    mapper.ReportDropCue();
                    break;
                case FeedbackCue.Invalid:
                    audioService.Post(AudioIds.PlayCardInvalid);
                    break;
                case FeedbackCue.Mark:
                    audioService.Post(AudioIds.PlayCardMark);
                    break;
                case FeedbackCue.MarkedSetBroken:
                    audioService.Post(AudioIds.PlayMeldInvalid);
                    break;
            }
        }

        private void HookUiDocuments()
        {
            var documents = FindObjectsByType<UIDocument>();
            for (var index = 0; index < documents.Length; index++)
            {
                var root = documents[index].rootVisualElement;
                if (root != null && hookedRoots.Add(root))
                {
                    root.RegisterCallback<ClickEvent>(OnUiClick, TrickleDown.TrickleDown);
                }
            }
        }

        private void UnhookUiDocuments()
        {
            foreach (var root in hookedRoots)
            {
                root.UnregisterCallback<ClickEvent>(OnUiClick, TrickleDown.TrickleDown);
            }

            hookedRoots.Clear();
        }

        private void OnUiClick(ClickEvent evt)
        {
            var target = evt.target as VisualElement;
            while (target != null && !(target is Button))
            {
                target = target.parent;
            }

            if (target != null)
            {
                audioService.Post(AudioIds.PlayUiClick);
            }
        }
    }
}