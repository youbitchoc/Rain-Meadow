﻿using System;
using System.Linq;

namespace RainMeadow
{
    public static class RPCs
    {
        [RPCMethod]
        public static void DeltaReset(RPCEvent rpcEvent, OnlineResource onlineResource, OnlineEntity.EntityId entity)
        {
            RainMeadow.Debug($"from {rpcEvent.from} resource {onlineResource} entity {entity}");
            if (entity != null)
            {
                foreach (var feed in OnlineManager.feeds)
                {
                    if (feed.player == rpcEvent.from && feed.entity.id == entity && feed.resource == onlineResource)
                    {
                        feed.ResetDeltas();
                        return;
                    }
                }
            }
            else
            {
                foreach (var subscription in OnlineManager.subscriptions)
                {
                    if (subscription.player == rpcEvent.from && subscription.resource == onlineResource)
                    {
                        subscription.ResetDeltas();
                        return;
                    }
                }
            }
        }
        [RPCMethod]
        public static void UpdateUsernameTemporarily(string incomingUsername, string lastSentMessage)
        {
            RainMeadow.Debug(incomingUsername);
            foreach (var playerAvatar in OnlineManager.lobby.playerAvatars.Select(kv => kv.Value))
            {
                if (OnlineManager.lobby.gameMode.usersIDontWantToChatWith.Contains(incomingUsername))
                {
                    continue;
                }
                if (playerAvatar.type == (byte)OnlineEntity.EntityId.IdType.none) continue; // not in game

                if (playerAvatar.FindEntity(true) is OnlinePhysicalObject opo && opo.owner.id.name == incomingUsername && opo.apo is AbstractCreature ac)
                {
                    var onlineHuds = ac.world.game.cameras[0].hud.parts
                        .OfType<PlayerSpecificOnlineHud>();

                    foreach (var onlineHud in onlineHuds)
                    {
                        OnlinePlayerDisplay usernameDisplay = null;

                        foreach (var part in onlineHud.parts.OfType<OnlinePlayerDisplay>())
                        {
                            if (part.username.text == incomingUsername)
                            {
                                usernameDisplay = part;
                                break;
                            }
                        }

                        if (usernameDisplay != null)
                        {
                            usernameDisplay.message.text = $"{lastSentMessage}";
                        }
                    }
                }

            }
        }

        [RPCMethod]
        public static void ChangeFood(short amt)
        {
            if (RWCustom.Custom.rainWorld.processManager.currentMainLoop is RainWorldGame game && game.Players[0]?.state is PlayerState state)
            {
                var newFood = Math.Max(0, Math.Min(state.foodInStomach * 4 + state.quarterFoodPoints + amt, game.session.characterStats.maxFood * 4));
                state.foodInStomach = newFood / 4;
                state.quarterFoodPoints = newFood % 4;
            }
        }

        [RPCMethod]
        public static void AddMushroomCounter()
        {
            ((RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame)?.Players[0].realizedCreature as Player).mushroomCounter += 320;
        }

        [RPCMethod]
        public static void ReinforceKarma()
        {
            ((RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame)?.session as StoryGameSession).saveState.deathPersistentSaveData.reinforcedKarma = true;
        }

        [RPCMethod]
        public static void PlayReinforceKarmaAnimation()
        {
            (RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame).cameras[0].hud.karmaMeter.reinforceAnimation = 0;
        }

        [RPCMethod]
        public static void MovePlayersToWinScreen(bool malnourished, string denPos)
        {
            var game = RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame;
            if (game == null || game.manager.upcomingProcess != null) return;

            var storyGameMode = OnlineManager.lobby.gameMode as StoryGameMode;

            if (storyGameMode.hasSheltered)
            {
                denPos = storyGameMode.myLastDenPos;
            }
            else
            {
                storyGameMode.myLastDenPos = denPos;
            }

            storyGameMode.defaultDenPos = game.GetStorySession.saveState.denPosition = denPos;

            game.Win(malnourished);

            foreach (OnlinePlayer player in OnlineManager.players)
            {
                if (!player.isMe)
                    player.InvokeOnceRPC(RPCs.GoToWinScreen, malnourished, denPos);
            }
        }

        [RPCMethod]
        public static void SessionEnded(bool survived, string newMalnourished)
        {
            if (!(RWCustom.Custom.rainWorld.processManager.currentMainLoop is RainWorldGame game && game.manager.upcomingProcess is null)) return;

                var storyGameMode = (OnlineManager.lobby.gameMode as StoryGameMode);
                if (!storyGameMode.hasSheltered)
                {
                    storyGameMode.myLastDenPos = denPos;
                }
            }

            // TODO: investigate client save desync (e.g. swallowed items)
            game.GetStorySession.saveState.SessionEnded(game, true, malnourished);

            //TODO: need to sync p5 and l2m deam events. Not doing it rn.
            DreamsState dreamsState = game.GetStorySession.saveState.dreamsState;

            if (dreamsState != null)
            {
                dreamsState.EndOfCycleProgress(game.GetStorySession.saveState, game.world.region.name, denPos);
                if (dreamsState.AnyDreamComingUp && !malnourished)
                {
                    game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Dream);
                    return;
                }
            }
            RainMeadow.Debug("I am moving to the sleepscreen");
            game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SleepScreen);
        }

        [RPCMethod]
        public static void GhostShutDown(GhostWorldPresence.GhostID ghostID)
        {
            if (!(RWCustom.Custom.rainWorld.processManager.currentMainLoop is RainWorldGame game && game.manager.upcomingProcess is null)) return;
            game.GhostShutDown(ghostID);
        }

        [RPCMethod]
        public static void KickToLobby()
        {
            var game = (RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame);
            try
            {
                if (game.manager.upcomingProcess != null)
                {
                    return;
                }
                game.ExitToMenu();
            }
            catch
            {
                RWCustom.Custom.rainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);


            }
            BanHammer.ShowBan(RWCustom.Custom.rainWorld.processManager);
        }

        [RPCMethod]
        public static void ExitToGameModeMenu()
        {
            var game = (RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame);
            if (game is null || game.manager.upcomingProcess != null) return;

            game.manager.RequestMainProcessSwitch(OnlineManager.lobby.gameMode.MenuProcessId());
        }

        [RPCMethod]
        public static void TriggerGhostHunch(string ghostID)
        {
            var game = (RWCustom.Custom.rainWorld.processManager.currentMainLoop as RainWorldGame);
            ExtEnumBase.TryParse(typeof(GhostWorldPresence.GhostID), ghostID, false, out var rawEnumBase);
            var ghostNumber = rawEnumBase as GhostWorldPresence.GhostID;
            if (ghostNumber == null) return;
            var ghostsTalkedTo = (game.session as StoryGameSession).saveState.deathPersistentSaveData.ghostsTalkedTo;
            if (!ghostsTalkedTo.ContainsKey(ghostNumber) || ghostsTalkedTo[ghostNumber] < 1)
                ghostsTalkedTo[ghostNumber] = 1;
        }
    }
}
