
namespace SOS2SRTSLANDINGZONE
{
    public static class Helper
    {
        public static void Log(string message, bool warning = false)
        {
            message = "[Roofless hull] " + message;

            if (warning)
            {
                Verse.Log.Warning(message);
            }
            else
            {
                Verse.Log.Message(message);
            }
        }
    }
}
