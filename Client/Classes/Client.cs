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

                                string? name = await AuthenticateUser(stream);
                                if (name != null)
                                {
                                        _ = ReceiveMessages(stream);
                                        await SendMessage(stream, name);
                                }
                        }
                        catch (Exception ex)
                        {
                                Console.WriteLine($"Erreur de connexion : {ex.Message}");
                        }
                }

                private async Task<string?> AuthenticateUser(NetworkStream stream)
                {
                        string? name = null;

                        while (true)
                        {
                                name = GetUsername();
                                await AuthentificationMethod(stream, name);

                                string response = await GetServerResponse(stream);
                                _lastName = name;

                                if (response == "OK")
                                {
                                        await RequestAndSetUserId(stream);
                                        DisplayWelcomeMessage();
                                        return name;
                                }
                                else if (response == "FAIL")
                                {
                                        Colored("Échec de connexion ou pseudo déjà pris. Réessayez !\n", ConsoleColor.Red);
                                }
                        }
                }

                private string GetUsername()
                {
                        string? name;
                        do
                        {
                                Colored("Username : ", ConsoleColor.Blue);
                                name = Console.ReadLine();
                                if (string.IsNullOrWhiteSpace(name))
                                        Console.WriteLine("Le pseudo ne peut pas être vide !");
                        } while (string.IsNullOrWhiteSpace(name));

                        return name;
                }

                private async Task AuthentificationMethod(NetworkStream stream, string name)
                {
                        // Envoi du nom d'utilisateur
                        byte[] nameData = Encoding.UTF8.GetBytes(name);
                        await stream.WriteAsync(nameData, 0, nameData.Length);

                        // Demande et envoi du mot de passe
                        Colored("Password :", ConsoleColor.Blue);
                        string? password = Console.ReadLine();
                        if (string.IsNullOrEmpty(password)) password = "password";
                        byte[] passData = Encoding.UTF8.GetBytes(password);
                        await stream.WriteAsync(passData, 0, passData.Length);
                }

                private async Task<string> GetServerResponse(NetworkStream stream)
                {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }

                private async Task RequestAndSetUserId(NetworkStream stream)
                {
                        // Demande l'id au serveur
                        byte[] askId = Encoding.UTF8.GetBytes("getid");
                        await stream.WriteAsync(askId, 0, askId.Length);

                        // Réception de l'ID
                        byte[] idBuffer = new byte[128];
                        int idBytes = await stream.ReadAsync(idBuffer, 0, idBuffer.Length);
                        myUserId = Encoding.UTF8.GetString(idBuffer, 0, idBytes).Trim();
                }

                private void DisplayWelcomeMessage()
                {
                        Colored("Bienvenue sur le chat. Pour la liste des commandes --help, pour quitter 'exit'.\n", ConsoleColor.Green);
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
                                                        HandleMessageByType(root, type.GetString());
                                                }
                                        }
                                        catch (JsonException)
                                        {

                                        }

                                        Console.Write($"{Colored(_lastName, ConsoleColor.Blue)} > ");
                                }
                        }
                }



                private void HandleMessageByType(JsonElement root, string? messageType)
                {
                        switch (messageType)
                        {
                                case "public_message":
                                        HandlePublicMessage(root);
                                        break;
                                case "private_message":
                                        HandlePrivateMessage(root);
                                        break;
                                case "private_confirmation":
                                        HandlePrivateConfirmation(root);
                                        break;
                                case "user_list":
                                        HandleUserList(root);
                                        break;
                                case "error":
                                        HandleError(root);
                                        break;
                                case "help":
                                        HandleHelp(root);
                                        break;
                        }
                }

                private void HandlePublicMessage(JsonElement root)
                {
                        if (root.GetProperty("sender").GetString() != _lastName)
                        {
                                ClearCurrentLine();
                                Colored($"{root.GetProperty("sender").GetString()} > ", ConsoleColor.Blue);
                                Console.WriteLine(root.GetProperty("content").GetString());
                        }
                }

                private void HandlePrivateMessage(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored($"[de {root.GetProperty("sender").GetString()}] > ", ConsoleColor.Magenta);
                        Colored(root.GetProperty("content").GetString() + "\n", ConsoleColor.Magenta);
                }

                private void HandlePrivateConfirmation(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored($"[ à {root.GetProperty("recipient").GetString()}] {root.GetProperty("content").GetString()}\n", ConsoleColor.Magenta);
                }

                private void HandleUserList(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                        foreach (var user in root.GetProperty("users").EnumerateArray())
                                Console.WriteLine($"- {user.GetString()}");
                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                }

                private void HandleError(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored($"[ERREUR] {root.GetProperty("message").GetString()}\n", ConsoleColor.Red);
                }

                private void HandleHelp(JsonElement root)
                {
                        ClearCurrentLine();
                        var commands = ExtractCommands(root);
                        DisplayCommandsTable(commands);
                }

                private List<Dictionary<string, string>> ExtractCommands(JsonElement root)
                {
                        var commands = new List<Dictionary<string, string>>();
                        foreach (var cmd in root.GetProperty("commands").EnumerateArray())
                        {
                                commands.Add(new Dictionary<string, string>
                                {
                                        { "Command", cmd.GetProperty("command").GetString() ?? "" },
                                        { "Description", cmd.GetProperty("description").GetString() ?? "" }
                                });
                        }
                        return commands;
                }

                private void DisplayCommandsTable(List<Dictionary<string, string>> commands)
                {
                        Colored("╔══════════════════════════════════════════════════════════════════╗\n", ConsoleColor.Magenta);
                        Colored("║                Commandes disponibles                             ║\n", ConsoleColor.Magenta);
                        Colored("╠══════════════════════════════════════════════════════════════════╣\n", ConsoleColor.Magenta);
                        foreach (var cmd in commands)
                        {
                                Colored($"║ {cmd["Command"],-25} | {cmd["Description"],-30}         ║\n", ConsoleColor.Magenta);
                        }
                        Colored("╚══════════════════════════════════════════════════════════════════╝\n", ConsoleColor.Magenta);
                }

                private void DisplayOnlineUsers(string msg)
                {
                        var json = msg.Substring("ONLINE_USERS|".Length);
                        var userList = JsonSerializer.Deserialize<List<string>>(json);
                        ClearCurrentLine();
                        Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                        foreach (var user in userList!)
                                Console.WriteLine($"- {user}");
                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                }

                private void ClearCurrentLine()
                {
                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
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