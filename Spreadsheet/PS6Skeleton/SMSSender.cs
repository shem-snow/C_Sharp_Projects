using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SpreadsheetGUI
{
    /// <summary>
    /// This class receives a phone number in the form of a string builder as well as a string content then it uses 
    /// "Twilio" to send a text message of the contents to the phone number.
    /// 
    /// Last edited by Shem Snow
    /// On date 10/21/2022
    /// </summary>
    internal class SMSSender
    {
        // Singleton this variable
        SMSSender singleton;

        // Sender and receiver phone numbers
        private string sender;
        private string recipient;

        // Twilio Credentials
        private string SID = Environment.GetEnvironmentVariable("AC5020b9d84949e7644dbcea9fe5fa361d");
        private string AuthToken = Environment.GetEnvironmentVariable("790986d997d2e1e8c9da4224f76d9f7e");
        


        // Singleton Constructor
        private SMSSender(string recipient)
        {
            // Set the global fields
            sender = "+18587629799"; // My twilio phone number.
            this.recipient = recipient;

            // Initialize the client.
            TwilioClient.Init(SID, Recipient);
        }

        // Property to access and mutate the recipient
        internal string Recipient { get { return recipient; } set { recipient = value; } }


        /// <summary>
        /// Attempts to send the specified contents to the currently save recipient.
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        public bool SendSMS(string contents, string recipient)
        {
            // Use a singleton programming strategty to make sure only one of this class is ever created.
            if(singleton is null)
                singleton = new SMSSender(recipient);

            try // Sending the text message
            {
                var message = MessageResource.Create(
                body: contents,
                from: new Twilio.Types.PhoneNumber(sender),
                to: new Twilio.Types.PhoneNumber(recipient)
                );
            }
            catch (Exception) { return false; }
            return true;
        }


    }
}
