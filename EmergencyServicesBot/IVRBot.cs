namespace EmergencyServicesBot
{
    using EmergencyServicesBot.Services;
using Microsoft.Bot.Builder.Calling;
using Microsoft.Bot.Builder.Calling.Events;
using Microsoft.Bot.Builder.Calling.ObjectModel.Contracts;
using Microsoft.Bot.Builder.Calling.ObjectModel.Misc;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

    public class IVRBot : IDisposable, ICallingBot
    {
        // DTMF keys required for each of option, will be used for parsing results of recognize
        private const string Emergency = "1";
        private const string Support = "2";

        // Response messages depending on user selection
        private const string Message_Welcome = "Hello, you have successfully contacted the Emergency Services Bot.";
        private const string Message_MainMenuPrompt = "If you have a life threatening medical emergency please go to your nearest hospital.  For non-life threatening situations please press 2.";
        private const string Message_NoConsultants = "Whilst we wait to connect you, please leave your name and a description of your problem. You can press the hash key when finished. We will call you as soon as possible.";
        private const string Message_Ending = "Thank you for leaving the message, goodbye";

        public IEnumerable<Participant> Participants { get; set; }
        private readonly Dictionary<string, CallState> _callStateMap = new Dictionary<string, CallState>();

        public ICallingBotService CallingBotService { get; private set; }

        public IVRBot(ICallingBotService callingBotService)
        {
            if (callingBotService == null)
                throw new ArgumentNullException(nameof(callingBotService));

            CallingBotService = callingBotService;

            CallingBotService.OnIncomingCallReceived += OnIncomingCallReceived;
            CallingBotService.OnPlayPromptCompleted += OnPlayPromptCompleted;
            CallingBotService.OnRecordCompleted += OnRecordCompleted;
            CallingBotService.OnRecognizeCompleted += OnRecognizeCompleted;
            CallingBotService.OnHangupCompleted += OnHangupCompleted;
        }

        private Task OnIncomingCallReceived(IncomingCallEvent incomingCallEvent)
        {
            var id = Guid.NewGuid().ToString();
            _callStateMap[incomingCallEvent.IncomingCall.Id] = new CallState();
            incomingCallEvent.ResultingWorkflow.Actions = new List<ActionBase>
                {
                    new Answer { OperationId = id },
                    GetPromptForText(Message_Welcome)
                };

            // Save the participants so that we can use them later for pro-active message
            // This would need to be stored/keyed for multiple users!
            Participants = incomingCallEvent.IncomingCall.Participants;
            return Task.FromResult(true);
        }

        private Task OnHangupCompleted(HangupOutcomeEvent hangupOutcomeEvent)
        {
            hangupOutcomeEvent.ResultingWorkflow = null;
            return Task.FromResult(true);
        }

        private Task OnPlayPromptCompleted(PlayPromptOutcomeEvent playPromptOutcomeEvent)
        {
            var callStateForClient = _callStateMap[playPromptOutcomeEvent.ConversationResult.Id];
            callStateForClient.InitiallyChosenMenuOption = null;
            SetupInitialMenu(playPromptOutcomeEvent.ResultingWorkflow);

            return Task.FromResult(true);
        }

        private async Task OnRecordCompleted(RecordOutcomeEvent recordOutcomeEvent)
        {
            var id = Guid.NewGuid().ToString();
            recordOutcomeEvent.ResultingWorkflow.Actions = new List<ActionBase>
                {
                    GetPromptForText(Message_Ending),
                    new Hangup { OperationId = id }
                };

            // Convert the audio to text
            if (recordOutcomeEvent.RecordOutcome.Outcome == Outcome.Success)
            {
                var record = await recordOutcomeEvent.RecordedContent;
                string sst = await BingSpeech.GetTextFromAudioAsync(record);
                await SendSTTResultToUser($"We detected the following audio: {sst}");
            }

            recordOutcomeEvent.ResultingWorkflow.Links = null;
            _callStateMap.Remove(recordOutcomeEvent.ConversationResult.Id);
        }

        private async Task SendSTTResultToUser(string text)
        {
            foreach (var participant in Participants)
            {
                if (participant.Originator)
                {
                    AgentListener.toId = participant.Identity;
                    AgentListener.toName = participant.DisplayName;
                    AgentListener.conversationId = participant.Identity; // same as channelid
                }
                else
                {
                    AgentListener.fromId = participant.Identity;
                    AgentListener.fromName = participant.DisplayName;
                }
            }
            AgentListener.channelId = "skype";
            AgentListener.serviceUrl = "https://skype.botframework.com";
            MicrosoftAppCredentials.TrustServiceUrl(AgentListener.serviceUrl);
            await AgentListener.Resume(text);

        }

        /// <summary>
        /// Gets text from an audio stream.
        /// </summary>
        /// <param name="audiostream"></param>
        /// <returns>Transcribed text. </returns>
        public async Task<string> GetTextFromAudioAsync(Stream audiostream)
        {
            var text = await BingSpeech.GetTextFromAudioAsync(audiostream);
            Debug.WriteLine(text);
            return text;
        }

        private Task OnRecognizeCompleted(RecognizeOutcomeEvent recognizeOutcomeEvent)
        {
            var callStateForClient = _callStateMap[recognizeOutcomeEvent.ConversationResult.Id];

            switch (callStateForClient.InitiallyChosenMenuOption)
            {
                case null:
                    ProcessMainMenuSelection(recognizeOutcomeEvent, callStateForClient);
                    break;
                case Emergency:
                    ProcessEmergency(recognizeOutcomeEvent, callStateForClient);
                    break;
                default:
                    SetupInitialMenu(recognizeOutcomeEvent.ResultingWorkflow);
                    break;
            }
            return Task.FromResult(true);
        }

        private void SetupInitialMenu(Workflow workflow)
        {
            workflow.Actions = new List<ActionBase> { CreateIvrOptions(Message_MainMenuPrompt, 5, false) };
        }

        private void ProcessMainMenuSelection(RecognizeOutcomeEvent outcome, CallState callStateForClient)
        {
            if (outcome.RecognizeOutcome.Outcome != Outcome.Success)
            {
                SetupInitialMenu(outcome.ResultingWorkflow);
                return;
            }

            switch (outcome.RecognizeOutcome.ChoiceOutcome.ChoiceName)
            {
                case Emergency:
                    callStateForClient.InitiallyChosenMenuOption = Emergency;
                    outcome.ResultingWorkflow = null;
                    break;
                case Support:
                    callStateForClient.InitiallyChosenMenuOption = Support;
                    SetupRecording(outcome.ResultingWorkflow);
                    break;
                default:
                    SetupInitialMenu(outcome.ResultingWorkflow);
                    break;
            }
        }

        private void ProcessEmergency(RecognizeOutcomeEvent outcome, CallState callStateForClient)
        {
            SetupRecording(outcome.ResultingWorkflow);
        }

        private static Recognize CreateIvrOptions(string textToBeRead, int numberOfOptions, bool includeBack)
        {
            if (numberOfOptions > 9)
                throw new Exception("too many options specified");

            var id = Guid.NewGuid().ToString();
            var choices = new List<RecognitionOption>();
            for (int i = 1; i <= numberOfOptions; i++)
            {
                choices.Add(new RecognitionOption { Name = Convert.ToString(i), DtmfVariation = (char)('0' + i) });
            }
            if (includeBack)
                choices.Add(new RecognitionOption { Name = "#", DtmfVariation = '#' });
            var recognize = new Recognize
            {
                OperationId = id,
                PlayPrompt = GetPromptForText(textToBeRead),
                BargeInAllowed = true,
                Choices = choices
            };

            return recognize;
        }

        private static void SetupRecording(Workflow workflow)
        {
            var id = Guid.NewGuid().ToString();

            var prompt = GetPromptForText(Message_NoConsultants);
            var record = new Record
            {
                OperationId = id,
                PlayPrompt = prompt,
                MaxDurationInSeconds = 60,
                InitialSilenceTimeoutInSeconds = 5,
                MaxSilenceTimeoutInSeconds = 4,
                PlayBeep = true,
                RecordingFormat = RecordingFormat.Wav,
                StopTones = new List<char> { '#' }
            };
            workflow.Actions = new List<ActionBase> { record };
        }

        private static PlayPrompt GetPromptForText(string text)
        {
            var prompt = new Prompt { Value = text, Voice = VoiceGender.Male };
            return new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = new List<Prompt> { prompt } };
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (CallingBotService != null)
            {
                CallingBotService.OnIncomingCallReceived -= OnIncomingCallReceived;
                CallingBotService.OnPlayPromptCompleted -= OnPlayPromptCompleted;
                CallingBotService.OnRecordCompleted -= OnRecordCompleted;
                CallingBotService.OnRecognizeCompleted -= OnRecognizeCompleted;
                CallingBotService.OnHangupCompleted -= OnHangupCompleted;
            }
        }

        #endregion

        private class CallState
        {
            public string InitiallyChosenMenuOption { get; set; }
        }
    }

}