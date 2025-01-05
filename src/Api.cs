using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;
using _7DTDWebsockets.Connections;

//original work done by KK
//removed some unecessary using statements and slight change to authentication method by Mustached_Maniac
//fixed to work with 1.0 release and major refactoring of code by Team Venture Gaming

namespace _7DTDWebsockets
{
    public class API : IModApi
    {
        private static HttpConnection Http;

        public void InitMod(Mod mod)
        {
            ConfigureAndStartWebServer(mod.Path + "/Config.xml");
            ApplyHarmonyPatches();
            RegisterEventHandlers();
        }

        private static void ConfigureAndStartWebServer(string path)
        {
            var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreWhitespace = true,
                IgnoreComments = true
            });

            // TODO: redo this to not load all the properties, and just grab the ones it needs directly
            #region REDO ME
            var data = new Dictionary<string, string>();

            Log.Out($"[Websocket] Attempting read of \"{path}\"");

            while (reader.Read())
            {
                // NOTE: in the original code, this was returning when IsEmptyElement was true,
                // but that seems wrong so I changed it here to just skip that element
                if (reader.IsStartElement() && !reader.IsEmptyElement)
                {
                    var n = reader.Name;
                    reader.Read();
                    if (reader.IsStartElement())
                    {
                        n = reader.Name;
                    }
                    data.Add(n, reader.ReadString());
                }
            }

            foreach (var i in data)
            {
                Log.Out($"[Websocket] Config: {i.Key} : {i.Value}");
            }

            if (!data.TryGetValue("Port", out var value) || !int.TryParse(value?.ToString(), out var port))
            {
                Log.Out("[Websocket] Port not found or invalid, defaulting to 9000");
                port = 9000;
            }

            if (!data.TryGetValue("Authentication", out var auth))
            {
                Log.Out("[Websocket] No authentication key found, defaulting to empty string");
                auth = "";
            }
            #endregion

            StartConnection(port, auth);
        }

        private static void StartConnection(int port, string auth)
        {
            Log.Out($"[Websocket] Starting api on port: {port}");
            new Thread(() =>
            {
                DebugLog.Out("[Websocket] Initializing server");
                Http = new HttpConnection(port, auth);
                Http.server.AddWebSocketService<WebsocketConnection>("/");
                DebugLog.Out("[Websocket] Starting server");
                Http.server.Start();
                DebugLog.Out("[Websocket] Started server");
            })
            {
                IsBackground = true
            }.Start();
        }

        private static void StopConnection()
        {
            Http?.server.Stop();
        }

        public static void Send(string eventName, object data)
        {
            Send(eventName, JsonConvert.SerializeObject(data));
        }

        public static void Send(string eventName, string arguments)
        {
            Send(eventName + " " + arguments);
        }

        public static void Send(string message)
        {
            // TODO: not sure why we need to check if Http null here
            if (Http == null)
            {
                DebugLog.Out("[Websocket] Http is null, cannot send message");
                return;
            }

            if (WebsocketConnection.WebSocketInstance == null)
            {
                DebugLog.Out("[Websocket] WebsocketConnection is null, cannot send message");
                return;
            }

            DebugLog.Out(() => $"[Websocket] Sending message: {message}");
            WebsocketConnection.WebSocketInstance.SendBroadcast(message);
        }

        private static void ApplyHarmonyPatches()
        {
            Log.Out("[Websocket] Runtime patches initialized");
            var harmony = new HarmonyLib.Harmony("com.gmail.kk964gaming.websockets.patch");
            harmony.PatchAll();

            foreach (var method in harmony.GetPatchedMethods())
            {
                Log.Out($"Successfully patched: {method.Name}");
            }
        }

        private void RegisterEventHandlers()
        {
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.ChatMessage.RegisterHandler(ChatMessage);
            ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnect);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
            ModEvents.EntityKilled.RegisterHandler(EntityKilled);
        }

        private void GameShutdown()
        {
            StopConnection();
        }

        private sealed class ChatMsg
        {
            public readonly int senderId;
            public readonly string senderName;
            public readonly string message;
            public readonly List<int> recipients;
            public readonly int echatType;
            public readonly string chatType;

            public ChatMsg(int senderId, string senderName, string message, List<int> recipients, EChatType ct)
            {
                this.senderId = senderId;
                this.senderName = senderName;
                this.message = message;
                this.recipients = recipients ?? new List<int>(0);
                this.echatType = (int)ct;
                this.chatType = ct.ToString();
            }
        }

        private bool ChatMessage(ClientInfo clientInfo, EChatType chatType, int senderId, string message, string mainName, List<int> recipientEntityIds)
        {
            DebugLog.Out(() => $"[Websocket] Chat type: {chatType}, sender id: {senderId}, message: {message}, mainName: {mainName}, recipients: {(recipientEntityIds != null ? string.Join(", ", recipientEntityIds) : string.Empty)}");

            // TODO: need to check this in multiplayer, but this being null does not seem to be an issue actually
            //if (clientInfo == null)
            //{
            //    Log.Warning("[Websocket] Chat Message event was sent, but no client info provided.  Ignoring.");
            //    return true;
            //}

            if (string.IsNullOrEmpty(message))
            {
                Log.Warning("[Websocket] Chat Message event was sent, but message is empty.  Ignoring.");
                return true;
            }

            Send("ChatMessage", new ChatMsg(senderId, mainName, message, recipientEntityIds, chatType));
            return true;
        }

        private sealed class PlayerOnlyObj
        {
            public readonly Player player;

            public PlayerOnlyObj(Player pl)
            {
                this.player = pl;
            }
        }

        private bool PlayerLogin(ClientInfo clientInfo, string noIdea, StringBuilder stringBuilder)
        {
            DebugLog.Out(() => $"{noIdea} :: {stringBuilder?.ToString() ?? "(null)"}");
            if (clientInfo == null)
            {
                Log.Warning("[Websocket] Player Login event was sent, but no client info provided.  Ignoring.");
                return true;
            }

            Send("PlayerJoin", new PlayerOnlyObj(new Player(clientInfo)));
            return true;
        }

        private void PlayerDisconnect(ClientInfo clientInfo, bool idk)
        {
            if (clientInfo == null)
            {
                Log.Warning("[Websocket] Player Disconnect event was sent, but no client info provided.  Ignoring.");
                return;
            }

            Send("PlayerLeave", new PlayerOnlyObj(new Player(clientInfo)));
        }

        private sealed class PlayerSpawnIn
        {
            public readonly Player player;
            public readonly string type;

            public PlayerSpawnIn(Player player, string type)
            {
                this.player = player;
                this.type = type;
            }
        }

        private void PlayerSpawnedInWorld(ClientInfo clientInfo, RespawnType respawnType, Vector3i vector3I)
        {
            if (clientInfo == null)
            {
                Log.Warning("[Websocket] Player Spawn In event was sent, but no client info provided.  Ignoring.");
                return;
            }

            Send("PlayerSpawn", new PlayerSpawnIn(new Player(clientInfo), respawnType.ToString()));
        }

        private void EntityKilled(Entity killed, Entity killedBy)
        {
            DebugLog.Out(() => $"[Websocket] Entity Killed event => Killed: {FormatEntityString(killed)} | Killed By: {FormatEntityString(killedBy)}");
            if (killed == null)
            {
                Log.Warning("[Websockets] Entity Killed event was sent, but the killed entity was not provided.  Ignoring.");
                return;
            }

            if (killedBy == null) // entity was killed by something in the world like a mine or spikes
            {
                // TODO: raise some sort of event for things killed by the world
                DebugLog.Warning("[Websockets] Entity Killed event was sent, but the killed by entity was not provided so something in the world like a mine or trap killed the entity.  Ignoring.");
                return;
            }

            if (!(killedBy is EntityPlayer killingPlayer))
            {
                // TODO: raise some sort of event for things killed by non-players (zombie vs animal?)
                DebugLog.Warning("[Websockets] Entity Killed event was sent, but the killed by entity was not a player.  Ignoring.");
                return;
            }

            var player = new Player(killingPlayer.GetDebugName()); // name seems to have "Player_{playerId}" as the value, but GetDebugName() has the player's name
            var isZombie = false;
            var isAnimal = false;
            var isPlayer = false;
            var killedName = string.Empty;
            bool isHeadshot = false;

            if (killed is EntityZombie killedZombie)
            {
                isZombie = true;
                var eHit = killedZombie.bodyDamage.bodyPartHit;
                isHeadshot = eHit == EnumBodyPartHit.Head;
                killedName = killed.GetDebugName().Replace("zombie", "");
                // TODO: could provide more info around Feral / Rad
                bool isFeral = killedZombie.IsFeral;
                bool isRadiated = false;
                if (killedZombie.IsFeral)
                {
                    isRadiated = killedName.Contains("Radiated");

                    // TODO: I think this implementation would be preferred so we can have the zombie type and modifiers separate instead of baked in the name
                    //var length = killedName.Length;
                    //killedName = killedName.Replace("Feral", "");
                    //if (killedName.Length == length) // feral was not in the name, so it's probably radiated
                    //{
                    //    killedName = killedName.Replace("Radiated", "");
                    //    if (killedName.Length == length) // radiated was not in the name, so it doesn't seem to follow the expected naming
                    //    {
                    //        DebugLog.Warning("[Websockets] Killed zombie is feral, but name was not in expected format.");
                    //    }
                    //    else
                    //    {
                    //        isRadiated = true;
                    //    }
                    //}
                }

                DebugLog.Out(() => $"Player Killed Zombie ({killedName}) by hitting {eHit}.  Is Headshot: {isHeadshot}.  Is Feral: {killedZombie.IsFeral}.  Is Radiated: {isRadiated}");
                Send("PlayerKillZombie", new patchs.PlayerKilledZombieEvent(player, killedName, isFeral, isRadiated));
            }
            else if (killed is EntityAnimal killedAnimal)
            {
                isAnimal = true;
                var eHit = killedAnimal.bodyDamage.bodyPartHit;
                isHeadshot = eHit == EnumBodyPartHit.Head;
                killedName = killed.GetDebugName().Replace("animal", "");
                DebugLog.Out(() => $"Player Killed Animal ({killedName}) by hitting {eHit}.  Is Headshot: {isHeadshot}.");
                Send("PlayerKillAnimal", new patchs.PlayerEntityEvent(player, killedName));
            }
            else if (killed is EntityEnemyAnimal killedEnemyAnimal)
            {
                isAnimal = true;
                var eHit = killedEnemyAnimal.bodyDamage.bodyPartHit;
                isHeadshot = eHit == EnumBodyPartHit.Head;
                killedName = killed.GetDebugName().Replace("animal", "");
                DebugLog.Out(() => $"Player Killed Enemy Animal ({killedName}) by hitting {eHit}.  Is Headshot: {isHeadshot}.");
                Send("PlayerKillAnimal", new patchs.PlayerEntityEvent(player, killedName));
            }
            else if (killed is EntityPlayer killedPlayer)
            {
                isPlayer = true;
                var eHit = killedPlayer.bodyDamage.bodyPartHit;
                isHeadshot = eHit == EnumBodyPartHit.Head;
                killedName = killedPlayer.GetDebugName(); // name seems to have "Player_{playerId}" as the value, but GetDebugName() has the player's name
                DebugLog.Out(() => $"Player Killed another Player ({killedName}) by hitting {eHit}.  Is Headshot: {isHeadshot}.");
                Send("PlayerKillPlayer", new patchs.PlayerEntityEvent(player, killedName));
            }
            else
            {
                Log.Warning("[Websocket] Entity Killed event, but entity killed was not one of the expected types of entities.  Ignoring.");
                return;
            }

            Send("PlayerKillEntity", new patchs.PlayerKillEntityEvent(player, killedName, isAnimal, isZombie, killingPlayer.inventory.holdingItem.Name, isHeadshot));
        }

        private static string FormatEntityString(Entity a)
        {
            if (a == null)
            {
                return "(null)";
            }

            return $"CS_TYPE: {a.GetType().FullName} :: DEBUG_NAME: {a.GetDebugName()} :: Name: {a.name}, Was => Is Dead: {a.bWasDead} => {a.bDead}, Belongs Player Id: {a.belongsPlayerId}, Client Entity Id: {a.clientEntityId}, Entity Id: {a.entityId}, Spawn By (Id / Name): {a.spawnById} / {a.spawnByName}";
        }
    }
}