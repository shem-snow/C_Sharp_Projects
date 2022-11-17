using FluentEmail.Smtp;
using FluentEmail.Core;
using System.Net.Mail;
using Microsoft.Maui.Controls.Compatibility.Platform.UWP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Windows.Media.Protection.PlayReady;

namespace SpreadsheetGUI
{
    /// <summary>
    /// This class uses the SMTP (Standard Mail Transfer Protocall) to send emails. It acts as a library class
    /// with a "SendEmail(string recipient)" method. The sender cannot be changed as the class is now and currently uses 
    /// my own gmail account.
    /// 
    /// Note that there is a property that enables getting and setting the recipient email. With a little bit of tweaking,
    /// I could also add a property for the sender.
    /// </summary>
    internal class EmailSender
    {
        // Global fields
        private NetworkCredential credentials;
        private string recipient;
        SmtpClient client;
        // SmtpSender sender;

        /// <summary>
        /// Constructor
        /// </summary>
        public EmailSender()
        {
            // Credentials
            credentials = new NetworkCredential("5e0d2a6cc66339", "fc1cebfab2dc6b"); // Parameters specify username and password.

            // Recipient
            Recipient = "u1058151@umail.utah.edu"; // Default recipient is my university email.

            // Determine which client will send the email
            client = new SmtpClient("smtp.mailtrap.io", 2525) // The parameters specify the host and port.
            {
                Credentials = credentials,
                EnableSsl = true,
            };
        }

        /// <summary>
        /// Property to access the recipient field. 
        /// It enables the user to change who (which email) receives emails.
        /// </summary>
        internal string Recipient {get { return recipient; } set { recipient = value; } }

    /// <summary>
    /// This method will send an email whose body is the contents specified by the parameter.
    /// The sender and recipient are determined beforehand somewhere in this EmailSender class.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="content"></param>
    public bool SendEmail(string content)
        {
            try // sending the email
            {
                client.Send("iamdashem@gmail.com", recipient, "The JSON contents of your current spreadsheet.", content);
                return true;
        }
            catch (Exception) { return false; }
        }
    }
}
