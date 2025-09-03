using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client.Classes
{
        public class TcpClientApp
        {
                private string _lastName = "";
                string? myUserId = null;

                public async Task ConnectToServer(string serverIp, int port)
                {
                        try
                        {
                                using TcpClient client = new TcpClient();
                                await client.ConnectAsync(serverIp, port);
                                Colored($"Connecté au serveur {serverIp}:{port}\n", ConsoleColor.Green);
                                using NetworkStream stream = client.GetStream();

                                string? name = null;

                                while (true)
                                {
                                        // Demande le nom et l'envoie au serveur
                                        do
                                        {
                                                Colored("Username : ", ConsoleColor.Blue);
                                                name = Console.ReadLine();
                                                if (string.IsNullOrWhiteSpace(name))
                                                        Console.WriteLine("Le pseudo ne peut pas être vide !");
                                        } while (string.IsNullOrWhiteSpace(name));

                                        byte[] nameData = Encoding.UTF8.GetBytes(name);
                                        await stream.WriteAsync(nameData, 0, nameData.Length);

                                        // Demande le mot de passe et l'envoie au serveur
                                        Colored("Password :", ConsoleColor.Blue);
                                        string? password = Console.ReadLine();
                                        if (string.IsNullOrEmpty(password)) password = "password";
                                        byte[] passData = Encoding.UTF8.GetBytes(password);
                                        await stream.WriteAsync(passData, 0, passData.Length);

                                        // Attend la réponse du serveur
                                        byte[] buffer = new byte[1024];
                                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                        _lastName = name;
                                        if (response == "OK")
                                        {
                                                // Demande l'id au serveur (ajoute une commande spéciale)
                                                byte[] askId = Encoding.UTF8.GetBytes("getid");
                                                await stream.WriteAsync(askId, 0, askId.Length);
                                                byte[] idBuffer = new byte[128];
                                                int idBytes = await stream.ReadAsync(idBuffer, 0, idBuffer.Length);
                                                myUserId = Encoding.UTF8.GetString(idBuffer, 0, idBytes).Trim();
                                                _lastName = name;
                                                Colored("Bievenue sur le chat. Pour la liste des commandes --help, pour quitter 'exit'.\n", ConsoleColor.Green);
                                                break;
                                        }
                                        else if (response == "FAIL")
                                        {
                                                Colored("Échec de connexion ou pseudo déjà pris. Réessayez !\n", ConsoleColor.Red);
                                        }
                                }
                                _ = ReceiveMessages(stream);
                                await SendMessage(stream, name);

                        }
                        catch (Exception ex)
                        {
                                Console.WriteLine($"Erreur de connexion : {ex.Message}");
                        }
                }
                private async Task SendMessage(NetworkStream stream, string name)
                {
                        while (true)
                        {
                                Console.Write($"{Colored(name, ConsoleColor.Blue)} > ");
                                string? message = Console.ReadLine();
                                if (string.IsNullOrEmpty(message)) continue;

                                if (message == "exit")
                                {
                                        byte[] data = Encoding.UTF8.GetBytes(message);
                                        await stream.WriteAsync(data);
                                        break;
                                }
                                if (message == "list")
                                {
                                        byte[] data = Encoding.UTF8.GetBytes(message);
                                        await stream.WriteAsync(data);
                                        continue;
                                }

                                byte[] msgData = Encoding.UTF8.GetBytes(message);
                                await stream.WriteAsync(msgData);
                        }
                }


                private async Task ReceiveMessages(NetworkStream stream)
                {
                        byte[] buffer = new byte[2048];
                        int bytesRead;
                        while (true)
                        {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead > 0)
                                {
                                        string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                                        try
                                        {
                                                using var doc = JsonDocument.Parse(msg);
                                                var root = doc.RootElement;

                                                if (root.TryGetProperty("type", out var type))
                                                {
                                                        switch (type.GetString())
                                                        {
                                                                case "public_message":
                                                                        if (root.GetProperty("sender").GetString() != _lastName)
                                                                        {
                                                                                Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                                Colored($"{root.GetProperty("sender").GetString()} > ", ConsoleColor.Blue);
                                                                                Console.WriteLine(root.GetProperty("content").GetString());
                                                                        }
                                                                        break;

                                                                case "private_message":
                                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                        Colored($"[de {root.GetProperty("sender").GetString()}] > ", ConsoleColor.Magenta);
                                                                        Colored(root.GetProperty("content").GetString() + "\n", ConsoleColor.Magenta);
                                                                        break;

                                                                case "private_confirmation":
                                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                        Colored($"[ à {root.GetProperty("recipient").GetString()}] {root.GetProperty("content").GetString()}\n", ConsoleColor.Magenta);
                                                                        break;

                                                                case "user_list":
                                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                        Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                                                                        foreach (var user in root.GetProperty("users").EnumerateArray())
                                                                                Console.WriteLine($"- {user.GetString()}");
                                                                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                                                                        break;
                                                                case "error":
                                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                        Colored($"[ERREUR] {root.GetProperty("message").GetString()}\n", ConsoleColor.Red);
                                                                        break;
                                                                case "help":
                                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                                        var commands = new List<Dictionary<string, string>>();
                                                                        foreach (var cmd in root.GetProperty("commands").EnumerateArray())
                                                                        {
                                                                                commands.Add(new Dictionary<string, string>
                                                                                {
                                                                                { "Command", cmd.GetProperty("command").GetString() ?? "" },
                                                                                { "Description", cmd.GetProperty("description").GetString() ?? "" }
                                                                                });
                                                                        }

                                                                        Colored("╔══════════════════════════════════════════════════════════════════╗\n", ConsoleColor.Magenta);
                                                                        Colored("║                Commandes disponibles                             ║\n", ConsoleColor.Magenta);
                                                                        Colored("╠══════════════════════════════════════════════════════════════════╣\n", ConsoleColor.Magenta);
                                                                        foreach (var cmd in commands)
                                                                        {
                                                                                Colored($"║ {cmd["Command"],-25} | {cmd["Description"],-30}         ║\n", ConsoleColor.Magenta);
                                                                        }
                                                                        Colored("╚══════════════════════════════════════════════════════════════════╝\n", ConsoleColor.Magenta);
                                                                        break;
                                                        }
                                                }
                                        }
                                        catch (JsonException)
                                        {
                                                if (msg.StartsWith("ONLINE_USERS|"))
                                                {
                                                        var json = msg.Substring("ONLINE_USERS|".Length);
                                                        var userList = JsonSerializer.Deserialize<List<string>>(json);
                                                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                                                        Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                                                        foreach (var user in userList!)
                                                                Console.WriteLine($"- {user}");
                                                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                                                }
                                        }

                                        Console.Write($"{Colored(_lastName, ConsoleColor.Blue)} > ");
                                }
                        }
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