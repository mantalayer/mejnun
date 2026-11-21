using System;
using UnityEngine;

namespace Mejnun.Net.Unity
{
    public sealed class OnlineReconnectController : MonoBehaviour
    {
        private const float HostRecoveryTimeoutSeconds = 180;
        private const float HostRecoveryRefreshIntervalSeconds = 5;
        private const float DisconnectedRecoveryDelaySeconds = 10;
        private const float AutomaticReconnectRetrySeconds = 10;
        private const float MissingWelcomeRetrySeconds = 10;
        private const float ForcedRejoinTimeoutSeconds = 45;
        private const float MembershipRecoveryRetrySeconds = 20;

        private OnlineMatchBridge bridge;
        private bool applicationWasPaused;
        private bool reconnectInProgress;
        private float hostRecoveryElapsed;
        private float disconnectedElapsed;
        private float nextAutomaticReconnectAt;
        private float welcomeElapsed;
        private float nextWelcomeRetryAt;
        private float nextMembershipRecoveryAt;
        private float nextHostRecoveryRefreshAt;

        private void Awake()
        {
            bridge = GetComponent<OnlineMatchBridge>();
        }

        private void Update()
        {
            if (bridge == null || !bridge.HasOnlineMatch)
            {
                hostRecoveryElapsed = 0;
                disconnectedElapsed = 0;
                nextAutomaticReconnectAt = 0;
                welcomeElapsed = 0;
                nextWelcomeRetryAt = 0;
                nextMembershipRecoveryAt = 0;
                nextHostRecoveryRefreshAt = 0;
                return;
            }

            if (bridge.IsRestoringHost || bridge.HostRecoveryInProgress && bridge.IsCurrentSessionHost)
            {
                disconnectedElapsed = 0;
                nextAutomaticReconnectAt = 0;
                welcomeElapsed = 0;
                nextWelcomeRetryAt = 0;
                return;
            }

            if (bridge.MembershipLost)
            {
                if (!reconnectInProgress && Time.unscaledTime >= nextMembershipRecoveryAt)
                {
                    nextMembershipRecoveryAt = Time.unscaledTime + MembershipRecoveryRetrySeconds;
                    ResumeAsync();
                }

                welcomeElapsed = 0;
                nextWelcomeRetryAt = 0;
                disconnectedElapsed = 0;
                return;
            }

            if (bridge.HostRecoveryInProgress)
            {
                hostRecoveryElapsed += Time.unscaledDeltaTime;
                if (hostRecoveryElapsed >= HostRecoveryTimeoutSeconds)
                {
                    bridge.FailHostRecovery();
                    hostRecoveryElapsed = 0;
                }
            }
            else
            {
                hostRecoveryElapsed = 0;
            }

            if (bridge.HostRecoveryInProgress && !bridge.IsCurrentSessionHost)
            {
                if (Time.unscaledTime >= nextHostRecoveryRefreshAt)
                {
                    nextHostRecoveryRefreshAt = Time.unscaledTime + HostRecoveryRefreshIntervalSeconds;
                    bridge.RefreshSessionAsync();
                }

                disconnectedElapsed = 0;
                nextAutomaticReconnectAt = 0;
                welcomeElapsed = 0;
                nextWelcomeRetryAt = 0;
                nextMembershipRecoveryAt = 0;
                return;
            }

            nextHostRecoveryRefreshAt = 0;

            nextMembershipRecoveryAt = 0;

            if (bridge.IsAwaitingWelcome)
            {
                welcomeElapsed += Time.unscaledDeltaTime;
                if (welcomeElapsed >= ForcedRejoinTimeoutSeconds && !reconnectInProgress)
                {
                    welcomeElapsed = 0;
                    nextWelcomeRetryAt = 0;
                    ResumeAsync();
                }
                else if (welcomeElapsed >= nextWelcomeRetryAt && !reconnectInProgress)
                {
                    nextWelcomeRetryAt = welcomeElapsed + MissingWelcomeRetrySeconds;
                    bridge.RejoinActiveClientSeat();
                }
            }
            else
            {
                welcomeElapsed = 0;
                nextWelcomeRetryAt = 0;
            }

            if (bridge.IsNetworkConnected)
            {
                disconnectedElapsed = 0;
                nextAutomaticReconnectAt = 0;
                return;
            }

            disconnectedElapsed += Time.unscaledDeltaTime;
            if (disconnectedElapsed >= DisconnectedRecoveryDelaySeconds && !reconnectInProgress &&
                disconnectedElapsed >= nextAutomaticReconnectAt)
            {
                nextAutomaticReconnectAt = disconnectedElapsed + AutomaticReconnectRetrySeconds;
                ResumeAsync();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (bridge == null || !bridge.HasOnlineMatch)
            {
                return;
            }

            if (paused)
            {
                applicationWasPaused = true;
                bridge.NotifyApplicationPaused();
                return;
            }

            if (!applicationWasPaused)
            {
                return;
            }

            applicationWasPaused = false;
            if (bridge.IsRestoringHost || bridge.HostRecoveryInProgress && bridge.IsCurrentSessionHost)
            {
                bridge.RefreshSessionAsync();
                return;
            }

            ResumeAsync();
        }

        private async void ResumeAsync()
        {
            if (reconnectInProgress || bridge == null || !bridge.HasOnlineMatch)
            {
                return;
            }

            var awaitingHostMigration = bridge.HostRecoveryInProgress && !bridge.IsCurrentSessionHost &&
                                        !bridge.MembershipLost;
            var forceClientRejoin = !awaitingHostMigration &&
                                    (bridge.MembershipLost || bridge.IsAwaitingWelcome);
            var handOverHost = bridge.IsCurrentSessionHost && !bridge.IsHostNetworkOperational &&
                               bridge.HasOtherSessionMembers;
            if (bridge.IsNetworkConnected && !forceClientRejoin && !handOverHost)
            {
                if (bridge.IsCurrentSessionHost && bridge.IsHostNetworkOperational)
                {
                    bridge.ResumeHostAway();
                    return;
                }

                if (!bridge.IsCurrentSessionHost && !bridge.IsTransportHost)
                {
                    bridge.RejoinActiveClientSeat();
                    return;
                }

                if (bridge.ConfigureAfterReconnect())
                {
                    return;
                }
            }

            var service = bridge.OnlineService;
            if (service == null)
            {
                return;
            }

            reconnectInProgress = true;
            var rejoinAsClient = !bridge.IsCurrentSessionHost || handOverHost;
            if (rejoinAsClient)
            {
                bridge.BeginRejoin();
            }

            var sessionId = bridge.SessionId;
            var sessionCode = bridge.SessionCode;
            try
            {
                if (handOverHost)
                {
                    await bridge.FlushMigrationRecordAsync();
                }

                bridge.PrepareForReconnect(handOverHost);
                global::Unity.Services.Multiplayer.ISession session = null;
                if (handOverHost && !string.IsNullOrWhiteSpace(sessionCode))
                {
                    session = await service.RejoinByCodeAsync(sessionCode);
                }
                else if (awaitingHostMigration && !string.IsNullOrWhiteSpace(sessionId))
                {
                    session = await service.ReconnectAsync(sessionId);
                }
                else if (rejoinAsClient && !string.IsNullOrWhiteSpace(sessionCode))
                {
                    session = await service.RejoinByCodeAsync(sessionCode);
                }
                else if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    try
                    {
                        session = await service.ReconnectAsync(sessionId);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Reconnect to the previous online session failed: " + exception.Message);
                    }
                }

                if (session == null && !awaitingHostMigration && !string.IsNullOrWhiteSpace(sessionCode))
                {
                    session = await service.RejoinByCodeAsync(sessionCode);
                }

                if (session == null || !bridge.ConfigureAfterReconnect())
                {
                    Debug.LogWarning("Online session reconnect completed without a usable session role.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Online session reconnect failed: " + exception.Message);
            }
            finally
            {
                if (rejoinAsClient)
                {
                    bridge.EndRejoin();
                }

                welcomeElapsed = 0;
                nextMembershipRecoveryAt = bridge.MembershipLost
                    ? Time.unscaledTime + MembershipRecoveryRetrySeconds
                    : 0;
                reconnectInProgress = false;
            }
        }
    }
}