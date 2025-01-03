using System.Reflection;

namespace _7DTDWebsockets
{
    public static class ReflectionUtils
    {
        public static object? GetValue(this object? obj, string? field)
        {
            if (obj == null)
            {
                return null;
            }

            if (field == null)
            {
                return null;
            }

            return obj.GetType()
                      .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                     ?.GetValue(obj);
        }
    }
}