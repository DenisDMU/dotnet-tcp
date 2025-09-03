// using System.Net.Sockets;
// using System.Text;
// using System.Text.Json;
// using MongoDB.Bson;

// namespace Server.Classes
// {
//         public class Broadcast
//         {
//                 private readonly Database _db;
//                 private readonly List<TcpClient> _clients;

//                 public Broadcast(Database db, List<TcpClient> clients)
//                 {
//                         _db = db;
//                         _clients = clients;
//                 }

//                 // Diffuse un message public à tous les clients
//                 public async Task BroadcastPublic(string senderId, string senderName, string message)
//                 {
//                         var broadcastMsg = new
//                         {
//                                 type = "public_message",
//                                 sender = senderName,
//                                 content = message,
//                                 timestamp = DateTime.UtcNow.ToString("o")
//                         };
//                         string json = JsonSerializer.Serialize(broadcastMsg) + "\n";

//                         foreach (var client in _clients)
//                         {
//                                 if (!client.Connected) continue;
//                                 try
//                                 {
//                                         var stream = client.GetStream();
//                                         byte[] data = Encoding.UTF8.GetBytes(json);
//                                         await stream.WriteAsync(data, 0, data.Length);
//                                 }
//                                 catch (Exception ex)
//                                 {
//                                         Console.WriteLine($"Erreur diffusion publique: {ex.Message}");
//                                 }
//                         }
//                 }

//                 // Envoie un message privé à un destinataire spécifique
//                 private async Task<TcpClient?> FindRecipientClient(string recipientName)
//                 {
//                         string? recipientId = await _db.GetUserId(recipientName);
//                         if (recipientId == null) return null;

//                         foreach (var client in _clients)
//                         {
//                                 if (!client.Connected) continue;
//                                 try
//                                 {
//                                         var stream = client.GetStream();
//                                         await stream.WriteAsync(Encoding.UTF8.GetBytes("getid"), 0, 5);
//                                         byte[] buffer = new byte[128];
//                                         int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
//                                         string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
//                                         if (response == recipientId)
//                                                 return client;
//                                 }
//                                 catch { }
//                         }
//                         return null;
//                 }

//                 // Puis dans BroadcastPrivate :
//                 public async Task BroadcastPrivate(string senderId, string senderName, string recipientName, string message)
//                 {
//                         await _db.SavePrivateMessage(senderName, recipientName, message);
//                         var recipientClient = await FindRecipientClient(recipientName);

//                         if (recipientClient != null)
//                         {
//                                 var privateMsg = new { type = "private_message", sender = senderName, content = message, timestamp = DateTime.UtcNow.ToString("o") };
//                                 string json = JsonSerializer.Serialize(privateMsg) + "\n";
//                                 await recipientClient.GetStream().WriteAsync(Encoding.UTF8.GetBytes(json), 0, json.Length);
//                         }
//                 }


//                 // Envoie la liste des utilisateurs connectés
//                 public async Task BroadcastUserList(TcpClient client)
//                 {
//                         var users = await _db.GetConnectedUsers();
//                         var userList = users.Select(u => u["username"].AsString).ToList();
//                         var response = new
//                         {
//                                 type = "user_list",
//                                 users = userList
//                         };
//                         string json = JsonSerializer.Serialize(response) + "\n";
//                         var stream = client.GetStream();
//                         byte[] data = Encoding.UTF8.GetBytes(json);
//                         await stream.WriteAsync(data, 0, data.Length);
//                 }
//         }
// }
