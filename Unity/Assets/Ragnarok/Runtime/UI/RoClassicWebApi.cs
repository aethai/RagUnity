using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The client's web API (the Hercules api-server): multipart POSTs to AssistAddr + path (0x593500 / CEmblemDataMgr
    /// 0x5aeeb0), answered with JSON (Type 2 / 3 = failure) or, for an emblem download, the raw image. AssistAddr is the Lua
    /// global of lua files\service_usa\externalsettings_usa.lub; this port takes RO_ASSIST_ADDR
    /// first, and for a login server on a loopback / private address (the lab) that host's port 7121. Every request carries
    /// AID, WorldName and AuthToken (AC_ACCEPT_LOGIN 0x0ac4's token); answers come back on the main thread.
    /// </summary>
    public sealed class RoClassicWebApi : IRoClassicUiProducer {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly ConcurrentQueue<Action> Done = new ConcurrentQueue<Action>();
        private static RoClassicWebApi instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public void ClassicUiTick(RoClassicUiSystem ui) {
            while (Done.TryDequeue(out Action action)) {
                try {
                    action();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        private static void Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || instance != null) return;
            instance = new RoClassicWebApi();
            system.Register(instance);
        }

        /// <summary>0xce8280: the web API base, "" when there is none (no request is made then).</summary>
        public static string BaseAddress(RoLoginShellSession session) {
            string env = Environment.GetEnvironmentVariable("RO_ASSIST_ADDR");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            string host = session?.LoginHost ?? string.Empty;
            if (IsLocal(host)) return host + ":7121";
            return string.Empty;
        }

        private static bool IsLocal(string host) {
            if (string.IsNullOrWhiteSpace(host)) return false;
            if (host == "localhost") return true;
            if (!IPAddress.TryParse(host, out IPAddress ip)) return false;
            byte[] b = ip.GetAddressBytes();
            return b.Length == 4 && (b[0] == 127 || b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] < 32));
        }

        /// <summary>True when a request can go out: an address and a login token.</summary>
        public static bool Available(RoLoginShellSession session) =>
            session != null && !string.IsNullOrEmpty(session.WebAuthToken) && BaseAddress(session).Length > 0;

        /// <summary>The common fields: AID, GID (when <paramref name="withGid"/>), WorldName, AuthToken.</summary>
        public static List<(string Name, string Value)> Common(RoLoginShellSession session, bool withGid) {
            (uint charId, uint accountId) = session.LocalCharacterIds();
            var fields = new List<(string, string)> { ("AID", accountId.ToString(CultureInfo.InvariantCulture)) };
            if (withGid) fields.Add(("GID", charId.ToString(CultureInfo.InvariantCulture)));
            fields.Add(("WorldName", session.SelectedServerName ?? string.Empty));
            fields.Add(("AuthToken", session.WebAuthToken ?? string.Empty));
            return fields;
        }

        /// <summary>
        /// POSTs the fields (and an optional file part) to base + <paramref name="path"/>; <paramref name="done"/> gets the
        /// HTTP success and the body bytes on the main thread. False when the request was not made.
        /// </summary>
        public static bool Post(RoLoginShellSession session, string path, List<(string Name, string Value)> fields,
            (string Name, string FileName, string ContentType, byte[] Data)? file, Action<bool, byte[]> done) {
            string address = BaseAddress(session);
            if (address.Length == 0) return false;
            Ensure();
            string url = (address.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? address : "http://" + address) + path;
            ByteArrayContent content = Multipart(fields, file);

            Task.Run(async () => {
                bool ok = false;
                byte[] body = Array.Empty<byte>();
                try {
                    using (HttpResponseMessage response = await Http.PostAsync(url, content).ConfigureAwait(false)) {
                        ok = response.IsSuccessStatusCode;
                        body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                } catch (Exception e) {
                    Debug.Log("[webapi] " + path + " failed: " + e.Message);
                } finally {
                    content.Dispose();
                }

                Done.Enqueue(() => done?.Invoke(ok, body));
            });
            return true;
        }

        /// <summary>
        /// The multipart body as libcurl writes it (the api-server's parser, aclif.c 0x1b0: an unquoted boundary and quoted
        /// part names, which .NET's MultipartFormDataContent does not produce).
        /// </summary>
        private static ByteArrayContent Multipart(List<(string Name, string Value)> fields, (string Name, string FileName, string ContentType, byte[] Data)? file) {
            string boundary = "------------------------" + Guid.NewGuid().ToString("N").Substring(0, 16);
            var body = new System.IO.MemoryStream();
            void Write(string text) {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                body.Write(bytes, 0, bytes.Length);
            }

            foreach ((string name, string value) in fields) {
                Write("--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + (value ?? string.Empty) + "\r\n");
            }

            if (file.HasValue) {
                Write("--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + file.Value.Name + "\"; filename=\"" + file.Value.FileName +
                    "\"\r\nContent-Type: " + file.Value.ContentType + "\r\n\r\n");
                body.Write(file.Value.Data, 0, file.Value.Data.Length);
                Write("\r\n");
            }

            Write("--" + boundary + "--\r\n");
            var content = new ByteArrayContent(body.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);
            return content;
        }

        /// <summary>The JSON answer, null when it is not JSON.</summary>
        public static JObject Json(byte[] body) {
            try {
                return body != null && body.Length > 0 ? JObject.Parse(System.Text.Encoding.UTF8.GetString(body)) : null;
            } catch (Newtonsoft.Json.JsonException) {
                return null;
            }
        }

        /// <summary>Every endpoint: Type 2 or 3 is a failure, anything else (a missing Type too) a success.</summary>
        public static bool Succeeded(bool ok, JObject json) {
            if (!ok || json == null) return false;
            int type = json.Value<int?>("Type") ?? 1;
            return type != 2 && type != 3;
        }
    }
}
