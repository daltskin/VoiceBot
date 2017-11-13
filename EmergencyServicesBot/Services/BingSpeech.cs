namespace EmergencyServicesBot.Services
{
    using EmergencyServicesBot.Models;
    using Newtonsoft.Json;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class BingSpeech
    {
        private static readonly string BingSpeechUri = "https://speech.platform.bing.com/speech/recognition/interactive/cognitiveservices/v1?language=en-us&format=detailed";

        public static async Task<string> GetTextFromAudioAsync(Stream audiostream)
        {
            string result = "";
            
            //string accessToken = await GetAccessToken();
            string accessToken = BingAuthentication.Instance.GetAccessToken();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                using (var binaryContent = new ByteArrayContent(StreamToBytes(audiostream)))
                {
                    binaryContent.Headers.TryAddWithoutValidation("content-type", "audio/wav; codec=\"audio/pcm\"; samplerate=16000");
                    var response = await client.PostAsync(BingSpeechUri, binaryContent);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        var bingResult = JsonConvert.DeserializeObject<BingSpeechResult>(responseString);
                        result = bingResult?.NBest?.OrderByDescending(b => b.Confidence)?.FirstOrDefault()?.Display;
                    }
                }
                return result;
            }
        }
        
        /// <summary>
        /// Converts Stream into byte[].
        /// </summary>
        /// <param name="input">Input stream</param>
        /// <returns>Output byte[]</returns>
        private static byte[] StreamToBytes(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}