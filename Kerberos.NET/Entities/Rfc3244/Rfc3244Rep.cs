using Kerberos.NET.Asn1;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Kerberos.NET.Entities.Rfc3244
{
    public enum KpasswdError : short
    {
        Success             = 0x0000,
        Malformed           = 0x0001,
        HardError           = 0x0002,
        AuthError           = 0x0003,
        SoftError           = 0x0004,
        AccessDenied        = 0x0005,
        BadVersion          = 0x0006,
        InitialFlagNeeded   = 0x0007
    }
    public enum FieldOffsets : int
    {
        MessageLength = 0,
        Version = MessageLength + sizeof (ushort),
        ApRepLength = Version + sizeof (ushort),
        ApRep = ApRepLength + sizeof (ushort),
    };

    public class Rfc3244Rep : IAsn1ApplicationEncoder<Rfc3244Rep>
    {

        public short MessageLength { get; private set; }
        public Rfc3244ProtocolVersionNumber Version { get; private set; } = Rfc3244ProtocolVersionNumber.Reply;

        public short ApRepLength { get; private set; }
        public KrbApRep ApRep { get; private set; }

        public KrbPriv KrbPrivMsg { get; private set; }

        public KrbError Error { get; private set; }

        public Rfc3244Rep DecodeAsApplication(ReadOnlyMemory<byte> data)
        {
            
            var r = new Rfc3244Rep
            {
                MessageLength = BinaryPrimitives.ReadInt16BigEndian (data.Span.Slice ((int)FieldOffsets.MessageLength)),
                Version = (Rfc3244ProtocolVersionNumber)BinaryPrimitives.ReadInt16BigEndian (data.Span.Slice ((int)FieldOffsets.Version)),
                ApRepLength = BinaryPrimitives.ReadInt16BigEndian (data.Span.Slice ((int) FieldOffsets.ApRepLength))

            };

            if (r.ApRepLength > 0)
            {
                r.ApRep = KrbApRep.DecodeApplication (data.Slice( (int) FieldOffsets.ApRep, r.ApRepLength));
                r.KrbPrivMsg = KrbPriv.DecodeApplication (data.Slice ((int)FieldOffsets.ApRep + r.ApRepLength));
            }
            else
            {
                r.Error = KrbError.DecodeApplication (data.Slice ((int) FieldOffsets.ApRep));
            }
            return r;
        }

        public ReadOnlyMemory<byte> EncodeApplication()
        {
            throw new NotImplementedException ();
        }
    }
}
