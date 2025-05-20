using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

class HangmanServer
{
    static string word;
    static char[] guesses;
    static int attempts = 6;
    static List<TcpClient> clients = new List<TcpClient>();
    static object lockObject = new object();

    static void Main()
    {
        try
        {
            // Load words from file
            string[] words = File.ReadAllLines("words.txt");
            if (words.Length == 0)
            {
                Console.WriteLine("No words found in words.txt.");
                return;
            }

            Random random = new Random();
            word = words[random.Next(words.Length)].Trim().ToLower();

            // Initialize guesses
            guesses = new char[word.Length];
            for (int i = 0; i < guesses.Length; i++)
                guesses[i] = '_';

            // Start the TCP server
            Console.WriteLine("Starting the TCP server...");
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 12345);
            tcpListener.Start();
            Console.WriteLine("TCP server started. Waiting for clients...");

            // Start the HTTP server
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://192.168.83.209:8000/");  // Binding to localhost
            httpListener.Start();
            Console.WriteLine("HTTP server started on http://192.168.83.209:8000/");

            // Handle HTTP requests
            Thread httpThread = new Thread(() => HandleHttpRequests(httpListener));
            httpThread.Start();

            // Handle TCP clients
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                lock (lockObject)
                {
                    clients.Add(client);
                }
                Console.WriteLine("Client connected.");
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in server: " + ex.Message);
        }
    }

    static void HandleHttpRequests(HttpListener httpListener)
    {
        while (true)
        {
            try
            {
                var context = httpListener.GetContext();
                var request = context.Request;
                var response = context.Response;

                // Serve the index.html file
                if (request.Url.AbsolutePath == "/")
                {
                    string htmlContent = File.ReadAllText("index.html");
                    byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                    response.ContentType = "text/html";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                // Handle AJAX request to fetch game state
                else if (request.Url.AbsolutePath == "/game-state")
                {
                    string victoryMessage = null;
                    string gameOverMessage = null;

                    // Check if the game is won or lost
                    if (new string(guesses) == word)
                    {
                        victoryMessage = "Congratulations! You guessed the word!";
                    }
                    else if (attempts <= 0)
                    {
                        gameOverMessage = $"Game Over! The correct word was: {word}";
                    }

                    // Serialize the game state
                    string jsonState = JsonSerializer.Serialize(new
                    {
                        wordState = new string(guesses),
                        attempts,
                        hangmanDrawing = GenerateHangmanDrawing(),
                        victoryMessage = victoryMessage,
                        gameOverMessage = gameOverMessage
                    });

                    byte[] buffer = Encoding.UTF8.GetBytes(jsonState);
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }



                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling HTTP request: " + ex.Message);
            }
        }
    }


    static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        string clientAddress = client.Client.RemoteEndPoint.ToString();

        try
        {
            while (true)
            {
                lock (lockObject)
                {
                    // Send game state to client
                    string gameState = GenerateGameStateMessage(GenerateHangmanDrawing());
                    byte[] data = Encoding.UTF8.GetBytes(gameState);
                    stream.Write(data, 0, data.Length);

                    // Receive guess from client
                    byte[] buffer = new byte[256];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string guess = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim().ToLower();

                    Console.WriteLine("Client " + clientAddress + " guessed: " + guess);

                    if (guess.Length == 1 && char.IsLetter(guess[0]))
                    {
                        if (word.Contains(guess))
                        {
                            for (int i = 0; i < word.Length; i++)
                            {
                                if (word[i] == guess[0])
                                    guesses[i] = guess[0];
                            }
                        }
                        else
                        {
                            attempts--;
                        }
                    }

                    if (new string(guesses) == word || attempts <= 0)
                    {
                        BroadcastMessage(new string(guesses) == word
                            ? "You guessed the word! Game Over."
                            : $"Game Over! The word was: {word}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling client " + clientAddress + ": " + ex.Message);
        }
        finally
        {
            lock (lockObject)
            {
                clients.Remove(client);
            }
            client.Close();
            Console.WriteLine("Client " + clientAddress + " disconnected.");
        }
    }

    static void BroadcastMessage(string message)
    {
        lock (lockObject)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (TcpClient client in clients)
            {
                client.GetStream().Write(data, 0, data.Length);
            }
        }
    }
    static string GenerateHangmanDrawing()
    {
        // Draw the hangman based on remaining attempts
        string drawing = "";
        switch (attempts)
        {
            case 5:
                drawing = " O ";
                break;
            case 4:
                drawing = " O \n | ";
                break;
            case 3:
                drawing = " O \n | \n/  ";
                break;
            case 2:
                drawing = " O \n | \n/ \\";
                break;
            case 1:
                drawing = " O \n\\| \n/ \\";
                break;
            case 0:
                drawing = " O \n\\|/ \n/ \\";
                break;
        }
        return drawing;
    }


    static string GenerateGameStateMessage(string hangmanDrawing)
    {
        string wordState = new string(guesses);
        string attemptsLeftMessage = $"Attempts left: {attempts}";

        // Creating the formatted game state message
        return $"Word: {wordState}\n{attemptsLeftMessage}\n\nHangman Drawing:\n{hangmanDrawing}\nEnter your guess: ";
    }

}
