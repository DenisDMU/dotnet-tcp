using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

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
                        var host = Dns.GetHostEntry(Dns.GetHostName());
                        Colored("Serveur en écoute sur les adresses suivantes :\n", ConsoleColor.Blue);
                        //liste les ip pour se connecter au chat (local ou public)
                        foreach (var ip in host.AddressList)
                        {
                                if (ip.AddressFamily == AddressFamily.InterNetwork)
                                {
                                        Colored($"- {ip}:{((IPEndPoint)listener.LocalEndpoint).Port}\n", ConsoleColor.Blue);
                                }
                        }
                        while (true)
                        {
                                TcpClient client = await listener.AcceptTcpClientAsync();
                                _ = ManageClient(client);
                        }
                }

                private async Task ManageClient(TcpClient client)
                {
                        using NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];
                        string? username = null;
                        string? userId = null;
                        bool waitingForIdRequest = true; // Flag pour autoriser getid uniquement après login

                        // Authentification
                        while (true)
                        {
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                username = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                string password = Encoding.UTF8.GetString(buffer, 0, bytesRead);

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
                                                Colored($"Failed Connection for {username} -> Wrong Password or Username already taken.\n", ConsoleColor.Red);
                                                await SendResponse(stream, "FAIL");
                                                continue;
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

                        // Ajoute le client au dictionnaire
                        _clients.Add(client);
                        _usernameToClient[username!] = client;

                        // Boucle de réception des messages
                        while (true)
                        {
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead == 0) break;

                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                                if (message == "getid" && waitingForIdRequest)
                                {
                                        await SendResponse(stream, userId!);
                                        waitingForIdRequest = false;
                                        continue;
                                }
                                else if (message == "getid")
                                {

                                        continue;
                                }
                                if (message == "--help")
                                {
                                        await Help.SendHelp(client);
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
                                        await _broadcast!.BroadcastUserList(client);
                                        continue;
                                }
                                if (message.StartsWith("/msg "))
                                {
                                        var parts = message.Split(' ', 3);
                                        if (parts.Length == 3)
                                        {
                                                string recipient = parts[1];
                                                string privateMessage = parts[2];

                                                if (_usernameToClient.ContainsKey(recipient))
                                                {
                                                        await _broadcast!.BroadcastPrivate(userId!, username!, recipient, privateMessage);

                                                        // Confirme l'envoi
                                                        var confirmation = new
                                                        {
                                                                type = "private_confirmation",
                                                                recipient = recipient,
                                                                content = privateMessage
                                                        };
                                                        string confirmJson = JsonSerializer.Serialize(confirmation) + "\n";
                                                        await stream.WriteAsync(Encoding.UTF8.GetBytes(confirmJson), 0, confirmJson.Length);
                                                }
                                                else
                                                {
                                                        // Informe que le destinataire n'est pas connecté
                                                        var errorMsg = new
                                                        {
                                                                type = "error",
                                                                message = $"Utilisateur {recipient} non connecté"
                                                        };
                                                        string errorJson = JsonSerializer.Serialize(errorMsg) + "\n";
                                                        await stream.WriteAsync(Encoding.UTF8.GetBytes(errorJson), 0, errorJson.Length);
                                                }
                                        }
                                        continue;
                                }

                                // Message public
                                Colored($"[{DateTime.Now:HH:mm:ss}] ", ConsoleColor.DarkGray);
                                Colored($"{username} ", ConsoleColor.Blue);
                                Colored($"({userId}) ", ConsoleColor.DarkGray);
                                Console.Write("> ");
                                Console.WriteLine(message);

                                await _db.SaveMessage(userId, message);
                                await _broadcast!.BroadcastPublic(userId!, username!, message, client);
                        }

                        // Nettoyage à la déconnexion
                        await _db.SetUserConnection(userId!, false);
                        _clients.Remove(client);
                        _usernameToClient.Remove(username!);
                        client.Close();
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
