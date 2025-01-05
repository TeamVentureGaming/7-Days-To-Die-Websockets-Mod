namespace _7DTDWebsockets
{
    /// <summary>
    /// Helper to allow extensive debug logging when building a debug version of the mod
    /// </summary>
    public static class DebugLog
    {
        public static void Out(System.Func<string> message)
        {
#if DEBUG
            Out(message());
#endif
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)] // should probably try to make sure this is inlined so there is zero overhead in Release build
        public static void Out(string message)
        {
#if DEBUG
            Log.Out(message);
#endif
        }

        public static void Warning(System.Func<string> message)
        {
#if DEBUG
            Warning(message());
#endif
        }

        public static void Warning(string message)
        {
#if DEBUG
            Log.Warning(message);
#endif
        }

        public static void Error(System.Func<string> message)
        {
#if DEBUG
            Error(message());
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