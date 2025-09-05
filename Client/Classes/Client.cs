using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client.Classes
{
        public class TcpClientApp
        {
                private string _lastName = "";
                string? myUserId = null;

                // lock pour protéger la console
                private readonly object _consoleLock = new object();

                // buffer pour le texte en cours de saisie
                private readonly StringBuilder _currentInput = new StringBuilder();

                public async Task ConnectToServer(string serverIp, int port)
                {
                        try
                        {
                                using TcpClient client = new TcpClient();
                                await client.ConnectAsync(serverIp, port);
                                Colored($"Connecté au serveur {serverIp}:{port}\n", ConsoleColor.Green);
                                using NetworkStream stream = client.GetStream();
                                string? name = null;
                                while (name == null)
                                {
                                        name = await AuthenticateUser(stream);
                                }
                                _ = ReceiveMessages(stream);
                                await SendMessage(stream, name);
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
                        byte[] nameData = Encoding.UTF8.GetBytes(name);
                        await stream.WriteAsync(nameData, 0, nameData.Length);

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
                        var request = new { type = "getid" };
                        string json = JsonSerializer.Serialize(request) + "\n";
                        byte[] askId = Encoding.UTF8.GetBytes(json);
                        await stream.WriteAsync(askId, 0, askId.Length);

                        byte[] idBuffer = new byte[128];
                        int idBytes = await stream.ReadAsync(idBuffer, 0, idBuffer.Length);
                        myUserId = Encoding.UTF8.GetString(idBuffer, 0, idBytes).Trim();
                }

                private void DisplayWelcomeMessage()
                {
                        Colored("Bienvenue sur le chat. Pour la liste des commandes --help, pour quitter 'exit'.\n", ConsoleColor.Green);
                }

                // L'envoi de message ici, on utilise InputReading à la place de Console.ReadLine
                private async Task SendMessage(NetworkStream stream, string name)
                {
                        while (true)
                        {
                                string message = await InputReading(name);

                                if (string.IsNullOrEmpty(message))
                                        continue;

                                if (message == "exit")
                                {
                                        byte[] data = Encoding.UTF8.GetBytes(message);
                                        await stream.WriteAsync(data);
                                        break;
                                }

                                if (message == "--list")
                                {
                                        byte[] data = Encoding.UTF8.GetBytes(message);
                                        await stream.WriteAsync(data);
                                        continue;
                                }

                                byte[] msgData = Encoding.UTF8.GetBytes(message);
                                await stream.WriteAsync(msgData);
                        }
                }

                // lecture des inputs de l'utilisateur, avec gestion du prompt
                private Task<string> InputReading(string name)
                {
                        return Task.Run(() =>
                        {
                                // On init le prompt
                                InitializePrompt(name);

                                // boucle pour lire les touches
                                while (true)
                                {
                                        var key = Console.ReadKey(true); // on capture la touche

                                        lock (_consoleLock)
                                        {
                                                // si c'est Enter, on retourne le texte saisi
                                                if (key.Key == ConsoleKey.Enter)
                                                {
                                                        Console.WriteLine();
                                                        Console.CursorVisible = false;
                                                        string result = _currentInput.ToString();
                                                        _currentInput.Clear();
                                                        return result;
                                                }

                                                // on traite la touche
                                                ProcessKeyInput(key);

                                                // On affiche le prompt mis à jour
                                                PromptBasic(name);
                                        }
                                }
                        });
                }

                // Initialise le prompt et prépare la saisie
                private void InitializePrompt(string name)
                {
                        lock (_consoleLock)
                        {
                                _currentInput.Clear();
                                PromptBasic(name);
                                Console.CursorVisible = true;
                        }
                }

                // Traite une touche du clavier
                private void ProcessKeyInput(ConsoleKeyInfo key)
                {
                        // touche effacer
                        if (key.Key == ConsoleKey.Backspace)
                        {
                                if (_currentInput.Length > 0)
                                        _currentInput.Remove(_currentInput.Length - 1, 1);
                        }
                        // caractère standard
                        else if (!char.IsControl(key.KeyChar))
                        {
                                _currentInput.Append(key.KeyChar);
                        }
                }
                // on  affiche notre prompt de base :)
                private void PromptBasic(string name)
                {
                        ClearCurrentLine();
                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored(name, ConsoleColor.Blue);
                        Console.Write(" > ");
                        Console.Write(_currentInput.ToString());
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
                                                        lock (_consoleLock)
                                                        {
                                                                // Chaque handler efface d'abord la ligne courante avant d'afficher
                                                                HandleMessageByType(root, type.GetString());
                                                                // on remet notre prompt de base
                                                                PromptBasic(_lastName);
                                                        }
                                                }
                                        }
                                        catch (JsonException) { }
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
                        // On efface la ligne courante avant d'afficher le message
                        ClearCurrentLine();

                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($"{root.GetProperty("sender").GetString()} > ", ConsoleColor.Blue);
                        Console.WriteLine(root.GetProperty("content").GetString());
                }

                private void HandlePrivateMessage(JsonElement root)
                {
                        ClearCurrentLine();

                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($"de {root.GetProperty("sender").GetString()} > ", ConsoleColor.Magenta);
                        Colored(root.GetProperty("content").GetString() + "\n", ConsoleColor.Magenta);
                }

                private void HandlePrivateConfirmation(JsonElement root)
                {
                        ClearCurrentLine();

                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($"à {root.GetProperty("recipient").GetString()} > {root.GetProperty("content").GetString()}\n", ConsoleColor.Magenta);
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
                                Colored($"║ {cmd["Command"],-25} | {cmd["Description"],-30}       ║\n", ConsoleColor.Magenta);
                        }
                        Colored("╚════════════════════════════════════════════════════════════════╝\n", ConsoleColor.Magenta);
                }

                private void ClearCurrentLine()
                {
                        try
                        {
                                int width = Console.WindowWidth;
                                // on efface la ligne courante
                                Console.Write("\r" + new string(' ', Math.Max(0, width - 1)) + "\r");
                        }
                        catch
                        {
                                //si on a pas la largeur, on retourne juste au début de la ligne
                                Console.Write("\r");
                        }
                }

                // Colored pour donner des couleurs aux textes
                private void Colored(string text, ConsoleColor color)
                {
                        Console.ForegroundColor = color;
                        Console.Write(text);
                        Console.ResetColor();
                }
        }
}
