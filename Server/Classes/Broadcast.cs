using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server.Classes
{
        public class Broadcast
        {
                private readonly Database _db;
                private readonly List<TcpClient> _clients;
                private readonly Dictionary<string, TcpClient> _usernameToClient;

                public Broadcast(Database db, List<TcpClient> clients, Dictionary<string, TcpClient> usernameToClient)
                {
                        _db = db;
                        _clients = clients;
                        _usernameToClient = usernameToClient;
                }

                public async Task BroadcastPublic(string senderId, string senderName, string message, TcpClient senderClient)
                {
                        var broadcastMsg = new
                        {
                                type = "public_message",
                                sender = senderName,
                                content = message,
                                timestamp = DateTime.Now.ToString("HH:mm:ss")
                        };
                        string json = JsonSerializer.Serialize(broadcastMsg) + "\n";

                        foreach (var client in _clients)
                        {
                                if (!client.Connected || client == senderClient) continue;
                                try
                                {
                                        await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(json), 0, json.Length);
                                }
                                catch (Exception ex)
                                {
                                        Console.WriteLine($"Erreur diffusion publique: {ex.Message}");
                                }
                        }
                }

                public async Task BroadcastPrivate(string senderId, string senderName, string recipientName, string message)
                {
                        // Sauvegarde dans MongoDB
                        await _db.SavePrivateMessage(senderName, recipientName, message);

                        // Trouve le client destinataire dans le dictionnaire
                        if (_usernameToClient.TryGetValue(recipientName, out var recipientClient))
                        {
                                if (recipientName == senderName)
                                {

                                        return;
                                }
                                var privateMsg = new
                                {
                                        type = "private_message",
                                        sender = senderName,
                                        content = message,
                                        timestamp = DateTime.Now.ToString("HH:mm:ss")
                                };
                                string json = JsonSerializer.Serialize(privateMsg) + "\n";

                                try
                                {
                                        await recipientClient.GetStream().WriteAsync(Encoding.UTF8.GetBytes(json), 0, json.Length);

                                }
                                catch (Exception ex)
                                {
                                        Console.WriteLine($"MP => Erreur lors de l'envoi du MP: {ex.Message}");
                                }
                        }
                        else
                        {
                                Console.WriteLine($"MP => Destinataire {recipientName} introuvable ou déconnecté");
                        }
                }

                public async Task BroadcastUserList(TcpClient client)
                {
                        var users = await _db.GetConnectedUsers();
                        var userList = users.Select(u => u["username"].AsString).ToList();
                        var response = new
                        {
                                type = "user_list",
                                users = userList
                        };
                        string json = JsonSerializer.Serialize(response) + "\n";
                        var stream = client.GetStream();
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        await stream.WriteAsync(data, 0, data.Length);
                }
        }
}
