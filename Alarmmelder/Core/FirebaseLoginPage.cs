using System;
using System.Text;
using System.Web.Script.Serialization;

namespace MioneAlarmmelder.Core
{
    public sealed class FirebaseLoginPageConfig
    {
        public string ApiKey { get; set; }
        public string AuthDomain { get; set; }
        public string ProjectId { get; set; }
        public string CallbackUrl { get; set; }
        public string EmailLinkUrl { get; set; }
        public string Mode { get; set; }
        public string InitialEmail { get; set; }
        public string InitialPhone { get; set; }
    }

    public static class FirebaseLoginPage
    {
        public static string BuildHtml(FirebaseLoginPageConfig config)
        {
            if (config == null) config = new FirebaseLoginPageConfig();
            string mode = NormalizeMode(config.Mode);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(config);

            StringBuilder sb = new StringBuilder(12000);
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang=\"de\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.AppendLine("<title>Mione Firebase Login</title>");
            sb.AppendLine("<script src=\"https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js\"></script>");
            sb.AppendLine("<script src=\"https://www.gstatic.com/firebasejs/10.13.2/firebase-auth-compat.js\"></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:0;background:linear-gradient(135deg,#eef4ff 0%,#f7fbff 42%,#eff7f2 100%);color:#152033;}");
            sb.AppendLine(".wrap{max-width:980px;margin:0 auto;padding:24px 16px 40px;}");
            sb.AppendLine(".hero{background:#0f2544;color:#fff;border-radius:20px;padding:26px 28px;box-shadow:0 18px 50px rgba(15,37,68,.18);}");
            sb.AppendLine(".hero h1{margin:0 0 8px;font-size:30px;line-height:1.1;}");
            sb.AppendLine(".hero p{margin:0;opacity:.88;max-width:760px;}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px;margin-top:18px;}");
            sb.AppendLine(".card{background:rgba(255,255,255,.96);border:1px solid rgba(17,34,68,.12);border-radius:18px;padding:18px 18px 16px;box-shadow:0 10px 30px rgba(13,35,72,.08);}");
            sb.AppendLine(".card h2{margin:0 0 12px;font-size:18px;}");
            sb.AppendLine(".field{margin-bottom:10px;}");
            sb.AppendLine(".field label{display:block;font-size:12px;font-weight:700;color:#42526b;margin-bottom:4px;}");
            sb.AppendLine(".field input{width:100%;box-sizing:border-box;border:1px solid #c8d3e2;border-radius:12px;padding:11px 12px;font-size:15px;background:#fff;}");
            sb.AppendLine(".row{display:flex;gap:10px;flex-wrap:wrap;}");
            sb.AppendLine("button{border:0;border-radius:12px;padding:11px 14px;font-weight:700;cursor:pointer;transition:transform .12s ease,box-shadow .12s ease;}");
            sb.AppendLine("button:hover{transform:translateY(-1px);box-shadow:0 10px 20px rgba(15,37,68,.12);}");
            sb.AppendLine(".primary{background:#1d4ed8;color:#fff;}");
            sb.AppendLine(".secondary{background:#e6edf8;color:#17324d;}");
            sb.AppendLine(".dark{background:#111827;color:#fff;}");
            sb.AppendLine(".success{background:#d9f7e5;color:#0e5132;}");
            sb.AppendLine(".warn{background:#fff1c2;color:#7a5500;}");
            sb.AppendLine(".danger{background:#ffe0e0;color:#8a1f1f;}");
            sb.AppendLine(".status{margin-top:14px;padding:14px 16px;border-radius:14px;background:#fff;border:1px solid #d8e1ed;white-space:pre-wrap;min-height:70px;}");
            sb.AppendLine(".muted{color:#66788f;font-size:13px;}");
            sb.AppendLine(".note{color:#334155;font-size:13px;line-height:1.45;}");
            sb.AppendLine(".hidden{display:none;}");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"wrap\">");
            AppendHero(sb, mode);
            sb.AppendLine("<div class=\"grid\">");
            AppendModeCard(sb, mode);
            AppendStatusCard(sb, mode);
            AppendNoteCard(sb, mode);
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            AppendScript(sb, json, mode);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private static void AppendHero(StringBuilder sb, string mode)
        {
            sb.AppendLine("<div class=\"hero\">");
            sb.AppendLine("<h1>" + EscapeHtml(TitleForMode(mode)) + "</h1>");
            sb.AppendLine("<p>" + EscapeHtml(DescriptionForMode(mode)) + "</p>");
            sb.AppendLine("</div>");
        }

        private static void AppendModeCard(StringBuilder sb, string mode)
        {
            if (mode == "password")
            {
                sb.AppendLine("<div class=\"card\"><h2>E-Mail / Passwort</h2>");
                sb.AppendLine("<div class=\"field\"><label>E-Mail</label><input id=\"email\" type=\"email\" autocomplete=\"username\" placeholder=\"name@firma.de\"></div>");
                sb.AppendLine("<div class=\"field\"><label>Passwort</label><input id=\"password\" type=\"password\" autocomplete=\"current-password\" placeholder=\"••••••••\"></div>");
                sb.AppendLine("<div class=\"row\"><button class=\"primary\" id=\"btnPassword\">Anmelden</button></div>");
                sb.AppendLine("</div>");
                return;
            }

            if (mode == "google")
            {
                sb.AppendLine("<div class=\"card\"><h2>Google</h2>");
                sb.AppendLine("<p class=\"note\">Die Anmeldung wird direkt zu Google weitergeleitet. Weitere Eingaben sind hier nicht nötig.</p>");
                sb.AppendLine("<div class=\"row\"><button class=\"secondary\" id=\"btnGoogle\">Mit Google anmelden</button></div>");
                sb.AppendLine("</div>");
                return;
            }

            if (mode == "apple")
            {
                sb.AppendLine("<div class=\"card\"><h2>Apple</h2>");
                sb.AppendLine("<p class=\"note\">Die Anmeldung wird direkt zu Apple weitergeleitet. Weitere Eingaben sind hier nicht nötig.</p>");
                sb.AppendLine("<div class=\"row\"><button class=\"dark\" id=\"btnApple\">Mit Apple anmelden</button></div>");
                sb.AppendLine("</div>");
                return;
            }

            if (mode == "sms")
            {
                sb.AppendLine("<div class=\"card\"><h2>SMS</h2>");
                sb.AppendLine("<div class=\"field\"><label>Telefonnummer</label><input id=\"phone\" type=\"tel\" autocomplete=\"tel\" placeholder=\"+49...\"></div>");
                sb.AppendLine("<div class=\"row\"><button class=\"success\" id=\"btnSendSms\">SMS-Code senden</button></div>");
                sb.AppendLine("<div id=\"smsStep2\" class=\"hidden\">");
                sb.AppendLine("<div class=\"field\"><label>SMS-Code</label><input id=\"smsCode\" type=\"text\" inputmode=\"numeric\" placeholder=\"123456\"></div>");
                sb.AppendLine("<div class=\"row\"><button class=\"secondary\" id=\"btnVerifySms\">SMS-Code prüfen</button></div>");
                sb.AppendLine("</div>");
                sb.AppendLine("<div id=\"recaptcha\" style=\"margin-top:10px;\"></div>");
                sb.AppendLine("</div>");
                return;
            }

            if (mode == "emaillink")
            {
                sb.AppendLine("<div class=\"card\"><h2>E-Mail-Link</h2>");
                sb.AppendLine("<div class=\"field\"><label>E-Mail</label><input id=\"emailLink\" type=\"email\" autocomplete=\"username\" placeholder=\"name@firma.de\"></div>");
                sb.AppendLine("<div class=\"row\"><button class=\"warn\" id=\"btnSendLink\">Login-Link senden</button></div>");
                sb.AppendLine("<p class=\"note\">Der Login-Link führt zurück auf diese Seite und wird dort automatisch abgeschlossen.</p>");
                sb.AppendLine("</div>");
                return;
            }

            sb.AppendLine("<div class=\"card\"><h2>Login</h2>");
            sb.AppendLine("<p class=\"note\">Bitte den Login-Modus erneut starten.</p>");
            sb.AppendLine("</div>");
        }

        private static void AppendStatusCard(StringBuilder sb, string mode)
        {
            sb.AppendLine("<div class=\"card\"><h2>Status</h2>");
            sb.AppendLine("<div class=\"muted\">Modus: " + EscapeHtml(ModeLabel(mode)) + "</div>");
            sb.AppendLine("<div class=\"status\" id=\"status\">Bereit.</div>");
            sb.AppendLine("</div>");
        }

        private static void AppendNoteCard(StringBuilder sb, string mode)
        {
            sb.AppendLine("<div class=\"card\"><h2>Hinweis</h2>");
            sb.AppendLine("<p class=\"note\">" + EscapeHtml(NoteForMode(mode)) + "</p>");
            sb.AppendLine("</div>");
        }

        private static void AppendScript(StringBuilder sb, string json, string mode)
        {
            sb.AppendLine("<script>");
            sb.AppendLine("const config = " + json + ";");
            sb.AppendLine("const mode = (config.Mode || '').toLowerCase();");
            sb.AppendLine("const authConfig = { apiKey: config.ApiKey, authDomain: config.AuthDomain, projectId: config.ProjectId };");
            sb.AppendLine("firebase.initializeApp(authConfig);");
            sb.AppendLine("const auth = firebase.auth();");
            sb.AppendLine("auth.setPersistence(firebase.auth.Auth.Persistence.LOCAL).catch(() => {});");
            sb.AppendLine("let confirmationResult = null;");
            sb.AppendLine("let recaptchaVerifier = null;");
            sb.AppendLine("function el(id){ return document.getElementById(id); }");
            sb.AppendLine("function status(text, kind){ const node = el('status'); if (!node) return; node.textContent = text; node.className = 'status ' + (kind || ''); }");
            sb.AppendLine("function show(id, visible){ const node = el(id); if (!node) return; node.className = visible ? '' : 'hidden'; }");
            sb.AppendLine("function focusFirst(ids){ for (let i = 0; i < ids.length; i++) { const node = el(ids[i]); if (node) { node.focus(); return; } } }");
            sb.AppendLine("function providerIdFor(user){ try { if (user && user.providerData && user.providerData.length) return user.providerData[0].providerId || ''; } catch(e) {} return ''; }");
            sb.AppendLine("function refreshTokenFor(user){ try { if (user && user.refreshToken) return user.refreshToken; if (user && user.stsTokenManager && user.stsTokenManager.refreshToken) return user.stsTokenManager.refreshToken; } catch(e) {} return ''; }");
            sb.AppendLine("async function postSession(user, providerId){");
            sb.AppendLine("  if (!user) throw new Error('Keine Firebase-Session vorhanden.');");
            sb.AppendLine("  const payload = {");
            sb.AppendLine("    uid: user.uid || '',");
            sb.AppendLine("    email: user.email || '',");
            sb.AppendLine("    displayName: user.displayName || '',");
            sb.AppendLine("    providerId: providerId || providerIdFor(user) || '',");
            sb.AppendLine("    phoneNumber: user.phoneNumber || '',");
            sb.AppendLine("    idToken: await user.getIdToken(true),");
            sb.AppendLine("    refreshToken: refreshTokenFor(user)");
            sb.AppendLine("  };");
            sb.AppendLine("  await fetch(config.CallbackUrl + 'session', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });");
            sb.AppendLine("  status('Login erfolgreich. Du kannst dieses Fenster schließen.', 'success');");
            sb.AppendLine("}");
            sb.AppendLine("async function handleRedirect(){");
            sb.AppendLine("  try {");
            sb.AppendLine("    const result = await auth.getRedirectResult();");
            sb.AppendLine("    if (result && result.user) { await postSession(result.user, providerIdFor(result.user)); return true; }");
            sb.AppendLine("  } catch (e) { status((e && e.message) ? e.message : String(e), 'danger'); }");
            sb.AppendLine("  return false;");
            sb.AppendLine("}");
            sb.AppendLine("async function signInPassword(){");
            sb.AppendLine("  status('Melde mit E-Mail/Passwort an ...', 'warn');");
            sb.AppendLine("  const email = el('email').value.trim();");
            sb.AppendLine("  const password = el('password').value;");
            sb.AppendLine("  const result = await auth.signInWithEmailAndPassword(email, password);");
            sb.AppendLine("  await postSession(result.user, 'password');");
            sb.AppendLine("}");
            sb.AppendLine("function startRedirect(provider){ status('Weiterleitung ...', 'warn'); auth.signInWithRedirect(provider); }");
            sb.AppendLine("async function signInGoogle(){ const p = new firebase.auth.GoogleAuthProvider(); p.addScope('email'); startRedirect(p); }");
            sb.AppendLine("async function signInApple(){ const p = new firebase.auth.OAuthProvider('apple.com'); p.addScope('email'); p.addScope('name'); startRedirect(p); }");
            sb.AppendLine("async function prepareRecaptcha(){ if (!recaptchaVerifier) { recaptchaVerifier = new firebase.auth.RecaptchaVerifier('recaptcha', { size: 'invisible' }, auth); await recaptchaVerifier.render(); } }");
            sb.AppendLine("async function sendSms(){");
            sb.AppendLine("  const phone = el('phone').value.trim();");
            sb.AppendLine("  if (!phone) throw new Error('Bitte eine Telefonnummer eingeben.');");
            sb.AppendLine("  status('SMS wird gesendet ...', 'warn');");
            sb.AppendLine("  await prepareRecaptcha();");
            sb.AppendLine("  confirmationResult = await auth.signInWithPhoneNumber(phone, recaptchaVerifier);");
            sb.AppendLine("  show('smsStep2', true);");
            sb.AppendLine("  status('SMS gesendet. Bitte den Code eingeben und auf \"SMS-Code prüfen\" klicken.', 'success');");
            sb.AppendLine("  focusFirst(['smsCode']);");
            sb.AppendLine("}");
            sb.AppendLine("async function verifySms(){");
            sb.AppendLine("  if (!confirmationResult) throw new Error('Bitte zuerst den SMS-Code senden.');");
            sb.AppendLine("  const code = el('smsCode').value.trim();");
            sb.AppendLine("  if (!code) throw new Error('Bitte den SMS-Code eingeben.');");
            sb.AppendLine("  status('SMS-Code wird geprüft ...', 'warn');");
            sb.AppendLine("  const result = await confirmationResult.confirm(code);");
            sb.AppendLine("  await postSession(result.user, 'phone');");
            sb.AppendLine("}");
            sb.AppendLine("async function sendEmailLink(){");
            sb.AppendLine("  const email = el('emailLink').value.trim();");
            sb.AppendLine("  if (!email) throw new Error('Bitte eine E-Mail-Adresse eingeben.');");
            sb.AppendLine("  localStorage.setItem('mione.firebase.email', email);");
            sb.AppendLine("  const actionCodeSettings = { url: config.EmailLinkUrl, handleCodeInApp: true };");
            sb.AppendLine("  status('E-Mail-Link wird gesendet ...', 'warn');");
            sb.AppendLine("  await auth.sendSignInLinkToEmail(email, actionCodeSettings);");
            sb.AppendLine("  status('Link gesendet. Bitte die E-Mail öffnen und den Login-Link anklicken.', 'success');");
            sb.AppendLine("}");
            sb.AppendLine("async function handleEmailLink(){");
            sb.AppendLine("  try {");
            sb.AppendLine("    if (!auth.isSignInWithEmailLink(window.location.href)) return false;");
            sb.AppendLine("    let email = localStorage.getItem('mione.firebase.email') || el('emailLink').value.trim();");
            sb.AppendLine("    if (!email) email = window.prompt('Bitte die E-Mail-Adresse für den Login-Link eingeben:') || '';");
            sb.AppendLine("    if (!email) throw new Error('Keine E-Mail-Adresse angegeben.');");
            sb.AppendLine("    status('E-Mail-Link wird bestätigt ...', 'warn');");
            sb.AppendLine("    const result = await auth.signInWithEmailLink(email, window.location.href);");
            sb.AppendLine("    localStorage.removeItem('mione.firebase.email');");
            sb.AppendLine("    await postSession(result.user, 'emailLink');");
            sb.AppendLine("    return true;");
            sb.AppendLine("  } catch (e) { status((e && e.message) ? e.message : String(e), 'danger'); }");
            sb.AppendLine("  return false;");
            sb.AppendLine("}");
            sb.AppendLine("function applyModeHints(){");
            sb.AppendLine("  if (mode === 'google') { status('Google-Anmeldung wird gestartet ...', 'warn'); setTimeout(function(){ signInGoogle().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); }, 50); return; }");
            sb.AppendLine("  if (mode === 'apple') { status('Apple-Anmeldung wird gestartet ...', 'warn'); setTimeout(function(){ signInApple().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); }, 50); return; }");
            sb.AppendLine("  if (mode === 'password') { status('Bitte E-Mail und Passwort eingeben.', 'warn'); focusFirst(['email']); return; }");
            sb.AppendLine("  if (mode === 'sms') { status('Bitte Telefonnummer eingeben und SMS-Code senden.', 'warn'); focusFirst(['phone']); return; }");
            sb.AppendLine("  if (mode === 'emaillink') { status('Bitte E-Mail eingeben und den Login-Link senden.', 'warn'); focusFirst(['emailLink']); return; }");
            sb.AppendLine("}");
            sb.AppendLine("async function init(){");
            sb.AppendLine("  const redirected = await handleRedirect();");
            sb.AppendLine("  const emailLinked = await handleEmailLink();");
            sb.AppendLine("  if (redirected || emailLinked) return;");
            sb.AppendLine("  applyModeHints();");
            sb.AppendLine("}");
            sb.AppendLine("const passwordBtn = el('btnPassword'); if (passwordBtn) passwordBtn.addEventListener('click', function(){ signInPassword().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("const googleBtn = el('btnGoogle'); if (googleBtn) googleBtn.addEventListener('click', function(){ signInGoogle().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("const appleBtn = el('btnApple'); if (appleBtn) appleBtn.addEventListener('click', function(){ signInApple().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("const sendSmsBtn = el('btnSendSms'); if (sendSmsBtn) sendSmsBtn.addEventListener('click', function(){ sendSms().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("const verifySmsBtn = el('btnVerifySms'); if (verifySmsBtn) verifySmsBtn.addEventListener('click', function(){ verifySms().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("const sendLinkBtn = el('btnSendLink'); if (sendLinkBtn) sendLinkBtn.addEventListener('click', function(){ sendEmailLink().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("window.addEventListener('load', init);");
            sb.AppendLine("</script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
        }

        private static string NormalizeMode(string value)
        {
            return (value ?? String.Empty).Trim().ToLowerInvariant();
        }

        private static string TitleForMode(string mode)
        {
            if (mode == "password") return "Mione Login - E-Mail / Passwort";
            if (mode == "google") return "Mione Login - Google";
            if (mode == "apple") return "Mione Login - Apple";
            if (mode == "sms") return "Mione Login - SMS";
            if (mode == "emaillink") return "Mione Login - E-Mail-Link";
            return "Mione Firebase Login";
        }

        private static string DescriptionForMode(string mode)
        {
            if (mode == "password") return "Bitte E-Mail und Passwort eingeben. Andere Felder werden in diesem Modus nicht angezeigt.";
            if (mode == "google") return "Die Anmeldung startet direkt über Google. Es sind keine Eingabefelder erforderlich.";
            if (mode == "apple") return "Die Anmeldung startet direkt über Apple. Es sind keine Eingabefelder erforderlich.";
            if (mode == "sms") return "Zuerst die Telefonnummer eingeben. Nach dem Senden des Codes wird das Eingabefeld für die Bestätigung eingeblendet.";
            if (mode == "emaillink") return "Nur die E-Mail-Adresse ist nötig. Der Link öffnet diese Seite erneut und schließt die Anmeldung ab.";
            return "Die Anmeldung wird über Firebase Authentication durchgeführt.";
        }

        private static string ModeLabel(string mode)
        {
            if (mode == "password") return "E-Mail / Passwort";
            if (mode == "google") return "Google";
            if (mode == "apple") return "Apple";
            if (mode == "sms") return "SMS";
            if (mode == "emaillink") return "E-Mail-Link";
            return "Unbekannt";
        }

        private static string NoteForMode(string mode)
        {
            if (mode == "password") return "Nur die Anmeldung per E-Mail und Passwort wird angezeigt.";
            if (mode == "google") return "Die Seite zeigt nur den Google-Login-Button.";
            if (mode == "apple") return "Die Seite zeigt nur den Apple-Login-Button.";
            if (mode == "sms") return "Nach dem Senden der SMS wird das Codefeld sichtbar.";
            if (mode == "emaillink") return "Die Anmeldung wird über den in der E-Mail enthaltenen Link abgeschlossen.";
            return "Es wird nur der für den jeweiligen Login benötigte Bereich angezeigt.";
        }

        private static string EscapeHtml(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
