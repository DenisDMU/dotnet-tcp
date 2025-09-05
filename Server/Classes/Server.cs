using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Server.Classes
{
        public class TcpServer
        {
                private readonly Database _db = new Database();
                private readonly List<TcpClient> _clients = new List<TcpClient>();
                private readonly Dictionary<string, TcpClient> _usernameToClient = new Dictionary<string, TcpClient>();
                private Broadcast? _broadcast;

                public async Task StartServer()
                {
                        IPAddress serverIp = IPAddress.Any;
                        TcpListener listener = new TcpListener(serverIp, 5000);
                        listener.Start();
                        _broadcast = new Broadcast(_db, _clients, _usernameToClient);

                        // Affiche les adresses IP disponibles pour la connexion
                        DisplayServerAddresses(listener);

                        // Boucle pour accepter clients
                        while (true)
                        {
                                TcpClient client = await listener.AcceptTcpClientAsync();
                                _ = ManageClient(client); // On peut gérer chaque client en parallèle
                        }
                }

                private void DisplayServerAddresses(TcpListener listener)
                {
                        var host = Dns.GetHostEntry(Dns.GetHostName());
                        Colored("Serveur en écoute sur les adresses suivantes :\n", ConsoleColor.Blue);
                        foreach (var ip in host.AddressList)
                        {
                                if (ip.AddressFamily == AddressFamily.InterNetwork)
                                {
                                        Colored($"- {ip}:{((IPEndPoint)listener.LocalEndpoint).Port}\n", ConsoleColor.Blue);
                                }
                        }
                }

                private async Task ManageClient(TcpClient client)
                {
                        using (client)
                        using (NetworkStream stream = client.GetStream())
                        {
                                // Authentification du client
                                var authResult = await AuthenticateClient(stream);
                                if (!authResult.success)
                                        return;

                                string username = authResult.username!;
                                string userId = authResult.userId!;

                                //  Ajout du client aux collections
                                AddClientToCollections(client, username);

                                //  Boucle de gestion des messages
                                await HandleClientMessages(stream, username, userId, client);

                                // Nettoyage à la déconnexion
                                await CleanupClientDisconnection(userId, username);
                        }
                }

                private async Task<(bool success, string? username, string? userId)> AuthenticateClient(NetworkStream stream)
                {
                        byte[] buffer = new byte[1024];

                        // on récup le  username
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string username = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        // on récup le  mot de passe
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string password = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        // Tentative de connexion
                        bool connected = await _db.ConnectUser(username, password);
                        if (!connected)
                        {
                                // Tentative de création de compte si la connexion échoue
                                bool created = await _db.AddUser(username, password);
                                if (!created)
                                {
                                        Colored($"Échec de la connexion pour {username} -> Mot de passe incorrect ou nom d'utilisateur déjà pris.\n", ConsoleColor.Red);
                                        await SendResponse(stream, "FAIL");
                                        return (false, null, null);
                                }
                                string? userId = await _db.GetUserId(username);
                                Colored($"Nouvel utilisateur : {username} - {userId}\n", ConsoleColor.Green);
                                await SendResponse(stream, "OK");
                                return (true, username, userId);
                        }
                        else
                        {
                                string? userId = await _db.GetUserId(username);
                                Colored($"Utilisateur connecté : {username} (ID {userId})\n", ConsoleColor.Green);
                                await _db.SetUserConnection(userId!, true);
                                await SendResponse(stream, "OK");
                                return (true, username, userId);
                        }
                }

                private void AddClientToCollections(TcpClient client, string username)
                {
                        _clients.Add(client);
                        _usernameToClient[username] = client;
                }

                private async Task HandleClientMessages(NetworkStream stream, string username, string userId, TcpClient client)
                {
                        byte[] buffer = new byte[1024];
                        bool waitingForIdRequest = true;

                        while (true)
                        {
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead == 0) break;

                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                                // Gestion du getid qu'on ne veut pas mettre a disposition des users
                                if (message == "getid" && waitingForIdRequest)
                                {
                                        await SendResponse(stream, userId);
                                        waitingForIdRequest = false;
                                        continue;
                                }
                                else if (message == "getid")
                                {
                                        continue;
                                }

                                if (await HandleSpecialCommands(stream, message, username, userId, client))
                                        continue;

                                // par défaut, message public
                                await HandlePublicMessage(stream, message, username, userId);
                        }
                }

                private async Task<bool> HandleSpecialCommands(NetworkStream stream, string message, string username, string userId, TcpClient client)
                {
                        if (message == "--help")
                        {
                                await Help.SendHelp(client);
                                return true;
                        }
                        if (message == "exit")
                        {
                                await _db.SetUserConnection(userId, false);
                                Colored($"{username} s'est déconnecté.\n", ConsoleColor.Yellow);
                                return true;
                        }
                        if (message == "list")
                        {
                                await _broadcast!.BroadcastUserList(client);
                                return true;
                        }
                        if (message.StartsWith("/msg "))
                        {
                                await HandlePrivateMessage(stream, message, userId, username);
                                return true;
                        }
                        return false;
                }

                private async Task HandlePrivateMessage(NetworkStream stream, string message, string userId, string username)
                {
                        var parts = message.Split(' ', 3);
                        if (parts.Length == 3)
                        {
                                string recipient = parts[1];
                                string privateMessage = parts[2];
                                if (_usernameToClient.TryGetValue(recipient, out _))
                                {
                                        await _broadcast!.BroadcastPrivate(userId, username, recipient, privateMessage);
                                        // Confirme l'envoi
                                        var confirmation = new { type = "private_confirmation", recipient, content = privateMessage };
                                        string confirmJson = JsonSerializer.Serialize(confirmation) + "\n";
                                        await stream.WriteAsync(Encoding.UTF8.GetBytes(confirmJson), 0, confirmJson.Length);
                                }
                                else
                                {
                                        // Message si le destinataire n'est pas connecté
                                        var errorMsg = new { type = "error", message = $"Utilisateur {recipient} non connecté" };
                                        string errorJson = JsonSerializer.Serialize(errorMsg) + "\n";
                                        await stream.WriteAsync(Encoding.UTF8.GetBytes(errorJson), 0, errorJson.Length);
                                }
                        }
                }

                // Affichage des messages publics dans le server pour monitorer tout ça 
                private async Task HandlePublicMessage(NetworkStream stream, string message, string username, string userId)
                {
                        Colored($"[{DateTime.Now:HH:mm:ss}] ", ConsoleColor.DarkGray);
                        Colored($"{username} ", ConsoleColor.Blue);
                        Colored($"({userId}) ", ConsoleColor.DarkGray);
                        Console.Write("> ");
                        Console.WriteLine(message);
                        await _db.SaveMessage(userId, message);
                        await _broadcast!.BroadcastPublic(userId, username, message, _usernameToClient[username]);
                }

                private async Task CleanupClientDisconnection(string userId, string username)
                {
                        await _db.SetUserConnection(userId, false);
                        if (_usernameToClient.TryGetValue(username, out var client))
                        {
                                _clients.Remove(client);
                                _usernameToClient.Remove(username);
                        }
                }

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
                        return text;
                }
        }
}
