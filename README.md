# VoiceBot - Emergency Services IVR Bot

This sample shows how to create a Bot using the Microsoft Bot Framework and use the Skype channel for voice input. 

DTMF tones are used to detect keypad input.  The scenario here is for an Emergency Services Bot where there could
be different types of services to route through to.  If the situation is non-life threatening a consultant will deal with the call.  However,
in this example all the consultants are busy (sound familiar?) so you are prompted to leave a voice message.

Microsoft Cognitive Services (Bing Speech Recognition) is used for the speech to text (STT).  The detected speech is then displayed back to the user
within the conversation.

# This bot uses the following nuget packages:

* Microsoft.Bot.Builder
* Microsoft.Bold.Builder.Calling
* Microsoft.Bing.Speech

# To run this bot, follow the below steps

1. Create a new bot on the dev.botframework.com portal
2. Update the bot Skype channel settings, by enabling audio calls and updating the Calling Webhook within the portal:
* Enable Calling
* Enable IVR - 1:1 audio calls
* Set the Webhook for calling eg https://7fb612b1.ngrok.io/api/calling/call
3. Add your bot to your Skype contacts
4. Ensure the bot code is running using your bot settings in the web.config
* "Microsoft.Bot.Builder.Calling.CallbackUrl" eg: https://7fb612b1.ngrok.io/api/calling/callback
* "MicrosoftSpeechApiKey" eg: get this from here: https://azure.microsoft.com/en-gb/try/cognitive-services/?api=speech-api
5. Within Skype, the Call icon should light up - click to begin conversation
* Display the Dial pad
* Select Option 2
* Record a short message and press # to end
* Hang up the call
* Your message should be converted to text and shown in the message window

