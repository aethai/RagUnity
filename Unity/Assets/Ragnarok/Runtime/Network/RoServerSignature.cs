using System;
using System.Security.Cryptography;
using System.Text;

namespace Ragnarok.Network {
    /// <summary>Optional server challenge verification.</summary>
    public static class RoServerSignature {
        public const ushort RequestId = 0x0c80;
        public const ushort ReplyId = 0x0c81;
        public const int ChallengeLength = 32;
        public const int SignatureLength = 256;
        public const int RequestLength = 2 + ChallengeLength;
        public const int ReplyLength = 2 + SignatureLength;
        public const string RefusedMessage = "Server signature verification failed.";

        private const string Exponent = "AQAB";

        private static readonly string RequiredModulus = Environment.GetEnvironmentVariable("RO_SERVER_RSA_MODULUS");

        private static readonly byte[] Domain = Encoding.ASCII.GetBytes("RO login signature v1");

        /// <summary>Whether this build checks the server before logging in.</summary>
        public static bool Required => !string.IsNullOrWhiteSpace(RequiredModulus);

        public static byte[] NewChallenge() {
            var challenge = new byte[ChallengeLength];
            using (var random = RandomNumberGenerator.Create()) {
                random.GetBytes(challenge);
            }

            return challenge;
        }

        public static byte[] BuildRequest(byte[] challenge) {
            if (challenge == null || challenge.Length != ChallengeLength) {
                throw new ArgumentException("A challenge is " + ChallengeLength + " bytes.", nameof(challenge));
            }

            var request = new byte[RequestLength];
            request[0] = unchecked((byte) RequestId);
            request[1] = (byte) (RequestId >> 8);
            Buffer.BlockCopy(challenge, 0, request, 2, ChallengeLength);
            return request;
        }

        /// <summary>Throws <see cref="RefusedMessage"/> unless <paramref name="reply"/> signs the challenge with this build's key.</summary>
        public static void Check(byte[] challenge, byte[] reply) {
            if (!Verify(challenge, reply, RequiredModulus)) {
                throw new RoNetworkProtocolException(RefusedMessage);
            }
        }

        /// <summary>True when <paramref name="reply"/> (the whole AC_CUSTOM_SIGN_ACK) signs the challenge under that modulus.</summary>
        public static bool Verify(byte[] challenge, byte[] reply, string modulus) {
            if (string.IsNullOrEmpty(modulus) || challenge == null || challenge.Length != ChallengeLength ||
                reply == null || reply.Length < ReplyLength || (reply[0] | (reply[1] << 8)) != ReplyId) {
                return false;
            }

            var signature = new byte[SignatureLength];
            Buffer.BlockCopy(reply, 2, signature, 0, SignatureLength);
            var message = new byte[Domain.Length + ChallengeLength];
            Buffer.BlockCopy(Domain, 0, message, 0, Domain.Length);
            Buffer.BlockCopy(challenge, 0, message, Domain.Length, ChallengeLength);
            try {
                using (RSA rsa = RSA.Create()) {
                    rsa.ImportParameters(new RSAParameters {
                        Modulus = Convert.FromBase64String(modulus),
                        Exponent = Convert.FromBase64String(Exponent)
                    });
                    return rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            } catch (CryptographicException) {
                return false;
            }
        }
    }
}
