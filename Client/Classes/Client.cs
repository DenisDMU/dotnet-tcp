using System.Net.Sockets;
using System.Text;

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
                                                Colored("Bievenue sur le chat. Pour la liste des commandes /help, pour quitter 'exit'.\n", ConsoleColor.Green);
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
                                        if (msg.StartsWith("ONLINE_USERS|"))
                                        {
                                                var json = msg.Substring("ONLINE_USERS|".Length);
                                                var userList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                                                Console.WriteLine();
                                                Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                                                foreach (var user in userList!)
                                                {
                                                        Console.WriteLine($"- {user}");
                                                }
                                                Colored("-----------------------------\n", ConsoleColor.Cyan);
                                                //On remet le prompt
                                                Console.Write($"{Colored(_lastName, ConsoleColor.Blue)} > ");
                                                continue;
                                        }
                                        else if (msg.StartsWith("HELP|"))
                                        {
                                                var json = msg.Substring("HELP|".Length);
                                                var commands = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);

                                                // Affichage du tableau
                                                Console.WriteLine();
                                                Colored("╔══════════════════════════════════════════════════════════════════╗\n", ConsoleColor.Magenta);
                                                Colored("║                Commandes disponibles                             ║\n", ConsoleColor.Magenta);
                                                Colored("╠══════════════════════════════════════════════════════════════════╣\n", ConsoleColor.Magenta);
                                                foreach (var cmd in commands!)
                                                {
                                                        Colored($"║ {cmd["Command"],-25} | {cmd["Description"],-30}         ║\n", ConsoleColor.Magenta);
                                                }
                                                Colored("╚══════════════════════════════════════════════════════════════════╝\n", ConsoleColor.Magenta);
                                                Console.Write($"{Colored(_lastName, ConsoleColor.Blue)} > ");
                                                continue;
                                        }
                                        else
                                        {
                                                var pipeSplit = msg.Split('|', 2);
                                                if (pipeSplit.Length == 2)
                                                {
                                                        var userIdPart = pipeSplit[0];
                                                        var rest = pipeSplit[1];
                                                        var split = rest.Split('>', 2);
                                                        if (split.Length == 2)
                                                        {
                                                                // Affiche le message sans saut de ligne supplémentaire
                                                                Console.Write("\r"); // Retour au début de la ligne (pour éviter les mélanges)
                                                                Console.Write(new string(' ', Console.WindowWidth - 1)); // Efface la ligne actuelle
                                                                Console.Write("\r"); // Retour au début de la ligne

                                                                if (userIdPart != myUserId) // N'affiche pas mes propres messages
                                                                {
                                                                        Colored(split[0], ConsoleColor.Blue);
                                                                        Console.Write(" > ");
                                                                        Colored(split[1] + "\n", ConsoleColor.White);
                                                                }

                                                                // Réaffiche le prompt après le message
                                                                Console.Write($"{Colored(_lastName, ConsoleColor.Blue)} > ");
                                                        }
                                                }
                                        }
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