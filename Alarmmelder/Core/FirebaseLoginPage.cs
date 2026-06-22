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
            sb.AppendLine(".mono{font-family:Consolas,Menlo,monospace;font-size:13px;word-break:break-all;}");
            sb.AppendLine(".split{display:grid;grid-template-columns:1fr 1fr;gap:12px;}");
            sb.AppendLine("@media (max-width:700px){.split{grid-template-columns:1fr;}}");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"wrap\">");
            sb.AppendLine("<div class=\"hero\">");
            sb.AppendLine("<h1>Mione Firebase Login</h1>");
            sb.AppendLine("<p>Email/Passwort, Google, Apple, SMS und E-Mail-Link werden hier über Firebase Authentication abgewickelt. Nach erfolgreichem Login übergibt diese Seite die Session an die Windows-Forms-App.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"grid\">");
            sb.AppendLine("<div class=\"card\"><h2>Direktlogin</h2>");
            sb.AppendLine("<div class=\"field\"><label>E-Mail</label><input id=\"email\" type=\"email\" autocomplete=\"username\" placeholder=\"name@firma.de\"></div>");
            sb.AppendLine("<div class=\"field\"><label>Passwort</label><input id=\"password\" type=\"password\" autocomplete=\"current-password\" placeholder=\"••••••••\"></div>");
            sb.AppendLine("<div class=\"row\"><button class=\"primary\" id=\"btnPassword\">E-Mail / Passwort anmelden</button><button class=\"secondary\" id=\"btnGoogle\">Anmelden mit Google</button><button class=\"dark\" id=\"btnApple\">Anmelden mit Apple</button></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"card\"><h2>SMS / Code</h2>");
            sb.AppendLine("<div class=\"field\"><label>Telefonnummer</label><input id=\"phone\" type=\"tel\" autocomplete=\"tel\" placeholder=\"+49...\"></div>");
            sb.AppendLine("<div class=\"field\"><label>SMS-Code</label><input id=\"smsCode\" type=\"text\" inputmode=\"numeric\" placeholder=\"123456\"></div>");
            sb.AppendLine("<div class=\"row\"><button class=\"success\" id=\"btnSendSms\">SMS-Code senden</button><button class=\"secondary\" id=\"btnVerifySms\">SMS-Code prüfen</button></div>");
            sb.AppendLine("<div id=\"recaptcha\" style=\"margin-top:10px;\"></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"card\"><h2>E-Mail-Link</h2>");
            sb.AppendLine("<div class=\"field\"><label>E-Mail</label><input id=\"emailLink\" type=\"email\" autocomplete=\"username\" placeholder=\"name@firma.de\"></div>");
            sb.AppendLine("<div class=\"row\"><button class=\"warn\" id=\"btnSendLink\">Login-Link senden</button></div>");
            sb.AppendLine("<p class=\"muted\">Der Link führt zurück auf diese lokale Login-Seite. Nach dem Klick auf den Link in der E-Mail wird die Session automatisch übernommen.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"card\"><h2>Status</h2>");
            sb.AppendLine("<div class=\"muted\">Firebase-Konfiguration</div>");
            sb.AppendLine("<div class=\"mono\" id=\"configInfo\"></div>");
            sb.AppendLine("<div class=\"status\" id=\"status\">Bereit.</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"card\"><h2>Hinweis</h2>");
            sb.AppendLine("<p class=\"muted\">Google und Apple nutzen den normalen Firebase Redirect-Flow. SMS und E-Mail-Link werden innerhalb dieser Seite abgeschlossen.</p>");
            sb.AppendLine("<p class=\"muted\">Nach erfolgreichem Login kannst du dieses Browserfenster schließen.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<script>");
            sb.AppendLine("const config = " + json + ";");
            sb.AppendLine("const mode = (config.Mode || '').toLowerCase();");
            sb.AppendLine("const authConfig = { apiKey: config.ApiKey, authDomain: config.AuthDomain, projectId: config.ProjectId };");
            sb.AppendLine("firebase.initializeApp(authConfig);");
            sb.AppendLine("const auth = firebase.auth();");
            sb.AppendLine("auth.setPersistence(firebase.auth.Auth.Persistence.LOCAL).catch(() => {});");
            sb.AppendLine("let confirmationResult = null;");
            sb.AppendLine("let recaptchaVerifier = null;");
            sb.AppendLine("document.getElementById('email').value = config.InitialEmail || '';");
            sb.AppendLine("document.getElementById('emailLink').value = config.InitialEmail || '';");
            sb.AppendLine("document.getElementById('phone').value = config.InitialPhone || '';");
            sb.AppendLine("document.getElementById('configInfo').textContent = JSON.stringify({ authDomain: authConfig.authDomain, projectId: authConfig.projectId }, null, 2);");
            sb.AppendLine("function status(text, kind){ const el=document.getElementById('status'); el.textContent=text; el.className='status ' + (kind || ''); }");
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
            sb.AppendLine("  const email = document.getElementById('email').value.trim();");
            sb.AppendLine("  const password = document.getElementById('password').value;");
            sb.AppendLine("  const result = await auth.signInWithEmailAndPassword(email, password);");
            sb.AppendLine("  await postSession(result.user, 'password');");
            sb.AppendLine("}");
            sb.AppendLine("function startRedirect(provider){ status('Weiterleitung ...', 'warn'); auth.signInWithRedirect(provider); }");
            sb.AppendLine("async function signInGoogle(){ const p = new firebase.auth.GoogleAuthProvider(); p.addScope('email'); startRedirect(p); }");
            sb.AppendLine("async function signInApple(){ const p = new firebase.auth.OAuthProvider('apple.com'); p.addScope('email'); p.addScope('name'); startRedirect(p); }");
            sb.AppendLine("async function prepareRecaptcha(){ if (!recaptchaVerifier) { recaptchaVerifier = new firebase.auth.RecaptchaVerifier('recaptcha', { size: 'invisible' }, auth); await recaptchaVerifier.render(); } }");
            sb.AppendLine("async function sendSms(){");
            sb.AppendLine("  const phone = document.getElementById('phone').value.trim();");
            sb.AppendLine("  if (!phone) throw new Error('Bitte eine Telefonnummer eingeben.');");
            sb.AppendLine("  status('SMS wird gesendet ...', 'warn');");
            sb.AppendLine("  await prepareRecaptcha();");
            sb.AppendLine("  confirmationResult = await auth.signInWithPhoneNumber(phone, recaptchaVerifier);");
            sb.AppendLine("  status('SMS gesendet. Bitte den Code eingeben und auf \"SMS-Code prüfen\" klicken.', 'success');");
            sb.AppendLine("}");
            sb.AppendLine("async function verifySms(){");
            sb.AppendLine("  if (!confirmationResult) throw new Error('Bitte zuerst den SMS-Code senden.');");
            sb.AppendLine("  const code = document.getElementById('smsCode').value.trim();");
            sb.AppendLine("  if (!code) throw new Error('Bitte den SMS-Code eingeben.');");
            sb.AppendLine("  status('SMS-Code wird geprüft ...', 'warn');");
            sb.AppendLine("  const result = await confirmationResult.confirm(code);");
            sb.AppendLine("  await postSession(result.user, 'phone');");
            sb.AppendLine("}");
            sb.AppendLine("async function sendEmailLink(){");
            sb.AppendLine("  const email = document.getElementById('emailLink').value.trim();");
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
            sb.AppendLine("    let email = localStorage.getItem('mione.firebase.email') || document.getElementById('emailLink').value.trim();");
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
            sb.AppendLine("  if (mode === 'password') { status('Bitte E-Mail und Passwort eingeben.', 'warn'); document.getElementById('email').focus(); return; }");
            sb.AppendLine("  if (mode === 'sms') { status('Bitte Telefonnummer eingeben und SMS-Code senden.', 'warn'); document.getElementById('phone').focus(); return; }");
            sb.AppendLine("  if (mode === 'emaillink') { status('Bitte E-Mail eingeben und den Login-Link senden.', 'warn'); document.getElementById('emailLink').focus(); return; }");
            sb.AppendLine("}");
            sb.AppendLine("async function init(){");
            sb.AppendLine("  const redirected = await handleRedirect();");
            sb.AppendLine("  const emailLinked = await handleEmailLink();");
            sb.AppendLine("  if (redirected || emailLinked) return;");
            sb.AppendLine("  applyModeHints();");
            sb.AppendLine("}");
            sb.AppendLine("document.getElementById('btnPassword').addEventListener('click', function(){ signInPassword().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("document.getElementById('btnGoogle').addEventListener('click', function(){ signInGoogle().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("document.getElementById('btnApple').addEventListener('click', function(){ signInApple().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("document.getElementById('btnSendSms').addEventListener('click', function(){ sendSms().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("document.getElementById('btnVerifySms').addEventListener('click', function(){ verifySms().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("document.getElementById('btnSendLink').addEventListener('click', function(){ sendEmailLink().catch(function(e){ status((e && e.message) ? e.message : String(e), 'danger'); }); });");
            sb.AppendLine("window.addEventListener('load', init);");
            sb.AppendLine("</script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }
    }
}
