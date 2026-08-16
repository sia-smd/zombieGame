namespace ZombieGame.Application.Room.Bots;

using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public static class RoomBotScheduler
{
    public static void ClearBotSchedules(RoomState room)
    {
        foreach (var player in room.Players)
            player.BotNextActionAt = null;
    }

    public static void ScheduleOpponentSelectionBots(RoomState room, RoomSettings settings)
    {
        foreach (var bot in room.AlivePlayers.Where(p => p.IsBot && !room.IsPaired(p.UserId)))
            bot.BotNextActionAt ??= RandomUtc(settings.BotInviteMinDelaySeconds, settings.BotInviteMaxDelaySeconds);
    }

    public static void ScheduleInvitationAccept(BattleInvitation invitation, RoomSettings settings)
    {
        invitation.BotRespondAt = RandomUtc(settings.BotAcceptMinDelaySeconds, settings.BotAcceptMaxDelaySeconds);
    }

    public static void ScheduleCardBattleBots(RoomState room, RoomSettings settings)
    {
        foreach (var pair in room.BattlePairs.Where(p => p.Status == BattlePairStatus.InProgress))
        {
            foreach (var playerId in new[] { pair.Player1Id, pair.Player2Id })
            {
                var player = room.GetPlayer(playerId);
                if (player is null || !player.IsBot || IsBotFinishedInPair(pair, playerId))
                    continue;

                player.BotNextActionAt ??= RandomUtc(settings.BotBattleMinDelaySeconds, settings.BotBattleMaxDelaySeconds);
            }
        }
    }

    /// <summary>Re-arms a bot that still has an action point left in its current battle turn.</summary>
    public static void ScheduleNextBattleAction(RoomState room, Guid userId, RoomSettings settings)
    {
        var player = room.GetPlayer(userId);
        if (player is null || !player.IsBot)
            return;

        player.BotNextActionAt = RandomUtc(settings.BotBattleMinDelaySeconds, settings.BotBattleMaxDelaySeconds);
    }

    public static void SchedulePhaseBots(RoomState room, RoomSettings settings)
    {
        foreach (var bot in room.AlivePlayers.Where(p => p.IsBot))
            bot.BotNextActionAt ??= RandomUtc(settings.BotReadyMinDelaySeconds, settings.BotReadyMaxDelaySeconds);
    }

    public static void ScheduleDiscussionBots(RoomState room, RoomSettings settings) =>
        ScheduleTimedBots(room, settings.BotChatMinDelaySeconds, settings.BotChatMaxDelaySeconds);

    public static void ScheduleVotingBots(RoomState room, RoomSettings settings) =>
        ScheduleTimedBots(room, settings.BotVoteMinDelaySeconds, settings.BotVoteMaxDelaySeconds);

    private static void ScheduleTimedBots(RoomState room, int minSeconds, int maxSeconds)
    {
        foreach (var bot in room.AlivePlayers.Where(p => p.IsBot))
            bot.BotNextActionAt ??= RandomUtc(minSeconds, maxSeconds);
    }

    public static bool IsBotFinishedInPair(BattlePair pair, Guid botUserId)
    {
        if (pair.Player1Id == botUserId)
            return pair.BattleSession.Player1Finished;

        if (pair.Player2Id == botUserId)
            return pair.BattleSession.Player2Finished;

        return true;
    }

    private static DateTime RandomUtc(int minSeconds, int maxSeconds)
    {
        var min = Math.Max(0, minSeconds);
        var max = Math.Max(min, maxSeconds);
        return DateTime.UtcNow.AddSeconds(Random.Shared.Next(min, max + 1));
    }
}
