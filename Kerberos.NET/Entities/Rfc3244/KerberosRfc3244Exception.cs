using Kerberos.NET.Entities.Rfc3244;

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Kerberos.NET.Entities
{
    [Serializable]
    public class KerberosRfc3244Exception : KerberosValidationException
    {

        KpasswdError KpasswdError { get; }

        public KerberosRfc3244Exception(KpasswdError e) : base ($"Kpasswd error: {e}.")
        {
            KpasswdError = e;
        }

        public KerberosRfc3244Exception(KpasswdError e, string message) : base ($"Kpasswd error: {e}. {message}")
        {
            KpasswdError = e;
        }

        public KerberosRfc3244Exception(string message, string parameter = null) : base (message, parameter)
        {
        }

        public KerberosRfc3244Exception(string message, Exception inner) : base (message, inner)
        {
        }

        protected KerberosRfc3244Exception(SerializationInfo serializationInfo, StreamingContext streamingContext) : base (serializationInfo, streamingContext)
        {
        }
    }
}
