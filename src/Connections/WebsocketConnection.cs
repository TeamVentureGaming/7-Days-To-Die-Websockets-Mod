using WebSocketSharp.Server;

//original work done by KK
//removed uncessary using statements -MM

namespace _7DTDWebsockets.Connections
{
    internal sealed class WebsocketConnection : WebSocketBehavior
    {
        public static WebsocketConnection WebSocketInstance;

        public WebsocketConnection()
        {
            DebugLog.Out("[Websocket] WebsocketConnection created");
            WebSocketInstance = this;
        }

        public void SendBroadcast(string msg)
        {
            Sessions.Broadcast(msg);
        }
    }
}
