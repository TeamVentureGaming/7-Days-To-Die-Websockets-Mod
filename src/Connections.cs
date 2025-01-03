using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using UnityEngine;
using WebSocketSharp.Server;

namespace _7DTDWebsockets
{
    public sealed class HttpConnection
    {
        public readonly HttpServer server;
        private readonly string? authentication;
        private readonly bool hasAuth;

        public HttpConnection(int port, string auth)
        {
            if (!string.IsNullOrWhiteSpace(auth))
            {
                authentication = GetHash(auth);
                hasAuth = true;
            }
            else
            {
                authentication = null;
                hasAuth = false;
            }

            server = new HttpServer(port);
            server.OnGet += (object? sender, HttpRequestEventArgs e) =>
            {
                var req = e.Request;
                var res = e.Response;

                if (!IsAuthenticated(e))
                {
                    res.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                string path = req.RawUrl;
                if (string.IsNullOrEmpty(path) || !path.StartsWith("/api"))
                {
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                //TODO: Add paths for getting game data

                res.Close();
            };
            server.OnPost += (object? sender, HttpRequestEventArgs e) =>
            {
                var req = e.Request;
                var res = e.Response;

                if (!IsAuthenticated(e))
                {
                    res.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                string path = req.RawUrl;
                if (string.IsNullOrEmpty(path) || !path.StartsWith("/api"))
                {
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                path = path["/api".Length..];

                string content = string.Empty;
                if (req.HasEntityBody)
                {
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding, leaveOpen: false))
                    {
                        content = reader.ReadToEnd();
                    }
                }

                //TODO: Add dynamic paths to methods

                if (path == "/command")
                {
                    if (String.IsNullOrWhiteSpace(content))
                    {
                        Log.Error("Empty command received.");
                        res.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    List<string> cmdResponse = RunCommand(content);

                    var encoding = Encoding.UTF8;
                    var responseBytes = encoding.GetBytes(string.Join("\n", cmdResponse));
                    res.ContentType = "text/plain";
                    res.ContentEncoding = encoding;
                    res.ContentLength64 = responseBytes.LongLength;
                    res.Close(responseBytes, true);
                }
            };
        }

        private bool IsAuthenticated(HttpRequestEventArgs e)
        {
            if (!hasAuth)
            {
                return true;
            }

            var authHeader = e.Request.Headers["Authentication"];
            if (authHeader == null)
            {
                return false;
            }

            string auth;
            if (authHeader.StartsWith("Bearer "))
            {
                auth = authHeader["Bearer ".Length..];
            }
            else
            {
                auth = authHeader;
            }

            return StringComparer.Ordinal.Equals(GetHash(auth), authentication);
        }

        private static List<string> RunCommand(string command)
        {
            var sdtd = SingletonMonoBehaviour<SdtdConsole>.Instance;
            var console = new ConsoleConnection();
            sdtd.ExecuteAsync(command, console);

            var queueField = typeof(SdtdConsole).GetField("m_commandsToExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            if (queueField == null)
            {
                Log.Error("Could not find field m_commandsToExecuteAsync");
            }
            else
            {
                do
                {
                    object? cmdQueue = queueField.GetValue(sdtd);
                    if (cmdQueue == null)
                    {
                        Log.Warning("Command queue is null.");
                        break;
                    }

                    if (cmdQueue is not System.Collections.IEnumerable cmds)
                    {
                        Log.Warning("Command queue is not enumerable.");
                        break;
                    }

                    var isStillQueued = false;
                    foreach (var cmd in cmds)
                    {
                        var com = ReflectionUtils.GetValue(cmd, "command");
                        if (System.Object.ReferenceEquals(com, command))
                        {
                            isStillQueued = true;
                            break;
                        }
                    }
                    if (!isStillQueued)
                    {
                        break;
                    }

                    Thread.Sleep(50);
                } while (true);
            }

            return console.GetSentLines();
        }

        private static string GetHash(string raw)
        {
            byte[] bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw));

            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    public sealed class ConsoleConnection : ConsoleConnectionAbstract
    {
        private readonly ConcurrentQueue<string> lines = [];

        public override string GetDescription() => "Websocket Mod Console";

        public override void SendLine(string _text) => lines.Enqueue(_text);

        public override void SendLines(List<string> _output)
        {
            foreach (string line in _output)
            {
                SendLine(line);
            }
        }

        public override void SendLog(string _formattedMessage, string _plainMessage, string _trace, LogType _type, DateTime _timestamp, long _uptime)
        {
            if (!IsLogLevelEnabled(_type)) return;
            SendLine(_formattedMessage);
        }

        public List<string> GetSentLines() => [.. lines];
    }

    public sealed class WebsocketConnection : WebSocketBehavior
    {
        public static readonly WebsocketConnection WebSocketInstance = new ();

        public void SendBroadcast(string msg)
        {
            Sessions.Broadcast(msg);
        }
    }
}