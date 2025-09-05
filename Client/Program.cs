using Client.Classes;

TcpClientApp client = new TcpClientApp();

//On utilise ConnectToServer en indiquant l'ip ciblé et le port
// await client.ConnectToServer("192.168.2.4", 5000);
await client.ConnectToServer("127.0.0.1", 5000);
// await client.ConnectToServer("10.79.42.144", 5000);


