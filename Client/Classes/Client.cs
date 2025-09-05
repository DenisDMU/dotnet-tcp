using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client.Classes
{
        public class TcpClientApp
        {
                private string _lastName = "";
                string? myUserId = null;

                /// Établit une connexion TCP avec le serveur spécifié.
                /// Gère l'authentification de l'utilisateur, puis lance les tâches d'envoi et de réception des messages en parallèle.     
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

                /// Boucle d'authentification qui demande un nom d'utilisateur et un mot de passe jusqu'à ce que le serveur valide la connexion.
                /// Retourne le nom d'utilisateur validé ou null en cas d'échec.
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

                /// Demande à l'utilisateur de saisir un nom d'utilisateur, pour éviter les entrées vides
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

                /// Envoie le nom d'utilisateur et le mot de passe au serveur pour authentification.
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

                /// On va lire la réponse du serveur (ok ou fail) après tentative de connexion
                private async Task<string> GetServerResponse(NetworkStream stream)
                {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }

                /// On récupère l'id de l'user auprès du serveur et on le stock dans myUserId
                private async Task RequestAndSetUserId(NetworkStream stream)
                {
                        // Demande l'id au serveur
                        var request = new { type = "getid" };
                        string json = JsonSerializer.Serialize(request) + "\n";
                        byte[] askId = Encoding.UTF8.GetBytes(json);
                        await stream.WriteAsync(askId, 0, askId.Length);

                        // Réception de l'ID
                        byte[] idBuffer = new byte[128];
                        int idBytes = await stream.ReadAsync(idBuffer, 0, idBuffer.Length);
                        myUserId = Encoding.UTF8.GetString(idBuffer, 0, idBytes).Trim();
                }

                /// Affichage message de bienvenue avec les commandes utiles
                private void DisplayWelcomeMessage()
                {
                        Colored("Bienvenue sur le chat. Pour la liste des commandes --help, pour quitter 'exit'.\n", ConsoleColor.Green);
                }

                /// Boucle d'envoi de messages qui lit les entrées utilisateur et les envoie au serveur.
                /// On gère aussi list et exit aussi
                private async Task SendMessage(NetworkStream stream, string name)
                {
                        while (true)
                        {
                                // On génère le timestamp actuel
                                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                                // On affiche le prompt avec le timestamp
                                Console.Write($"{Colored($"[{timestamp}] ", ConsoleColor.DarkGray)}{Colored(name, ConsoleColor.Blue)} > ");
                                string? message = Console.ReadLine();
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

                /// Écoute en continu les messages envoyés par le serveur.
                /// On parse les JSON reçus que l'on traite ensuite par "type" c'est à dire public_message, private_message, user_list, error, help
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
                                        catch (JsonException) { }
                                        // Réaffiche le prompt avec le timestamp actuel
                                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                                        Console.Write($"{Colored($"[{timestamp}] ", ConsoleColor.DarkGray)}{Colored(_lastName, ConsoleColor.Blue)} > ");
                                }
                        }
                }


                /// Aiguille le traitement des messages reçus en fonction de leur type (public, privé, erreur, etc.).
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

                /// Gestion des messages en mode public
                private void HandlePublicMessage(JsonElement root)
                {
                        ClearCurrentLine();


                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($"{root.GetProperty("sender").GetString()} > ", ConsoleColor.Blue);
                        Console.WriteLine(root.GetProperty("content").GetString());
                }


                /// Affiche un message privé reçu avec le nom de l'expéditeur
                private void HandlePrivateMessage(JsonElement root)
                {
                        ClearCurrentLine();

                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($"de {root.GetProperty("sender").GetString()} > ", ConsoleColor.Magenta);
                        Colored(root.GetProperty("content").GetString() + "\n", ConsoleColor.Magenta);
                }

                /// Affiche pour l'expéditeur le message privé qu'il envoi (comme les /w de WoW ou autres jeux)
                private void HandlePrivateConfirmation(JsonElement root)
                {
                        ClearCurrentLine();
                        string timestamp = root.TryGetProperty("timestamp", out var postime) ? postime.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(timestamp))
                                Colored($"[{timestamp}] ", ConsoleColor.DarkGray);
                        Colored($" à {root.GetProperty("recipient").GetString()} > {root.GetProperty("content").GetString()}\n", ConsoleColor.Magenta);
                }

                //Affiche une liste des utilisateurs connectés
                private void HandleUserList(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored("\n--------- Online ---------------\n", ConsoleColor.Cyan);
                        foreach (var user in root.GetProperty("users").EnumerateArray())
                                Console.WriteLine($"- {user.GetString()}");
                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                }

                //Gestion des messages d'erreur du serveur ici
                private void HandleError(JsonElement root)
                {
                        ClearCurrentLine();
                        Colored($"[ERREUR] {root.GetProperty("message").GetString()}\n", ConsoleColor.Red);
                }

                //liste des commandes disponibles venant du serveur
                private void HandleHelp(JsonElement root)
                {
                        ClearCurrentLine();
                        var commands = ExtractCommands(root);
                        DisplayCommandsTable(commands);
                }

                // On récupère les fameuses commandes depuis le JSON ici en créant une liste de dictionnaires
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

                //On affiche ici les commandes dans un truc pas mal (même si la bordure de droite se fait la malle)
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


                //On efface la ligne actuelle de la console pour nettoyer l'affichage
                private void ClearCurrentLine()
                {
                        // On efface la ligne et on replace le curseur au début
                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                }

                // Méthode utilitaire pour afficher du texte en couleur dans la console
                private string Colored(string text, ConsoleColor color)
                {
                        Console.ForegroundColor = color;
                        Console.Write(text);
                        Console.ResetColor();
                        return "";
                }
        }
}