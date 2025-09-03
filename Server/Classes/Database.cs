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

                public async Task ConnectDatabase()
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

                        // Récupère les documents complets des deux utilisateurs
                        var senderDoc = await _users.Find(new BsonDocument("username", senderName)).FirstOrDefaultAsync();
                        var recipientDoc = await _users.Find(new BsonDocument("username", recipientName)).FirstOrDefaultAsync();

                        // Crée un filtre pour trouver une conversation existante entre les deux utilisateurs (par id)
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.And(
                                Builders<BsonDocument>.Filter.Eq("participants.0._id", senderDoc["_id"]),
                                Builders<BsonDocument>.Filter.Eq("participants.1._id", recipientDoc["_id"])
                            ),
                            Builders<BsonDocument>.Filter.And(
                                Builders<BsonDocument>.Filter.Eq("participants.0._id", recipientDoc["_id"]),
                                Builders<BsonDocument>.Filter.Eq("participants.1._id", senderDoc["_id"])
                            )
                        );

                        // Cherche une conversation existante
                        var conversation = await conversations.Find(filter).FirstOrDefaultAsync();

                        // Crée un nouveau document de message
                        var messageDoc = new BsonDocument
                        {
                                { "sender", senderName },
                                { "content", message },
                                { "timestamp", DateTime.UtcNow }
                        };

                        if (conversation == null)
                        {
                                // Crée une nouvelle conversation avec les documents complets
                                conversation = new BsonDocument
                                {
                                { "participants", new BsonArray { senderDoc, recipientDoc } },
                                { "messages", new BsonArray { messageDoc } }
                                };
                                await conversations.InsertOneAsync(conversation);
                        }
                        else
                        {
                                // Ajoute le message à la conversation existante
                                var update = Builders<BsonDocument>.Update.Push("messages", messageDoc);
                                await conversations.UpdateOneAsync(filter, update);
                        }
                }
                public async Task<List<BsonDocument>> GetPrivateMessages(string user1, string user2)
                {
                        var conversations = _users.Database.GetCollection<BsonDocument>("conversations");

                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.And(
                                Builders<BsonDocument>.Filter.Eq("participants.0", user1),
                                Builders<BsonDocument>.Filter.Eq("participants.1", user2)
                            ),
                            Builders<BsonDocument>.Filter.And(
                                Builders<BsonDocument>.Filter.Eq("participants.0", user2),
                                Builders<BsonDocument>.Filter.Eq("participants.1", user1)
                            )
                        );

                        var conversation = await conversations.Find(filter).FirstOrDefaultAsync();

                        if (conversation != null && conversation.Contains("messages"))
                        {
                                // Convertit chaque BsonValue en BsonDocument
                                return conversation["messages"].AsBsonArray
                                    .Select(m => m.AsBsonDocument)
                                    .ToList();
                        }

                        return new List<BsonDocument>();
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