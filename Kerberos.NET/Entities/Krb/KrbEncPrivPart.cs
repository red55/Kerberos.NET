using Kerberos.NET.Asn1;

using System;
using System.Collections.Generic;
using System.Security.Cryptography.Asn1;
using System.Text;

namespace Kerberos.NET.Entities
{
    public partial class KrbEncPrivPart : IAsn1ApplicationEncoder<KrbEncPrivPart>
    {
        public KrbEncPrivPart DecodeAsApplication(ReadOnlyMemory<byte> data)
        {
            return DecodeApplication (data);
        }

    }
}
