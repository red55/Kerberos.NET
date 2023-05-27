using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kerberos.NET.Entities.Rfc3244
{
    public enum Rfc3244ProtocolVersionNumber : ushort {
        Request = 0xff80,
        Reply = 0x0001
    }
    public class Rfc3244Req
    {
                
        public Rfc3244ProtocolVersionNumber Version => Rfc3244ProtocolVersionNumber.Request;
                    
        public KrbApReq ApReq { get; set; }

        public KrbPriv KrbPrivMsg { get; set; }


        public ReadOnlyMemory<byte> Encode()
        {
            if (ApReq == null)
            {
                throw new ArgumentNullException(nameof(ApReq));
            }

            if (KrbPrivMsg == null)
            {
                throw new ArgumentNullException (nameof (KrbPrivMsg));
            }

            

            var derApReq = ApReq.EncodeApplication ();
            var derKrbPriv = KrbPrivMsg.EncodeApplication ();

            var Length = (short)(
                sizeof (short)     /* sizeof(this.Length) */
                + sizeof (Rfc3244ProtocolVersionNumber)
                + sizeof (short)   /* sizeof(ApReq.Length) */
                + derApReq.Length
                + derKrbPriv.Length
                );

            var buffer = new byte[Length];
            var mem = buffer.AsMemory ();
            

            BinaryPrimitives.WriteInt16BigEndian (mem.Span.Slice ((int) Rfc3244.FieldOffsets.MessageLength), Length);
            BinaryPrimitives.WriteInt16BigEndian (mem.Span.Slice ((int) Rfc3244.FieldOffsets.Version), (short) Version);
            BinaryPrimitives.WriteInt16BigEndian (mem.Span.Slice ((int) Rfc3244.FieldOffsets.ApRepLength), (short) derApReq.Length);

            derApReq.CopyTo (mem.Slice ((int)Rfc3244.FieldOffsets.ApRep));
            derKrbPriv.CopyTo (mem.Slice ((int)Rfc3244.FieldOffsets.ApRep + derApReq.Length));
/*
            using var s = new MemoryStream (buffer.Memory);
            using var sw = new StreamWriter (s);

            sw.Write (Length);
            sw.Write (Rfc3244ProtocolVersionNumber.Request);
            sw.Write (derApReq.Length);
            sw.Write (derApReq);
            sw.Write (derKrbPriv);
*/
         

            return mem;
        }
    }
}
