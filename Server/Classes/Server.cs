using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

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
                        using NetworkStream stream = client.GetStream();
                        // Reçoit le nom du client
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        Console.WriteLine($"{clientName} - {clientId} connecté");

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