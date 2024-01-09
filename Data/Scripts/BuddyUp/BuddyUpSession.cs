using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Digi.Examples.NetworkProtobuf;
using Digi.NetworkLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace BuddyUp
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class BuddyUpSessionComponent : MySessionComponentBase
    {
        public const ushort NetworkId = (ushort)(3135280138 % ushort.MaxValue);

        public Network Net;

        private BuddyUpRequestPacket requestPacket;
        private BuddyUpConfirmationPacket confirmPacket;
        private readonly List<IMyPlayer> players = new List<IMyPlayer>(1);

        public static BuddyUpSessionComponent Instance { get; private set; }
        private readonly List<KeyValuePair<RelatablePair, DateTime>> requests = new List<KeyValuePair<RelatablePair, DateTime>>();

        private int expirySeconds = 300;

        private static Regex commandMatch = new Regex(@"^/buddyup (\w+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override void LoadData()
        {
            if (!Util.IsDedicatedServer)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
            }
            Net = new Network(NetworkId, ModContext.ModName);
            Net.SerializeTest = true;
            requestPacket = new BuddyUpRequestPacket();
            confirmPacket = new BuddyUpConfirmationPacket();
            BuddyUpRequestPacket.OnReceive += BuddyUpRequestPacket_OnReceive;
            BuddyUpConfirmationPacket.OnReceive += BuddyUpConfirmationPacket_OnReceive;

            Instance = this;
        }

        protected override void UnloadData()
        {
            if (!Util.IsDedicatedServer)
            {
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
            }
            Net?.Dispose();
            Net = null;

            BuddyUpRequestPacket.OnReceive -= BuddyUpRequestPacket_OnReceive;

            Instance = null;
        }

        private void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (messageText.StartsWith("/buddyup", StringComparison.OrdinalIgnoreCase))
            {
                sendToOthers = false;
                var identityId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                if (identityId != 0)
                {
                    ParseBuddyUpCommand(identityId, messageText);
                }
            }
        }

        private void ParseBuddyUpCommand(long sender, string messageText)
        {
            var match = commandMatch.Match(messageText);
            if (match.Success)
            {
                var senderFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(sender);

                if (senderFaction == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"You must have a faction to buddy up!");
                    return;
                }

                if (!senderFaction.IsLeader(sender))
                {
                    MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"You must be a faction leader to buddy up!");
                    return;
                }

                var receiverFaction = MyAPIGateway.Session.Factions.TryGetFactionByTag(match.Groups[1].Value);

                if (receiverFaction == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"Could not find the faction {match.Groups[1].Value} by Faction Tag (not name!)");
                    return;
                }

                if (senderFaction == receiverFaction)
                {
                    MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"Cannot send a request to your own faction!");
                    return;
                }

                if (senderFaction.IsEveryoneNpc() || receiverFaction.IsEveryoneNpc())
                {
                    MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"Cannot buddy up with an NPC faction!");
                    return;
                }
                requestPacket.Setup(receiverFaction.FactionId);
                Net.SendToServer(requestPacket);
            }
            else
            {
                MyAPIGateway.Utilities.ShowMessage("Buddy Up", $"Invalid buddyup command! Use '/buddyup TAG' where tag is the other faction's tag (case insensitive)");
            }
        }

        private void BuddyUpConfirmationPacket_OnReceive(BuddyUpConfirmationPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (!MyAPIGateway.Multiplayer.MultiplayerActive || senderSteamId == MyAPIGateway.Multiplayer.ServerId)
            {
                var senderFaction = MyAPIGateway.Session.Factions.TryGetFactionById(packet.SenderFaction);
                if (senderFaction == null)
                {
                    return;
                }
                var receiverFaction = MyAPIGateway.Session.Factions.TryGetFactionById(packet.ReceiverFaction);
                if (receiverFaction == null)
                {
                    return;
                }

                var existingRelations = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(senderFaction.FactionId, receiverFaction.FactionId);
                switch (existingRelations)
                {
                    case MyRelationsBetweenFactions.Neutral:
                    case MyRelationsBetweenFactions.Enemies:
                        break;
                    default:
                        return;
                }

                var myFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
                if (myFaction != null)
                {
                    if (myFaction == senderFaction)
                    {
                        MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(MyAPIGateway.Session.LocalHumanPlayer.IdentityId, receiverFaction.FactionId, 1500);
                        MyVisualScriptLogicProvider.SetRelationBetweenFactions(myFaction.Tag, receiverFaction.Tag, 1500);
                    }
                    if (myFaction == receiverFaction)
                    {
                        MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(MyAPIGateway.Session.LocalHumanPlayer.IdentityId, senderFaction.FactionId, 1500);
                        MyVisualScriptLogicProvider.SetRelationBetweenFactions(myFaction.Tag, senderFaction.Tag, 1500);
                    }
                }
            }

        }

        private void BuddyUpRequestPacket_OnReceive(BuddyUpRequestPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer)
            {
                try
                {
                    players.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => p.SteamUserId == senderSteamId);
                    var senderPlayer = players.FirstOrDefault();
                    players.Clear();

                    if (senderPlayer == null)
                    {
                        return;
                    }

                    var senderFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(senderPlayer.IdentityId);
                    if (senderFaction == null || !senderFaction.IsLeader(senderPlayer.IdentityId))
                    {
                        return;
                    }

                    var receiverFaction = MyAPIGateway.Session.Factions.TryGetFactionById(packet.ReceiverFaction);

                    if (receiverFaction == null)
                    {
                        return;
                    }

                    var existingRelations = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(senderFaction.FactionId, receiverFaction.FactionId);
                    switch (existingRelations)
                    {
                        case MyRelationsBetweenFactions.Neutral:
                        case MyRelationsBetweenFactions.Enemies:
                            break;
                        default:
                            MyVisualScriptLogicProvider.SendChatMessage($"The faction '{receiverFaction.Tag}' is already friendly!", "Buddy Up", senderPlayer.IdentityId);
                            return;
                    }

                    var now = DateTime.UtcNow;
                    var key = new RelatablePair(senderFaction.FactionId, receiverFaction.FactionId);
                    for (int i = requests.Count - 1; i >= 0; i--)
                    {
                        var item = requests[i];
                        if (now > item.Value)
                        {
                            requests.RemoveAtFast(i);
                            continue;
                        }
                        // Need to look up this way instead of by key because we need to check the order of the requestors
                        if (RelatablePair.Comparer.GetHashCode(key) != RelatablePair.Comparer.GetHashCode(item.Key))
                        {
                            continue;
                        }
                        if (item.Key.RelateeId1 == senderFaction.FactionId)
                        {
                            MyVisualScriptLogicProvider.SendChatMessage($"An outstanding buddy up request already exists for the faction '{receiverFaction.Tag}'. Another request can be sent in {(int)Math.Ceiling((item.Value - now).TotalSeconds)} seconds.", "Buddy Up", senderPlayer.IdentityId);
                            return;
                        }
                        if (item.Key.RelateeId2 == senderFaction.FactionId)
                        {
                            confirmPacket.Setup(senderFaction.FactionId, receiverFaction.FactionId);
                            var serialized = MyAPIGateway.Utilities.SerializeToBinary(confirmPacket);

                            SetFactionsFriendly(senderFaction, receiverFaction);
                            var messageS = $"The faction '{receiverFaction.Tag}' is now friendly!";
                            NotifyMembersInFaction(messageS, senderFaction, serialized);
                            var messageR = $"The faction '{senderFaction.Tag}' is now friendly!";
                            NotifyMembersInFaction(messageR, receiverFaction, serialized);
                            requests.RemoveAtFast(i);
                            return;
                        }
                    }
                    players.Clear();
                    var onlinePlayers = MyVisualScriptLogicProvider.GetOnlinePlayers();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => receiverFaction.IsLeader(p.IdentityId) && onlinePlayers.Contains(p.IdentityId));
                    if (players.Count > 0)
                    {
                        var message = $"The player {senderPlayer.DisplayName} in faction '{senderFaction.Tag}' wants to buddy up! Type '/buddyup {senderFaction.Tag}' in chat to accept, or just ignore this message to refuse.";
                        NotifyLeadersInFaction(message, receiverFaction);
                        requests.Add(new KeyValuePair<RelatablePair, DateTime>(new RelatablePair(senderFaction.FactionId, receiverFaction.FactionId), now.AddSeconds(expirySeconds)));
                        var message2 = $"Buddy request sent to '{receiverFaction.Tag}' by {senderPlayer.DisplayName}!";
                        NotifyLeadersInFaction(message2, senderFaction);
                    }
                    else
                    {
                        MyVisualScriptLogicProvider.SendChatMessage($"Request not sent: No leader from the faction '{receiverFaction.Tag}' is available to receive the request.");
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"Buddyup: Error: {e.Message} {e.StackTrace}");
                }
            }
        }

        private void NotifyLeadersInFaction(string message, IMyFaction faction)
        {
            players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => faction.IsLeader(p.IdentityId));
            if (players.Count > 0)
            {
                foreach (var player in players)
                {
                    MyVisualScriptLogicProvider.SendChatMessage(message, "Buddy Up", player.IdentityId);
                }
                players.Clear();
            }
        }

        private void NotifyMembersInFaction(string message, IMyFaction faction, byte[] serialized)
        {
            players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => faction.IsMember(p.IdentityId));
            if (players.Count > 0)
            {
                foreach (var player in players)
                {
                    Net.SendToPlayer(confirmPacket, player.SteamUserId, serialized);
                    MyVisualScriptLogicProvider.SendChatMessage(message, "Buddy Up", player.IdentityId);
                }
                players.Clear();
            }
        }

        private void SetFactionsFriendly(IMyFaction senderFaction, IMyFaction receiverFaction)
        {
            SetAllPlayersReputationInFaction(senderFaction, receiverFaction, 1500);
            SetAllPlayersReputationInFaction(receiverFaction, senderFaction, 1500);
            MyVisualScriptLogicProvider.SetRelationBetweenFactions(senderFaction.Tag, receiverFaction.Tag, 1500);
            MyVisualScriptLogicProvider.SetRelationBetweenFactions(receiverFaction.Tag, senderFaction.Tag, 1500);
        }

        private void SetAllPlayersReputationInFaction(IMyFaction factionToSet, IMyFaction otherFaction, int reputation)
        {
            players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(players, p => factionToSet.IsMember(p.IdentityId));
            if (players.Count > 0)
            {
                foreach (var player in players)
                {
                    MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(player.IdentityId, otherFaction.FactionId, reputation);
                }
                players.Clear();
            }
        }
    }
}