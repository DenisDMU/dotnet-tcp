using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server.Classes
{
        public class TcpServer
        {

                public async Task StartServer()
                {
                        IPAddress localIp = Dns.GetHostEntry(Dns.GetHostName())
                                               .AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                        TcpListener listener = new TcpListener(localIp, 5000);
                        listener.Start();
                        Colored($"Serveur en écoute sur {localIp}:5000...\n", ConsoleColor.Blue);

                        while (true)
                        {
                                TcpClient client = await listener.AcceptTcpClientAsync();
                                //_ pour multi client, gerer chaque client dans une tache
                                _ = HandleClient(client);
                        }
                }

                private readonly Database _db = new Database();
                private readonly List<TcpClient> _clients = new List<TcpClient>();

                private async Task HandleClient(TcpClient client)
                {
                        using NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];

                        string? username = null;
                        string? userId = null;

                        while (true)
                        {
                                // Reçoit le nom d'utilisateur
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                username = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                                // Reçoit le mot de passe
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                string password = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                                // Vérifie l'utilisateur
                                bool connected = await _db.ConnectUser(username, password);
                                if (!connected)
                                {
                                        bool created = await _db.AddUser(username, password);
                                        if (created)
                                        {
                                                userId = await _db.GetUserId(username);
                                                Colored($"New User : {username} - {userId}\n", ConsoleColor.Green);
                                                await SendResponse(stream, "OK");
                                                break;
                                        }
                                        else
                                        {
                                                Colored($"Failed Conncetion for {username} -> Wrong Password or Username already taken.\n", ConsoleColor.Red);
                                                await SendResponse(stream, "FAIL");
                                                continue; // Redemande login
                                        }
                                }
                                else
                                {
                                        userId = await _db.GetUserId(username);
                                        Colored($"User Connected : {username} (ID {userId})\n", ConsoleColor.Green);
                                        await _db.SetUserConnection(userId!, true);
                                        await SendResponse(stream, "OK");
                                        break;
                                }
                        }
                        _clients.Add(client);
                        // Boucle de réception des messages
                        while (true)
                        {
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead == 0) break;
                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                if (message == "getid")
                                {
                                        await SendResponse(stream, userId!);
                                        continue;
                                }
                                if (message == "/help")
                                {
                                        await Help.SendHelp(stream);
                                        continue;
                                }
                                if (message == "exit")
                                {
                                        await _db.SetUserConnection(userId!, false);
                                        Colored($"{username} s'est déconnecté.\n", ConsoleColor.Yellow);
                                        break;
                                }

                                if (message == "list")
                                {
                                        var users = await _db.GetConnectedUsers();
                                        var userList = users.Select(u => u["username"].AsString).ToList();
                                        var json = System.Text.Json.JsonSerializer.Serialize(userList);
                                        await SendResponse(stream, $"ONLINE_USERS|{json}");
                                        continue;
                                }

                                // Affichage et sauvegarde du message
                                Colored($"[{DateTime.Now:HH:mm:ss}] ", ConsoleColor.DarkGray);
                                Colored($"{username} ", ConsoleColor.Blue);
                                Colored($"({userId}) ", ConsoleColor.DarkGray);
                                Console.Write("> ");
                                Console.WriteLine(message);
                                await _db.SaveMessage(userId, message);
                                foreach (var otherClient in _clients)
                                {
                                        if (!otherClient.Connected || otherClient == client) continue;
                                        try
                                        {
                                                var otherStream = otherClient.GetStream();
                                                string broadcast = $"{userId}|{username}>{message}";
                                                byte[] broadcastData = Encoding.UTF8.GetBytes(broadcast);
                                                await otherStream.WriteAsync(broadcastData, 0, broadcastData.Length);
                                        }
                                        catch (Exception ex)
                                        {
                                                Colored($"Erreur diffusion à {otherClient.Client.RemoteEndPoint}: {ex.Message}\n", ConsoleColor.Red);
                                        }
                                }
                        }
                        await _db.SetUserConnection(userId!, false);
                        _clients.Remove(client);
                        client.Close();
                }

                //Méthode et Fonctions utilitaires
                // Méthode pour envoyer une réponse au client
                private async Task SendResponse(NetworkStream stream, string response)
                {
                        byte[] data = Encoding.UTF8.GetBytes(response);
                        await stream.WriteAsync(data, 0, data.Length);
                }
                private string Colored(string text, ConsoleColor color)
                {
                        Console.ForegroundColor = color;
                        Console.Write(text);
                        Console.ResetColor();
                        return "";
                }

        }
}