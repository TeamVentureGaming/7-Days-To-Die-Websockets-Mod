using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//original work done by KK
//removed unecessary using statements -MM

namespace _7DTDWebsockets.Connections
{
    internal sealed class ConsoleConnection : ConsoleConnectionAbstract
    {
        private readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();

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

        public List<string> GetSentLines() => lines.ToList();
    }
}
