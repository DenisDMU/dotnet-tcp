# TCP Chat Application

A real-time console-based chat application built with .NET using TCP sockets for reliable client-server communication.

## Overview

This project implements a multi-user chat system with both public and private messaging capabilities. It consists of two applications:

- **Server:** Handles client connections, authentication, and message broadcasting.
- **Client:** Provides a user-friendly console interface for sending and receiving messages.

The application uses raw TCP socket communication with a simple text-based protocol enhanced with JSON for structured message exchange.

## Features

- **User Authentication:** Login with username and password.
- **Account Creation:** New users are automatically registered on first login.
- **Private Messaging:** Send private messages to specific users with `/msg <username> <message>`.
- **User Listing:** View all currently connected users with the `list` command.
- **Command Help:** Display available commands with `--help`.
- **Persistent Storage:** All messages and user accounts are stored in MongoDB.
- **Timestamp Display:** All messages include timestamps for better conversation tracking.

## Architecture

### Server Components

- **TcpServer:** Core server class that handles client connections.
- **Database:** MongoDB integration for user management and message persistence.
- **Broadcast:** Handles message distribution to connected clients.
- **Help:** Provides command documentation to users.

### Client Components

- **TcpClientApp:** Main client class with UI handling and server communication.
- **Console Input Management:** Custom implementation for preserving user input when receiving messages.

### Communication Protocol

The application uses a JSON-based protocol for message exchange:

```json
{
  "type": "public_message",
  "sender": "username",
  "content": "message text",
  "timestamp": "10:15:30"
}
```

**Message types include:**

- `public_message`: Broadcast to all users.
- `private_message`: Sent to a specific user.
- `private_confirmation`: Confirmation of a sent private message.
- `user_list`: List of online users.
- `error`: Error messages.
- `help`: Command documentation.

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- MongoDB instance (local or Atlas)

### Configuration

1. Configure the MongoDB connection in your settings file (rename database.exemple.json to database.json):

    ```json
    {
      "ConnectionString": "mongodb+srv://your-connection-string",
      "DatabaseName": "tcp-chat"
    }
    ```

2. **Run the server:**

    ```bash
    cd Server
    dotnet run
    ```

3. **Run the client:**

    ```bash
    cd Client
    dotnet run
    ```

Connect to the server by providing the server IP and port (default: 127.0.0.1:5000) inside Program.cs in the Client directory.

## Usage

### Basic Commands

- **Send a public message:** Just type your message and press Enter.
- **Send a private message:** `/msg <username> <message>`
- **List online users:** `list`
- **Show available commands:** `--help`
- **Exit the application:** `exit`

### Authentication

The application provides a simple authentication system:

- If the username doesn't exist, a new account is created.
- If the username exists, the password is validated.
- Multiple login attempts are supported without closing the connection.

## Technical Details

- **Thread Safety:** Console output is protected with locks to prevent corruption.
- **Async/Await:** Fully asynchronous communication for better performance.
- **Error Handling:** Robust error handling for network issues and invalid inputs.
- **Connection Management:** Graceful handling of client disconnections.
- **Prompt Preservation:** Client UI maintains user input when receiving messages.

---
