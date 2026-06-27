using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MioneAlarmmelder.Core
{
    public sealed class FirebaseAuthService
    {
        private readonly AppSettings settings;
        private readonly object syncRoot = new object();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private FirebaseAuthSession currentSession;

        public FirebaseAuthService(AppSettings value)
        {
            settings = value;
        }

        public FirebaseAuthSession CurrentSession
        {
            get { lock (syncRoot) { return currentSession; } }
        }

        public void InvalidateCurrentSession()
        {
            ClearCurrentSession();
        }

        public FirebaseAuthSession RestoreSession()
        {
            if (String.IsNullOrEmpty(settings.FirebaseRefreshToken))
            {
                ClearCurrentSession();
                return null;
            }

            FirebaseAuthSession session = RefreshWithToken(settings.FirebaseRefreshToken, true);
            SetCurrentSession(session);
            return session;
        }

        public FirebaseAuthSession RefreshSession()
        {
            if (String.IsNullOrEmpty(settings.FirebaseRefreshToken))
            {
                ClearCurrentSession();
                return null;
            }

            FirebaseAuthSession session = RefreshWithToken(settings.FirebaseRefreshToken, false);
            SetCurrentSession(session);
            return session;
        }

        public FirebaseAuthSession SignInWithEmailPassword(string email, string password)
        {
            string apiKey = EnsureApiKey();
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + Uri.EscapeDataString(apiKey);
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["email"] = email;
            body["password"] = password;
            body["returnSecureToken"] = true;
            Dictionary<string, object> data = PostJson(url, body);
            FirebaseAuthSession session = FromIdentityToolkitResponse(data);
            if (String.IsNullOrEmpty(session.ProviderId)) session.ProviderId = "password";
            if (String.IsNullOrEmpty(session.Email)) session.Email = email;
            SetCurrentSession(session);
            return session;
        }

        public FirebaseAuthSession SignInWithGoogle(string initialEmail)
        {
            return SignInInteractive("google", initialEmail, "");
        }

        public FirebaseAuthSession SignInWithApple(string initialEmail)
        {
            return SignInInteractive("apple", initialEmail, "");
        }

        public FirebaseAuthSession SignInWithPhone(string initialPhone, string initialEmail)
        {
            return SignInInteractive("sms", initialEmail, initialPhone);
        }

        public FirebaseAuthSession SignInWithEmailLink(string initialEmail)
        {
            return SignInInteractive("emailLink", initialEmail, "");
        }

        public FirebaseAuthSession SignInInteractive(string mode, string initialEmail, string initialPhone)
        {
            EnsureLoginConfiguration();

            FirebaseLoginPageConfig config = new FirebaseLoginPageConfig();
            config.ApiKey = GetConfiguredApiKey();
            config.AuthDomain = GetConfiguredAuthDomain();
            config.ProjectId = GetConfiguredProjectId();
            config.InitialEmail = initialEmail ?? String.Empty;
            config.InitialPhone = initialPhone ?? String.Empty;

            using (FirebaseLoginHost host = new FirebaseLoginHost(config))
            {
                FirebaseAuthSession session = host.Run(mode);
                if (session == null) throw new InvalidOperationException("Der Login konnte nicht abgeschlossen werden.");
                SetCurrentSession(session);
                return session;
            }
        }

        public void SignOut()
        {
            settings.FirebaseUid = "";
            settings.FirebaseEmail = "";
            settings.FirebaseDisplayName = "";
            settings.FirebaseProviderId = "";
            settings.FirebasePhoneNumber = "";
            settings.FirebaseRefreshToken = "";
            ClearCurrentSession();
            SettingsStore.Save(settings);
        }

        private void EnsureLoginConfiguration()
        {
            if (String.IsNullOrEmpty(GetConfiguredApiKey())) throw new InvalidOperationException("Firebase API-Konfiguration fehlt.");
            if (String.IsNullOrEmpty(GetConfiguredAuthDomain())) throw new InvalidOperationException("Firebase Auth-Domain fehlt.");
            if (String.IsNullOrEmpty(GetConfiguredProjectId())) throw new InvalidOperationException("Firebase Project ID fehlt.");
        }

        private string EnsureApiKey()
        {
            string apiKey = GetConfiguredApiKey();
            if (String.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("Firebase API-Konfiguration fehlt.");
            return apiKey;
        }

        private string BuildAuthDomain()
        {
            string authDomain = GetConfiguredAuthDomain();
            if (authDomain.Length > 0) return authDomain;
            string projectId = GetConfiguredProjectId();
            if (projectId.Length == 0) return "";
            return projectId + ".firebaseapp.com";
        }

        private string GetConfiguredApiKey()
        {
            string apiKey = (settings.FirebaseApiKey ?? String.Empty).Trim();
            if (apiKey.Length > 0) return apiKey;
            return FirebaseDefaults.ApiKey;
        }

        private string GetConfiguredAuthDomain()
        {
            string authDomain = (settings.FirebaseAuthDomain ?? String.Empty).Trim();
            if (authDomain.Length > 0) return authDomain;
            return FirebaseDefaults.AuthDomain;
        }

        private string GetConfiguredProjectId()
        {
            string projectId = (settings.FirebaseProjectId ?? String.Empty).Trim();
            if (projectId.Length > 0) return projectId;
            return FirebaseDefaults.ProjectId;
        }

        private FirebaseAuthSession RefreshWithToken(string refreshToken, bool preserveProfile)
        {
            string apiKey = EnsureApiKey();
            string url = "https://securetoken.googleapis.com/v1/token?key=" + Uri.EscapeDataString(apiKey);
            string body = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken);

            Dictionary<string, object> data = PostForm(url, body);
            FirebaseAuthSession session = new FirebaseAuthSession();
            session.Uid = GetString(data, "user_id", "localId", "uid");
            session.IdToken = GetString(data, "id_token", "idToken");
            session.RefreshToken = GetString(data, "refresh_token", "refreshToken");
            if (String.IsNullOrEmpty(session.RefreshToken)) session.RefreshToken = refreshToken;
            session.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ClampSeconds(GetInt(data, "expires_in", "expiresIn", 3600)));

            if (preserveProfile)
            {
                session.Email = settings.FirebaseEmail;
                session.DisplayName = settings.FirebaseDisplayName;
                session.ProviderId = settings.FirebaseProviderId;
                session.PhoneNumber = settings.FirebasePhoneNumber;
            }
            else
            {
                FirebaseAuthSession current = CurrentSession;
                if (current != null)
                {
                    session.Email = current.Email;
                    session.DisplayName = current.DisplayName;
                    session.ProviderId = current.ProviderId;
                    session.PhoneNumber = current.PhoneNumber;
                }
            }

            if (String.IsNullOrEmpty(session.ProviderId)) session.ProviderId = settings.FirebaseProviderId;
            if (String.IsNullOrEmpty(session.Email)) session.Email = settings.FirebaseEmail;
            if (String.IsNullOrEmpty(session.DisplayName)) session.DisplayName = settings.FirebaseDisplayName;
            if (String.IsNullOrEmpty(session.PhoneNumber)) session.PhoneNumber = settings.FirebasePhoneNumber;
            if (String.IsNullOrEmpty(session.Uid)) throw new InvalidOperationException("Firebase hat keine System-ID geliefert.");
            return session;
        }

        private FirebaseAuthSession FromIdentityToolkitResponse(Dictionary<string, object> data)
        {
            FirebaseAuthSession session = new FirebaseAuthSession();
            session.Uid = GetString(data, "localId", "user_id", "uid");
            session.Email = GetString(data, "email");
            session.DisplayName = GetString(data, "displayName", "fullName");
            session.ProviderId = GetString(data, "providerId");
            session.PhoneNumber = GetString(data, "phoneNumber");
            session.IdToken = GetString(data, "idToken", "id_token");
            session.RefreshToken = GetString(data, "refreshToken", "refresh_token");
            session.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ClampSeconds(GetInt(data, "expiresIn", "expires_in", 3600)));
            if (String.IsNullOrEmpty(session.Uid)) throw new InvalidOperationException("Firebase hat keine System-ID geliefert.");
            if (String.IsNullOrEmpty(session.RefreshToken)) throw new InvalidOperationException("Firebase hat keinen Refresh Token geliefert.");
            return session;
        }

        private Dictionary<string, object> PostJson(string url, Dictionary<string, object> body)
        {
            string payload = serializer.Serialize(body);
            return PostRequest(url, payload, "application/json");
        }

        private Dictionary<string, object> PostForm(string url, string body)
        {
            return PostRequest(url, body, "application/x-www-form-urlencoded");
        }

        private Dictionary<string, object> PostRequest(string url, string body, string contentType)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = contentType;
            request.Accept = "application/json";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return ParseJson(ReadAll(response.GetResponseStream()));
                }
            }
            catch (WebException ex)
            {
                string message = ReadErrorMessage(ex);
                throw new InvalidOperationException(message, ex);
            }
        }

        private string ReadErrorMessage(WebException ex)
        {
            if (ex.Response == null) return ex.Message;
            try
            {
                string json = ReadAll(ex.Response.GetResponseStream());
                Dictionary<string, object> data = ParseJson(json);
                Dictionary<string, object> error = GetDictionary(data, "error");
                if (error != null)
                {
                    string message = GetString(error, "message");
                    if (!String.IsNullOrEmpty(message)) return FriendlyError(message);
                }
                if (!String.IsNullOrEmpty(json)) return json;
            }
            catch { }
            return ex.Message;
        }

        private string FriendlyError(string code)
        {
            if (String.IsNullOrEmpty(code)) return "Firebase-Anmeldung fehlgeschlagen.";
            if (code.IndexOf("EMAIL_NOT_FOUND", StringComparison.OrdinalIgnoreCase) >= 0) return "Die E-Mail-Adresse ist nicht registriert.";
            if (code.IndexOf("INVALID_PASSWORD", StringComparison.OrdinalIgnoreCase) >= 0) return "Das Passwort ist ungültig.";
            if (code.IndexOf("USER_DISABLED", StringComparison.OrdinalIgnoreCase) >= 0) return "Das Firebase-Konto ist deaktiviert.";
            if (code.IndexOf("INVALID_REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase) >= 0) return "Der gespeicherte Refresh Token ist ungültig. Bitte neu anmelden.";
            if (code.IndexOf("TOKEN_EXPIRED", StringComparison.OrdinalIgnoreCase) >= 0) return "Die Firebase-Session ist abgelaufen. Bitte neu anmelden.";
            if (code.IndexOf("OPERATION_NOT_ALLOWED", StringComparison.OrdinalIgnoreCase) >= 0) return "Die gewünschte Firebase-Anmeldeart ist im Projekt nicht aktiviert.";
            if (code.IndexOf("PROJECT_NUMBER_MISMATCH", StringComparison.OrdinalIgnoreCase) >= 0) return "Die Firebase API Key / Projekt-Zuordnung passt nicht zum Refresh Token.";
            return code.Replace("_", " ");
        }

        private Dictionary<string, object> ParseJson(string json)
        {
            if (String.IsNullOrEmpty(json)) return new Dictionary<string, object>();
            object parsed = serializer.DeserializeObject(json);
            Dictionary<string, object> data = parsed as Dictionary<string, object>;
            if (data != null) return data;
            return new Dictionary<string, object>();
        }

        private static string ReadAll(Stream stream)
        {
            if (stream == null) return "";
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> data, string key)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value)) return null;
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> data, params string[] keys)
        {
            if (data == null) return "";
            for (int i = 0; i < keys.Length; i++)
            {
                object value;
                if (data.TryGetValue(keys[i], out value) && value != null)
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!String.IsNullOrEmpty(text)) return text;
                }
            }
            return "";
        }

        private static int GetInt(Dictionary<string, object> data, string key1, string key2, int fallback)
        {
            string text = GetString(data, key1, key2);
            int value;
            return Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static int ClampSeconds(int value)
        {
            if (value < 1) return 3600;
            if (value > 86400) return 86400;
            return value;
        }

        private void SetCurrentSession(FirebaseAuthSession session)
        {
            lock (syncRoot)
            {
                currentSession = session;
            }
            if (session == null)
            {
                ClearCurrentSession();
                return;
            }
            session.ApplyTo(settings);
            SettingsStore.Save(settings);
        }

        private void ClearCurrentSession()
        {
            lock (syncRoot)
            {
                currentSession = null;
            }
        }
    }

    internal sealed class FirebaseLoginHost : IDisposable
    {
        private readonly FirebaseLoginPageConfig config;
        private readonly int port;
        private readonly string baseUrl;
        private readonly HttpListener listener;
        private readonly ManualResetEvent completion = new ManualResetEvent(false);
        private FirebaseAuthSession session;
        private Exception error;
        private Thread worker;
        private volatile bool stopping;

        public FirebaseLoginHost(FirebaseLoginPageConfig value)
        {
            config = value;
            port = FindFreePort();
            baseUrl = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/";
            listener = new HttpListener();
            listener.Prefixes.Add(baseUrl);
        }

        public FirebaseAuthSession Run(string mode)
        {
            config.Mode = mode ?? "";
            config.CallbackUrl = baseUrl;
            config.EmailLinkUrl = baseUrl + "email-link";

            listener.Start();
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "Firebase Login Host";
            worker.Start();

            string launchUrl = baseUrl + "?mode=" + Uri.EscapeDataString(config.Mode ?? "") +
                               "&email=" + Uri.EscapeDataString(config.InitialEmail ?? "") +
                               "&phone=" + Uri.EscapeDataString(config.InitialPhone ?? "");
            try { Process.Start(launchUrl); }
            catch (Exception ex) { throw new InvalidOperationException("Die Login-Seite konnte nicht geöffnet werden: " + ex.Message, ex); }

            if (!completion.WaitOne(TimeSpan.FromMinutes(20), false))
            {
                throw new TimeoutException("Der Firebase-Login wurde nicht rechtzeitig abgeschlossen.");
            }
            if (error != null) throw error;
            return session;
        }

        private void Loop()
        {
            while (!stopping)
            {
                HttpListenerContext context = null;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    if (!stopping) throw;
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (context != null)
                {
                    try { Handle(context); }
                    catch (Exception ex)
                    {
                        error = ex;
                        completion.Set();
                        TryWriteText(context, 500, "Interner Fehler: " + ex.Message);
                    }
                }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            string path = (context.Request.Url.AbsolutePath ?? "").TrimEnd('/').ToLowerInvariant();
            if (path.Length == 0) path = "/";

            if (context.Request.HttpMethod == "GET" && (path == "/" || path == "/index.html"))
            {
                WriteHtml(context, FirebaseLoginPage.BuildHtml(config));
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/email-link")
            {
                WriteHtml(context, FirebaseLoginPage.BuildHtml(config));
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/session")
            {
                string payload = ReadAll(context.Request.InputStream);
                FirebaseAuthSession value = ParseSession(payload);
                session = value;
                completion.Set();
                TryWriteText(context, 200, "OK");
                StopListener();
                return;
            }

            TryWriteText(context, 404, "Not found");
        }

        private FirebaseAuthSession ParseSession(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object parsed = serializer.DeserializeObject(json);
            Dictionary<string, object> data = parsed as Dictionary<string, object>;
            if (data == null) throw new InvalidOperationException("Ungültige Login-Antwort.");
            FirebaseAuthSession value = new FirebaseAuthSession();
            value.Uid = GetString(data, "uid", "localId", "user_id");
            value.Email = GetString(data, "email");
            value.DisplayName = GetString(data, "displayName");
            value.ProviderId = GetString(data, "providerId");
            value.PhoneNumber = GetString(data, "phoneNumber");
            value.IdToken = GetString(data, "idToken", "id_token");
            value.RefreshToken = GetString(data, "refreshToken", "refresh_token");
            string expires = GetString(data, "expiresIn", "expires_in");
            int expiresSeconds;
            if (!Int32.TryParse(expires, NumberStyles.Integer, CultureInfo.InvariantCulture, out expiresSeconds)) expiresSeconds = 3600;
            value.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ClampSeconds(expiresSeconds));
            if (String.IsNullOrEmpty(value.Uid)) throw new InvalidOperationException("Firebase hat keine System-ID geliefert.");
            if (String.IsNullOrEmpty(value.RefreshToken)) throw new InvalidOperationException("Firebase hat keinen Refresh Token geliefert.");
            return value;
        }

        private static string ReadAll(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string GetString(Dictionary<string, object> data, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                object value;
                if (data.TryGetValue(keys[i], out value) && value != null)
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!String.IsNullOrEmpty(text)) return text;
                }
            }
            return "";
        }

        private static int ClampSeconds(int value)
        {
            if (value < 1) return 3600;
            if (value > 86400) return 86400;
            return value;
        }

        private void WriteHtml(HttpListenerContext context, string html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.Headers["Cache-Control"] = "no-store";
            using (Stream stream = context.Response.OutputStream) { stream.Write(bytes, 0, bytes.Length); }
        }

        private void TryWriteText(HttpListenerContext context, int status, string text)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
                context.Response.StatusCode = status;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                using (Stream stream = context.Response.OutputStream) { stream.Write(bytes, 0, bytes.Length); }
            }
            catch { }
        }

        private void StopListener()
        {
            stopping = true;
            try { if (listener.IsListening) listener.Stop(); } catch { }
        }

        private static int FindFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Dispose()
        {
            stopping = true;
            try { if (listener.IsListening) listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
            try { completion.Set(); } catch { }
            if (worker != null && worker.IsAlive) worker.Join(1500);
            completion.Close();
        }
    }
}
