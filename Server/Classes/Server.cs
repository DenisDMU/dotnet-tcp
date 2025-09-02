using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server.Classes
{
        public class TcpServer
        {
                public async Task StartServer()
                {
                        IPAddress localIp = Dns.GetHostEntry(Dns.GetHostName())
                                               .AddressList[0];
                        TcpListener listener = new TcpListener(localIp, 5000);
                        listener.Start();
                        Console.WriteLine($"Serveur en écoute sur {localIp}:5000...");

                        await Task.Delay(10000);
                        listener.Stop();
                        Console.WriteLine("Connexion fermée après 10 sec.");
                }
        }
}