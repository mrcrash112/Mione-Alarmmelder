using System;

namespace MioneAlarmmelder.Core
{
    public sealed class FirebaseAuthSession
    {
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string ProviderId { get; set; }
        public string Uid { get; set; }
        public string PhoneNumber { get; set; }
        public string IdToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public bool IsComplete
        {
            get { return !String.IsNullOrEmpty(Uid) && !String.IsNullOrEmpty(RefreshToken); }
        }

        public void ApplyTo(AppSettings settings)
        {
            if (settings == null) return;
            settings.FirebaseUid = Uid ?? String.Empty;
            settings.FirebaseEmail = Email ?? String.Empty;
            settings.FirebaseDisplayName = DisplayName ?? String.Empty;
            settings.FirebaseProviderId = ProviderId ?? String.Empty;
            settings.FirebasePhoneNumber = PhoneNumber ?? String.Empty;
            settings.FirebaseRefreshToken = RefreshToken ?? String.Empty;
        }
    }
}
