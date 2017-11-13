namespace EmergencyServicesBot.Services
{
    using System;
    using System.Configuration;
    using System.Net.Http;
    using System.Threading;

    public class BingAuthentication
    {
        public static readonly string FetchTokenUri = "https://api.cognitive.microsoft.com/sts/v1.0";
        private static readonly object LockObject;
        private static readonly string ApiKey;
        private string Token;
        private Timer timer;

        static BingAuthentication()
        {
            LockObject = new object();
            ApiKey = ConfigurationManager.AppSettings["MicrosoftSpeechApiKey"];
        }

        public static BingAuthentication Instance { get; } = new BingAuthentication();

        /// <summary>
        /// Gets the current access token.
        /// </summary>
        /// <returns>Current access token</returns>
        public string GetAccessToken()
        {
            // Token will be null first time the function is called.
            if (this.Token == null)
            {
                lock (LockObject)
                {
                    // This condition will be true only once in the lifetime of the application
                    if (this.Token == null)
                    {
                        this.RefreshToken();
                    }
                }
            }

            return this.Token;
        }

        private static string GetNewToken()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ApiKey);
                var response = client.PostAsync($"{FetchTokenUri}/issueToken", null).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        /// <summary>
        /// Refreshes the current token before it expires. This method will refresh the current access token.
        /// It will also schedule itself to run again before the newly acquired token's expiry by one minute.
        /// </summary>
        private void RefreshToken()
        {
            this.Token = GetNewToken();
            this.timer?.Dispose();
            this.timer = new Timer(
                x => this.RefreshToken(),
                null,
                TimeSpan.FromMinutes(9), // Specifies the delay before RefreshToken is invoked.
                TimeSpan.FromMilliseconds(-1)); // Indicates that this function will only run once
        }
    }
}