namespace EmergencyServicesBot
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using System.Net.Http.Headers;
    using System.Diagnostics;
    using Microsoft.Bing.Speech;
    using System.Threading;
    using System.Collections.Generic;
    using System.Web.Configuration;
    using System.Text;

    public class MicrosoftCognitiveSpeechService
    {
        private static string TextAnalyticsApiKey = WebConfigurationManager.AppSettings["MicrosoftTextAnalyticsApiKey"];
        public string SubscriptionKey { get; } = WebConfigurationManager.AppSettings["MicrosoftSpeechApiKey"];
        private static string SpeechRecognitionUri = WebConfigurationManager.AppSettings["MicrosoftSpeechRecognitionUri"];
        //private const string BaseUrl = "https://westus.api.cognitive.microsoft.com/";
        public string DefaultLocale { get; } = "en-US";

        private static readonly Task CompletedTask = Task.FromResult(true);

        //public async Task GetText(Stream audiostream)
        //{
        //    var preferences = new Preferences(DefaultLocale, new Uri(SpeechRecognitionUri), new CognitiveServicesAuthorizationProvider(SubscriptionKey));

        //    // Create a a speech client
        //    using (var speechClient = new SpeechClient(preferences))
        //    {
        //        speechClient.SubscribeToPartialResult(this.OnPartialResultAsync);
        //        speechClient.SubscribeToRecognitionResult(this.OnRecognitionResult);
        //        // create an audio content and pass it a stream.
        //        var deviceMetadata = new DeviceMetadata(DeviceType.Near, DeviceFamily.Desktop, NetworkType.Wifi, OsName.Windows, "1607", "Dell", "T3600");
        //        var applicationMetadata = new ApplicationMetadata("SampleApp", "1.0.0");
        //        var requestMetadata = new RequestMetadata(Guid.NewGuid(), deviceMetadata, applicationMetadata, "SampleAppService");
        //        await speechClient.RecognizeAsync(new SpeechInput(audiostream, requestMetadata), CancellationToken.None).ConfigureAwait(false);
        //    }
        //}

        /// <summary>
        /// Invoked when the speech client receives a partial recognition hypothesis from the server.
        /// </summary>
        /// <param name="args">The partial response recognition result.</param>
        /// <returns>
        /// A task
        /// </returns>
        public Task OnPartialResultAsync(RecognitionPartialResult args)

        {
            Debug.WriteLine("--- Partial result received by OnPartialResult ---");

            Debug.WriteLine(args.DisplayText);
            // Print the partial response recognition hypothesis.
            return AgentListener.Resume(args.DisplayText);
            //Debug.WriteLine(args.DisplayText);
            //return CompletedTask;
        }

        /// <summary>
        /// Invoked when the speech client receives a phrase recognition result(s) from the server.
        /// </summary>
        /// <param name="args">The recognition result.</param>
        /// <returns>
        /// A task
        /// </returns>
        public Task OnRecognitionResult(RecognitionResult args)
        {
            var response = args;
            Debug.WriteLine("--- Phrase result received by OnRecognitionResult ---");

            // Print the recognition status.
            Debug.WriteLine("***** Phrase Recognition Status = [{0}] ***", response.RecognitionStatus);

            if (response.Phrases != null)
            {
                foreach (var result in response.Phrases)
                {
                    // Print the recognition phrase display text.
                    Debug.WriteLine("{0} (Confidence:{1})", result.DisplayText, result.Confidence);
                }
            }
            return CompletedTask;
        }


        /// <summary>
        /// Gets text from an audio stream.
        /// </summary>
        /// <param name="audiostream"></param>
        /// <returns>Transcribed text. </returns>
        public async Task<string> GetTextFromAudioAsync(Stream audiostream)
        {
            var requestUri = @"https://speech.platform.bing.com/recognize?scenarios=smd&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5&locale=en-US&device.os=bot&form=BCSSTT&version=3.0&format=json&instanceid=565D69FF-E928-4B7E-87DA-9A750B96D9E3&requestid=" + Guid.NewGuid();
            //var requestUri = SpeechRecognitionUri + Guid.NewGuid();

            using (var client = new HttpClient())
            {
                var token = Authentication.Instance.GetAccessToken();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);

                try
                {
                    using (var binaryContent = new ByteArrayContent(StreamToBytes(audiostream)))
                    {
                        //binaryContent.Headers.TryAddWithoutValidation("content-type", "audio/wav; codec=\"audio/pcm\"; samplerate=16000");
                        var response = await client.PostAsync(requestUri, binaryContent);
                        var responseString = await response.Content.ReadAsStringAsync();
                        dynamic data = JsonConvert.DeserializeObject(responseString);

                        if (data != null)
                        {
                            return data.header.name;
                        }
                        {
                            return string.Empty;
                        }
                    }
                }
                catch (Exception exp)
                {
                    Debug.WriteLine(exp);
                    return string.Empty;
                }
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

        static async Task<String> CallEndpoint(HttpClient client, string uri, byte[] byteData)
        {
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync(uri, content);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}