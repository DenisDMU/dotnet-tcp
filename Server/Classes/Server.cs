using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server.Classes
{
        public class TcpServer
        {
                private readonly List<Task> _clientTasks = new();
                private int _clientCounter = 0;

                public async Task StartServer()
                {
                        IPAddress localIp = Dns.GetHostEntry(Dns.GetHostName())
                                               .AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                        TcpListener listener = new TcpListener(localIp, 5000);
                        listener.Start();
                        Console.WriteLine($"Serveur en écoute sur {localIp}:5000...");

                        // boucle permettant d'accepter plusieurs clients (on les ajoutes dans une liste)
                        while (true)
                        {
                                TcpClient client = await listener.AcceptTcpClientAsync();
                                _clientCounter++;
                                int clientId = _clientCounter;
                                var task = handleClient(client, clientId);
                                _clientTasks.Add(task);
                        }
                }

                private async Task handleClient(TcpClient client, int clientId)
                {
                        // Récupère le flux réseau
                        using NetworkStream stream = client.GetStream();
                        // nom client
                        byte[] buffer = new byte[1024];
                        // Lit le nom du client
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        // Convertit les données en chaîne de caractères
                        string clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        Console.WriteLine($"{clientName} - {clientId} connecté");

                        // Boucle de réception des messages
                        while (true)
                        {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead == 0) break;
                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                Console.WriteLine($"[{clientName} | ID {clientId}] Message : {message}");
                        }
                        Console.WriteLine($"{clientId} - ({clientName}) déconnecté.");
                        client.Close();
                }
        }
}