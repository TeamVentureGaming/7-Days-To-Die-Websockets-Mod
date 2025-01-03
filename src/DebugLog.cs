//original work done by KK
//removed some unecessary using statements and slight change to authentication method by Mustached_Maniac

namespace _7DTDWebsockets
{
    /// <summary>
    /// Helper to allow extensive debug logging when building a debug version of the mod
    /// </summary>
    public static class DebugLog
    {
        public static void Out(string message)
        {
#if DEBUG
            Log.Out(message);
#endif
        }

        public static void Warning(string message)
        {
#if DEBUG
            Log.Warning(message);
#endif
        }

        public static void Error(string message)
        {
#if DEBUG
            Log.Error(message);
#endif
        }
    }
}