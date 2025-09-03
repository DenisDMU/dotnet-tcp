using Server.Classes;

Database db = new Database();
await db.TestConnectionAsync();

TcpServer server = new();
await server.StartServer();



