using System;
using UnityEngine;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>Persists guest tokens in PlayerPrefs (dev convenience; use secure storage in production).</summary>
    public static class PlayerSessionPrefs
    {
        private const string PlayerIdKey = "zg.playerId";
        private const string AccessTokenKey = "zg.accessToken";
        private const string RefreshTokenKey = "zg.refreshToken";
        private const string MatchIdKey = "zg.matchId";
        private const string SessionTokenKey = "zg.sessionToken";

        public static void Save(ZombieGameSession session)
        {
            if (session.PlayerId is Guid playerId)
                PlayerPrefs.SetString(PlayerIdKey, playerId.ToString());
            if (!string.IsNullOrEmpty(session.AccessToken))
                PlayerPrefs.SetString(AccessTokenKey, session.AccessToken);
            if (!string.IsNullOrEmpty(session.RefreshToken))
                PlayerPrefs.SetString(RefreshTokenKey, session.RefreshToken);
            if (session.MatchId is Guid matchId)
                PlayerPrefs.SetString(MatchIdKey, matchId.ToString());
            if (!string.IsNullOrEmpty(session.SessionToken))
                PlayerPrefs.SetString(SessionTokenKey, session.SessionToken);
            PlayerPrefs.Save();
        }

        public static bool TryRestoreTokens(ZombieGameSession session)
        {
            var access = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
            var refresh = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
                return false;

            session.RestoreSession(access, refresh);
            return true;
        }

        public static (Guid MatchId, string SessionToken)? TryRestoreMatch()
        {
            var matchText = PlayerPrefs.GetString(MatchIdKey, string.Empty);
            var sessionToken = PlayerPrefs.GetString(SessionTokenKey, string.Empty);
            if (!Guid.TryParse(matchText, out var matchId) || string.IsNullOrEmpty(sessionToken))
                return null;
            return (matchId, sessionToken);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(MatchIdKey);
            PlayerPrefs.DeleteKey(SessionTokenKey);
            PlayerPrefs.Save();
        }
    }
}
