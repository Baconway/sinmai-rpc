using System;
using System.IO;
using System.Text;

namespace maimai_rpc
{
    public enum DiscordOpcode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4
    }

    /// <summary>
    /// Handles the low-level binary frame encoding and decoding for the Discord IPC protocol.
    /// Each frame starts with an 8-byte header:
    ///   - Opcode (Int32, 4 bytes, Little Endian)
    ///   - Length (Int32, 4 bytes, Little Endian)
    /// Followed by Length bytes of UTF-8 encoded JSON.
    /// </summary>
    public static class DiscordIpcProtocol
    {
        private const int MaxPayloadSize = 65536;

        public static void WriteFrame(Stream stream, DiscordOpcode opcode, string json)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            byte[] payload = string.IsNullOrEmpty(json) ? new byte[0] : Encoding.UTF8.GetBytes(json);
            byte[] header = new byte[8];

            BitConverter.GetBytes((int)opcode).CopyTo(header, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(header, 4);

            stream.Write(header, 0, header.Length);
            if (payload.Length > 0)
            {
                stream.Write(payload, 0, payload.Length);
            }
            stream.Flush();
        }

        public static bool ReadFrame(Stream stream, out DiscordOpcode opcode, out string json)
        {
            opcode = DiscordOpcode.Close;
            json = null;

            if (stream == null)
                return false;

            byte[] header = new byte[8];
            int totalHeaderRead = 0;

            while (totalHeaderRead < 8)
            {
                int read = stream.Read(header, totalHeaderRead, 8 - totalHeaderRead);
                if (read <= 0)
                {
                    // Stream closed or EOF
                    return false;
                }
                totalHeaderRead += read;
            }

            int rawOpcode = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);

            if (length < 0 || length > MaxPayloadSize)
            {
                return false;
            }

            byte[] payload = new byte[length];
            int totalPayloadRead = 0;

            while (totalPayloadRead < length)
            {
                int read = stream.Read(payload, totalPayloadRead, length - totalPayloadRead);
                if (read <= 0)
                {
                    return false;
                }
                totalPayloadRead += read;
            }

            opcode = (DiscordOpcode)rawOpcode;
            json = length > 0 ? Encoding.UTF8.GetString(payload, 0, length) : string.Empty;
            return true;
        }
    }
}
