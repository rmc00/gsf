//*******************************************************************************************************
//  Response.cs
//  Copyright � 2008 - TVA, all rights reserved - Gbtc
//
//  Build Environment: C#, Visual Studio 2008
//  Primary Developer: James R Carroll
//      Office: PSO TRAN & REL, CHATTANOOGA - MR 2W-C
//       Phone: 423/751-2827
//       Email: jrcarrol@tva.gov
//
//  Code Modification History:
//  -----------------------------------------------------------------------------------------------------
//  05/22/2003 - James R Carroll
//       Generated original version of source code.
//
//*******************************************************************************************************

using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace PCS.Net.Ftp
{
    public class FtpResponse
    {
        #region [ Members ]

        // Constants
        public const int InvalidCode = -1;
        public const int DataChannelOpenedTransferStart = 125;
        public const int FileOkBeginOpenDataChannel = 150;
        public const int ServiceReady = 220;
        public const int ClosingDataChannel = 226;
        public const int EnterPassiveMode = 227;
        public const int RequestFileActionComplete = 250;
        public const int UserLoggedIn = 230;
        public const int UserAcceptedWaitingPass = 331;
        public const int RequestFileActionPending = 350;
        public const int ServiceUnavailable = 421;
        public const int TransferAborted = 426;

        // Fields
        private Queue m_responses;
        private int m_code;

        #endregion

        #region [ Constructors ]

        internal FtpResponse(NetworkStream stream)
        {
            string response;

            m_responses = new Queue();

            do
            {
                response = GetLine(stream);

                try
                {
                    m_code = InvalidCode;
                    m_code = int.Parse(response.Substring(0, 3));
                }
                catch
                {
                    throw new FtpInvalidResponseException("Invalid response", this);
                }

                m_responses.Enqueue(response);
            }
            while (response.Length >= 4 && response[3] == '-');

            if (m_code == ServiceUnavailable)
                throw new FtpServerDownException(this);
        }

        #endregion

        #region [ Properties ]

        public string Message
        {
            get
            {
                return m_responses.Peek().ToString();
            }
        }

        public Queue Respones
        {
            get
            {
                return m_responses;
            }
        }

        public int Code
        {
            get
            {
                return m_code;
            }
        }

        #endregion

        #region [ Methods ]

        private char ReadAppendChar(NetworkStream stream, StringBuilder toAppend)
        {
            int i = stream.ReadByte();

            if (i > -1)
            {
                char c = Encoding.ASCII.GetChars(new byte[] { (byte)i })[0];
                toAppend.Append(c);
                return c;
            }
            else
            {
                throw new EndOfStreamException("Attempt to read past end of stream");
            }
        }

        private string GetLine(NetworkStream stream)
        {
            StringBuilder response = new StringBuilder(256);

            while (true)
            {
                // Read until carriage return received
                while (ReadAppendChar(stream, response) != '\r') {}

                // Skip thru any extra carriage returns
                while (ReadAppendChar(stream, response) == '\r') { }

                // Break on new line character received
                if (response[response.Length - 1] == '\n')
                    break;
            }

            return response.ToString();
        }

        #endregion
    }
}