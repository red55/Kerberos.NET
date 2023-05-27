using Kerberos.NET.Asn1;

using System;
using System.Collections.Generic;
using System.Text;

namespace Kerberos.NET.Entities
{
    public partial class KrbPriv : IAsn1ApplicationEncoder<KrbPriv>
    {
        public KrbPriv() {
            ProtocolVersionNumber = 5;
            MessageType = MessageType.KRB_PRIV;
        }
        public KrbPriv DecodeAsApplication(ReadOnlyMemory<byte> data)
        {
            return DecodeApplication(data);
        }
    }
}
