using Server.Classes;

Database db = new Database();
await db.ConnectDatabase();

TcpServer server = new();
await server.StartServer();



