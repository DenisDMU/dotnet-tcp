using System.Net.Sockets;
using System.Text;

namespace Client.Classes
{
        public class TcpClientApp
        {
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

                                        if (response == "OK")
                                        {
                                                Colored("Connexion réussie ! Si vous voulez vous déconnecter, tapez 'exit'.\n", ConsoleColor.Green);
                                                break;
                                        }
                                        else if (response == "FAIL")
                                        {
                                                Colored("Échec de connexion ou pseudo déjà pris. Réessayez !\n", ConsoleColor.Red);
                                        }
                                }

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

                                        // Attend la réponse du serveur
                                        byte[] buffer = new byte[2048];
                                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                                        Colored("\n--- Utilisateurs connectés ---\n", ConsoleColor.Cyan);
                                        Console.WriteLine(response);
                                        Colored("-----------------------------\n", ConsoleColor.Cyan);
                                        continue;
                                }

                                byte[] msgData = Encoding.UTF8.GetBytes(message);
                                await stream.WriteAsync(msgData);
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