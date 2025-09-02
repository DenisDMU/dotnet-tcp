using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
                                Console.WriteLine($"Connecté au serveur {serverIp}:{port} !");
                                using NetworkStream stream = client.GetStream();

                                // Demande le nom et l'envoie au serveur
                                Console.Write("Entrez votre nom : ");
                                string? name = Console.ReadLine();
                                if (string.IsNullOrEmpty(name)) name = "Client";
                                byte[] nameData = Encoding.UTF8.GetBytes(name);
                                await stream.WriteAsync(nameData, 0, nameData.Length);

                                await SendMessage(stream);
                        }
                        catch (Exception ex)
                        {
                                Console.WriteLine($"Erreur de connexion : {ex.Message}");
                        }
                }

                private async Task SendMessage(NetworkStream stream)
                {
                        while (true)
                        {
                                Console.Write("Message (exit pour quitter): ");
                                string? message = Console.ReadLine();
                                if (string.IsNullOrEmpty(message)) continue;
                                if (message == "exit") break;

                                byte[] data = Encoding.UTF8.GetBytes(message);
                                await stream.WriteAsync(data);
                        }
                }
        }
}