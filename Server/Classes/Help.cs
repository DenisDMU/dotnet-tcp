using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server.Classes
{
        public static class Help
        {
                public static async Task SendHelp(TcpClient client)
                {
                        var helpMessage = new
                        {
                                type = "help",
                                commands = new[]
                            {
                    new { command = "--help", description = "Affiche cette aide" },
                    new { command = "/msg <user> <message>", description = "Envoyer un message privé" },
                    new { command = "--list", description = "Lister les utilisateurs connectés" },
                    new { command = "exit", description = "Quitter le chat" }
                }
                        };
                        string json = JsonSerializer.Serialize(helpMessage) + "\n";
                        var stream = client.GetStream();
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        await stream.WriteAsync(data, 0, data.Length);
                }
        }
}
