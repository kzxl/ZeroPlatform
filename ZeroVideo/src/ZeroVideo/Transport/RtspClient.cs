using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeroVideo.Transport
{
    public enum RtspState
    {
        Disconnected,
        Init,
        Ready,
        Playing,
        Paused
    }

    /// <summary>
    /// Lightweight pure C# RTSP 1.0 (RFC 2326) protocol message formatter and response parser.
    /// Provides session negotiation for IP security cameras, NVRs, and industrial streaming servers.
    /// </summary>
    public static class RtspClient
    {
        public static string CreateOptionsRequest(string url, int cseq)
        {
            return $"OPTIONS {url} RTSP/1.0\r\n" +
                   $"CSeq: {cseq}\r\n" +
                   $"User-Agent: ZeroVideo/1.0\r\n\r\n";
        }

        public static string CreateDescribeRequest(string url, int cseq, string? sessionToken = null)
        {
            var sb = new StringBuilder();
            sb.Append($"DESCRIBE {url} RTSP/1.0\r\n");
            sb.Append($"CSeq: {cseq}\r\n");
            sb.Append("Accept: application/sdp\r\n");
            sb.Append("User-Agent: ZeroVideo/1.0\r\n");
            if (!string.IsNullOrEmpty(sessionToken))
            {
                sb.Append($"Authorization: {sessionToken}\r\n");
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        public static string CreateSetupRequest(string trackUrl, int cseq, int clientRtpPort, int clientRtcpPort, string session = "")
        {
            var sb = new StringBuilder();
            sb.Append($"SETUP {trackUrl} RTSP/1.0\r\n");
            sb.Append($"CSeq: {cseq}\r\n");
            sb.Append($"Transport: RTP/AVP;unicast;client_port={clientRtpPort}-{clientRtcpPort}\r\n");
            sb.Append("User-Agent: ZeroVideo/1.0\r\n");
            if (!string.IsNullOrEmpty(session))
            {
                sb.Append($"Session: {session}\r\n");
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        public static string CreatePlayRequest(string url, int cseq, string session)
        {
            return $"PLAY {url} RTSP/1.0\r\n" +
                   $"CSeq: {cseq}\r\n" +
                   $"Session: {session}\r\n" +
                   $"Range: npt=0.000-\r\n" +
                   $"User-Agent: ZeroVideo/1.0\r\n\r\n";
        }

        public static string CreateTeardownRequest(string url, int cseq, string session)
        {
            return $"TEARDOWN {url} RTSP/1.0\r\n" +
                   $"CSeq: {cseq}\r\n" +
                   $"Session: {session}\r\n" +
                   $"User-Agent: ZeroVideo/1.0\r\n\r\n";
        }

        /// <summary>
        /// Parses an incoming RTSP status line and response headers.
        /// </summary>
        public static bool TryParseResponse(string responseText, out int statusCode, out Dictionary<string, string> headers, out string body)
        {
            statusCode = 0;
            headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            body = string.Empty;

            if (string.IsNullOrEmpty(responseText)) return false;

            int bodySplit = responseText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string headerPart = (bodySplit >= 0) ? responseText.Substring(0, bodySplit) : responseText;
            if (bodySplit >= 0 && bodySplit + 4 < responseText.Length)
            {
                body = responseText.Substring(bodySplit + 4);
            }

            string[] lines = headerPart.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return false;

            // Status line: RTSP/1.0 200 OK
            string statusLine = lines[0];
            string[] statusTokens = statusLine.Split(new[] { ' ' }, 3);
            if (statusTokens.Length >= 2 && int.TryParse(statusTokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                statusCode = code;
            }
            else
            {
                return false;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string key = line.Substring(0, colon).Trim();
                    string val = line.Substring(colon + 1).Trim();
                    headers[key] = val;
                }
            }

            return true;
        }

        /// <summary>
        /// Extracts the H.264 video control track URI from SDP response body.
        /// </summary>
        public static string ExtractVideoTrack(string sdpBody, string baseRtspUrl)
        {
            if (string.IsNullOrEmpty(sdpBody)) return baseRtspUrl;

            string[] lines = sdpBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool inVideo = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("m=video", StringComparison.OrdinalIgnoreCase))
                {
                    inVideo = true;
                    continue;
                }
                else if (line.StartsWith("m=", StringComparison.OrdinalIgnoreCase))
                {
                    inVideo = false;
                }

                if (inVideo && line.StartsWith("a=control:", StringComparison.OrdinalIgnoreCase))
                {
                    string controlUri = line.Substring(10).Trim();
                    if (controlUri.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
                    {
                        return controlUri;
                    }
                    else if (baseRtspUrl.EndsWith("/"))
                    {
                        return baseRtspUrl + controlUri;
                    }
                    else
                    {
                        return baseRtspUrl + "/" + controlUri;
                    }
                }
            }

            return baseRtspUrl;
        }
    }
}
