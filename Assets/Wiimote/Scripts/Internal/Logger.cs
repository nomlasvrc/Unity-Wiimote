namespace WiimoteApi
{
    public class Logger
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine("Warning: " + message);
        public static void LogError(object message) => Console.WriteLine("Error: " + message);
    }
}