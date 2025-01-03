using System.Text;
using System.Text.Json;
using System.Xml;
using HarmonyLib;

namespace _7DTDWebsockets
{
    public class API : IModApi
    {
        private static HttpConnection? Http;

        public void InitMod(Mod mod)
        {
            string path = ModManager.GetMod("WebsocketIntegration").Path + "/Config.xml";

            XmlReader reader = XmlReader.Create(path, new XmlReaderSettings
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

            Log.Out("[Websocket] Runtime patches initialized");
            var harmony = new Harmony("com.gmail.kk964gaming.websockets.patch");
            harmony.PatchAll();

            foreach (var method in harmony.GetPatchedMethods())
            {
                Log.Out($"Successfully patched: {method.Name}");
            }

            StartConnection(port, auth);
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.ChatMessage.RegisterHandler(ChatMessage);
            ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnect);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
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
            Send(eventName, JsonSerializer.Serialize(data));
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

// avoid formatting the message if not in debug mode
#if DEBUG
            Log.Out($"[Websocket] Sending message: {message}");
#endif
            WebsocketConnection.WebSocketInstance.SendBroadcast(message);
        }

        private void GameShutdown()
        {
            StopConnection();
        }

        private sealed class ChatMsg
        {
            public readonly Player player;
            public readonly string Message;

            public ChatMsg(Player pl, string msg)
            {
                this.player = pl;
                this.Message = msg;
            }
        }

        private bool ChatMessage(ClientInfo clientInfo, EChatType chatType, int senderId, string message, string mainName, List<int> recipientEntityIds)
        {
            if (clientInfo == null || string.IsNullOrEmpty(message))
            {
                return true;
            }

// avoid formatting the message if not in debug mode
#if DEBUG
            Log.Out($"[Websocket] Chat type: {chatType}, message: {message}");
#endif
            Send("ChatMessage", new ChatMsg(new Player(clientInfo), message));
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
            if (clientInfo == null)
            {
                return true;
            }

            Send("PlayerJoin", JsonSerializer.Serialize(new PlayerOnlyObj(new Player(clientInfo))));
            return true;
        }

        private void PlayerDisconnect(ClientInfo clientInfo, bool idk)
        {
            if (clientInfo == null)
            {
                return;
            }

            Send("PlayerLeave", JsonSerializer.Serialize(new PlayerOnlyObj(new Player(clientInfo))));
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
                return;
            }

            Send("PlayerSpawn", JsonSerializer.Serialize(new PlayerSpawnIn(new Player(clientInfo), respawnType.ToString())));
        }
    }
}