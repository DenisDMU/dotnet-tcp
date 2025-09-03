using System.Text.Json;
using System.Net.Sockets;
using System.Text;

namespace Server.Classes
{
        public static class Help
        {
                public static async Task SendHelp(NetworkStream stream)
                {
                        var commands = new[]
                        {
                new { Command = "/help", Description = "display this help" },
                new { Command = "exit", Description = "quit chat" },
                new { Command = "list", Description = "list online users" },
                new { Command = "/msg <pseudo> <message>", Description = "PM another user" }
            };

                        var helpJson = JsonSerializer.Serialize(commands);
                        byte[] data = Encoding.UTF8.GetBytes($"HELP|{helpJson}\n");
                        await stream.WriteAsync(data, 0, data.Length);
                }
        }
}