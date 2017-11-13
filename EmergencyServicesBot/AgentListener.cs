namespace EmergencyServicesBot
{
    using Microsoft.Bot.Connector;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class AgentListener
    {
        //Note: Of course you don't want these here. Eventually you will need to save these in some table

        //Having them here as static variables means we can only remember one user :)
        public static string fromId;
        public static string fromName;
        public static string toId;
        public static string toName;
        public static string serviceUrl;
        public static string channelId;
        public static string conversationId;
        public static string resumptionCookie;

        //This will send an adhoc message to the user
        public static async Task Resume(string msg)
        {
            try
            {
                var userAccount = new ChannelAccount(toId, toName);
                var botAccount = new ChannelAccount(fromId, fromName);
                var connector = new ConnectorClient(new Uri(serviceUrl));

                IMessageActivity message = Activity.CreateMessageActivity();
                if (!string.IsNullOrEmpty(conversationId) && !string.IsNullOrEmpty(channelId))
                {
                    message.ChannelId = channelId;
                }
                else
                {
                    conversationId = (await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount)).Id;
                }
                message.From = botAccount;
                message.Recipient = userAccount;
                message.Conversation = new ConversationAccount(id: conversationId);
                message.Text = msg;
                message.Locale = "en-Us";
                await connector.Conversations.SendToConversationAsync((Activity)message);
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp);
            }
        }
    }
}