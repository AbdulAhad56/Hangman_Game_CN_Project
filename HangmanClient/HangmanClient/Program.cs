using System;
using System.Net.Sockets;
using System.Text;

class HangmanClient
{
    static void Main()
    {
        Console.WriteLine("Enter the server IP address: ");
        string serverIp = Console.ReadLine();

        try
        {
            TcpClient client = new TcpClient();
            Console.WriteLine("Connecting to the server...");
            client.Connect(serverIp, 12345);
            Console.WriteLine("Connected to the server!");

            NetworkStream stream = client.GetStream();

            while (true)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string serverMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine(serverMessage);

                if (serverMessage.Contains("Game Over"))
                {
                    Console.WriteLine("Game over! Would you like to play again? (yes/no)");
                    string playAgain = Console.ReadLine().Trim().ToLower();
                    if (playAgain == "no")
                    {
                        break;  // Exit the loop and terminate the program
                    }
                    else
                    {
                        // If they choose to play again, you can reinitialize the game state on the server side
                        // For now, we simply reconnect to the server again to restart the game
                        client.Close();
                        client = new TcpClient();
                        Console.WriteLine("Reconnecting to the server...");
                        client.Connect(serverIp, 12345);
                        Console.WriteLine("Connected to the server!");
                        stream = client.GetStream();
                    }
                }

                string guess = Console.ReadLine().Trim();
                byte[] data = Encoding.UTF8.GetBytes(guess);
                stream.Write(data, 0, data.Length);
            }

            client.Close();
            Console.WriteLine("Disconnected from server.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
