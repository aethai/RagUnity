using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Ragnarok.Network {
    /// <summary>
    /// Login through the Custom launcher: when the launcher starts the game it signs the player in on its own and
    /// puts the account name plus a local ticket endpoint in the environment (CUSTOM_LOGIN_ACCOUNT,
    /// CUSTOM_LAUNCHER_TICKET, CUSTOM_LAUNCHER_KEY). Every CA_LOGIN of that account then carries a fresh
    /// one-time token from the launcher instead of a password (the login server's launcher_token plugin accepts it),
    /// so the password never reaches the client. Without those variables nothing here changes anything.
    /// </summary>
    public static class RoLauncherTicket {
        /// <summary>Stands in for the password in the shell while the launcher logs in; never sent.</summary>
        public const string Placeholder = "\u0001custom-launcher";

        private static readonly Regex TokenPattern = new Regex("\"token\"\\s*:\\s*\"(~[A-Za-z0-9]{22})\"", RegexOptions.CultureInvariant);
        private static readonly Regex MessagePattern = new Regex("\"message\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);

        static RoLauncherTicket() {
            Account = Environment.GetEnvironmentVariable("CUSTOM_LOGIN_ACCOUNT");
            Endpoint = Environment.GetEnvironmentVariable("CUSTOM_LAUNCHER_TICKET");
            Key = Environment.GetEnvironmentVariable("CUSTOM_LAUNCHER_KEY");
            Active = !string.IsNullOrWhiteSpace(Account) && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Key);
        }

        public static bool Active { get; }
        public static string Account { get; }
        private static string Endpoint { get; }
        private static string Key { get; }

        /// <summary>The password to put in CA_LOGIN: a fresh launcher token for the placeholder, the password itself otherwise.</summary>
        public static string ResolvePassword(string account, string password) {
            if (!Active || password != Placeholder) {
                return password;
            }

            if (!string.Equals(account, Account, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("The launcher signed in a different account.");
            }

            return Acquire();
        }

        private static string Acquire() {
            int colon = Endpoint.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(Endpoint.Substring(colon + 1), out int port)) {
                throw new InvalidOperationException("The launcher ticket endpoint is invalid.");
            }

            try {
                using (var client = new TcpClient()) {
                    if (!client.ConnectAsync(Endpoint.Substring(0, colon), port).Wait(TimeSpan.FromSeconds(5))) {
                        throw new IOException("timeout");
                    }

                    client.ReceiveTimeout = 20000;
                    client.SendTimeout = 5000;
                    NetworkStream stream = client.GetStream();
                    byte[] request = Encoding.ASCII.GetBytes("FSTICKET " + Key + "\n");
                    stream.Write(request, 0, request.Length);
                    string reply = ReadLine(stream, 4096);
                    Match token = TokenPattern.Match(reply ?? string.Empty);
                    if (token.Success) {
                        return token.Groups[1].Value;
                    }

                    Match message = MessagePattern.Match(reply ?? string.Empty);
                    throw new InvalidOperationException(message.Success ? message.Groups[1].Value : "The launcher did not issue a login ticket.");
                }
            } catch (Exception e) when (e is IOException || e is SocketException || e is AggregateException) {
                throw new InvalidOperationException("Can't reach the Custom launcher. Keep it open while you play, or start the game from it again.", e);
            }
        }

        private static string ReadLine(NetworkStream stream, int max) {
            var bytes = new MemoryStream();
            int b;
            while (bytes.Length < max && (b = stream.ReadByte()) >= 0 && b != '\n') {
                bytes.WriteByte((byte)b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
