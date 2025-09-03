using MongoDB.Driver;
using MongoDB.Bson;


namespace Server.Classes
{
        public class Database
        {
                private readonly IMongoCollection<BsonDocument> _users;

                public Database()
                {
                        const string connectionUri = "mongodb://localhost:27017";
                        var client = new MongoClient(connectionUri);
                        var db = client.GetDatabase("tcp-chat");
                        _users = db.GetCollection<BsonDocument>("users");
                }

                public async Task TestConnectionAsync()
                {
                        Console.WriteLine("Tentative de connexion à MongoDB...");
                        try
                        {
                                var result = await _users.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                                Colored("Connexion à MongoDB réussie !\n", ConsoleColor.Green);
                        }
                        catch (Exception ex)
                        {
                                Colored($"Échec de la connexion à MongoDB : {ex.Message}\n", ConsoleColor.Red);
                        }
                }

                public async Task<bool> AddUser(string username, string password)
                {
                        var existing = await _users.Find(new BsonDocument("username", username)).FirstOrDefaultAsync();
                        if (existing != null)
                                return false; // utilisateur déjà existant

                        var doc = new BsonDocument
                        {
                                { "username", username },
                                { "password", password }
                        };
                        await _users.InsertOneAsync(doc);
                        return true;
                }
                public async Task<string?> GetUserId(string username)
                {
                        var user = await _users.Find(new BsonDocument("username", username)).FirstOrDefaultAsync();
                        return user?["_id"]?.ToString();
                }

                public async Task<bool> ConnectUser(string username, string password)
                {
                        var user = await _users.Find(new BsonDocument("username", username)).FirstOrDefaultAsync();
                        if (user == null)
                                return false; //user pas trouvé

                        // vérif mot de passe (on cryptera plus tard)
                        var passwordInDb = user.GetValue("password", "").AsString;
                        return passwordInDb == password;
                }

                public async Task SaveMessage(string? userId, string message)
                {
                        var messages = _users.Database.GetCollection<BsonDocument>("messages");

                        // Récupère le document utilisateur complet
                        var userDoc = await _users.Find(new BsonDocument("_id", new ObjectId(userId))).FirstOrDefaultAsync();

                        var doc = new BsonDocument
                {
                        { "user", userDoc ?? new BsonDocument() }, // Ajoute tout le doc utilisateur
                        { "message", message },
                        { "timestamp", DateTime.UtcNow }
                };
                        await messages.InsertOneAsync(doc);
                }
                public async Task SavePrivateMessage(string senderName, string recipientName, string message)
                {
                        var conversations = _users.Database.GetCollection<BsonDocument>("conversations");
                        var doc = new BsonDocument
                        {
                                { "sender", senderName },
                                { "recipient", recipientName },
                                { "message", message },
                                { "timestamp", DateTime.UtcNow }
                        };
                        await conversations.InsertOneAsync(doc);
                }
                public async Task SetUserConnection(string userId, bool isConnected)
                {
                        var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(userId));
                        var update = Builders<BsonDocument>.Update.Set("isConnected", isConnected);
                        await _users.UpdateOneAsync(filter, update);
                }
                public async Task<List<BsonDocument>> GetConnectedUsers()
                {
                        var filter = Builders<BsonDocument>.Filter.Eq("isConnected", true);
                        return await _users.Find(filter).ToListAsync();
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