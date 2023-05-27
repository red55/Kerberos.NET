using Humanizer;

using Kerberos.NET.Client;
using Kerberos.NET.Credentials;
using Kerberos.NET.Crypto;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kerberos.NET.CommandLine.CommandLine
{
    [CommandLineCommand ("kpasswd", Description = "Change password using RFC 3244 protocol")]
    public class KerberosKpasswdCommand : BaseCommand
    {
        public KerberosKpasswdCommand(CommandLineParameters parameters) : base (parameters)
        {
        }

        [CommandLineParameter ("principal",
            FormalParameter = true,
            Required = true,
            Description = "UserPrincipalName @ realm")]
        public override string PrincipalName { get; set; }


        [CommandLineParameter ("V|verbose", Description = "Verbose")]
        public override bool Verbose { get; set; }
        
        [CommandLineParameter ("password", Description = "Password")]
        public string Password { get; set; }

        [CommandLineParameter ("newPassword", Description = "New Password")]
        public string NewPassword { get; set; }

        public override async Task<bool> Execute()
        {
            if (await base.Execute())
            {
                return true;
            }

            WriteLine ();

            var client = CreateClient (verbose: Verbose);
            


            string password = Password;

            if (string.IsNullOrWhiteSpace (password))
            {
                Write (SR.Resource ("CommandLine_KInit_PassPrompt", PrincipalName));

                password = ReadMasked ();
            }

            if (string.IsNullOrWhiteSpace (password))
            {
                return false;
            }
            
            var newPassword = NewPassword;
            /*if (string.IsNullOrWhiteSpace (newPassword))
            {
                Write (SR.Resource ("CommandLine_KPasswd_NewPassPrompt", PrincipalName));

                newPassword = ReadMasked ();

                if (string.IsNullOrWhiteSpace (newPassword))
                {
                    return false;
                }

                Write (SR.Resource ("CommandLine_KPasswd_NewPassVerifyPrompt", PrincipalName));

                var newPasswordConfirmation = ReadMasked ();

                if (!string.Equals(newPassword, newPasswordConfirmation, StringComparison.InvariantCulture))
                {
                    WriteLine (SR.Resource ("CommandLine_KPasswd_PassDoesNotMatch"));
                    return false;
                }

            }
            */


            var cred = new KerberosPasswordCredential (PrincipalName, password, Realm);

            try
            {
                await client.ChangePassword (cred, newPassword);
                
            }
            catch (KerberosPolicyException kex)
            {
                WriteLine ();
                WriteLine (1, "Authentication failed because of a policy violation");

                if (kex.RequestedType != null)
                {
                    string type = kex.RequestedType switch
                    {
                        Entities.PaDataType.PA_PK_AS_REQ => "Smart Card",
                        _ => kex.RequestedType.ToString ().Humanize (LetterCasing.Title),
                    };

                    WriteLine (1, "KDC requires {Type} logon", type);
                }
                else if (kex.StatusCode != null)
                {
                    WriteLine (1, "KDC error: {Error}", kex.StatusCode.ToString ().Humanize (LetterCasing.Title));
                }
                else if (!string.IsNullOrWhiteSpace (kex.Message))
                {
                    WriteLine (1, kex.Message);
                }

                return false;
            }

            return true;
        }
    }
}
