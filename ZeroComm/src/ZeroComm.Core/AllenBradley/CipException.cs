using System;

namespace ZeroComm.Core.AllenBradley
{
    /// <summary>
    /// Exception thrown when an EtherNet/IP encapsulation error or CIP service fault occurs.
    /// </summary>
    public class CipException : Exception
    {
        /// <summary>
        /// Gets the EtherNet/IP encapsulation status code (0x0000 = Success).
        /// </summary>
        public uint EncapsulationStatus { get; }

        /// <summary>
        /// Gets the CIP general status code (0x00 = Success).
        /// </summary>
        public byte GeneralStatus { get; }

        /// <summary>
        /// Gets any additional CIP status words returned by the PLC.
        /// </summary>
        public ushort[] AdditionalStatus { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CipException"/> class with a custom message.
        /// </summary>
        public CipException(string message) : base(message)
        {
            AdditionalStatus = Array.Empty<ushort>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CipException"/> class from an EtherNet/IP encapsulation status error.
        /// </summary>
        public CipException(uint encapsulationStatus)
            : base($"EtherNet/IP Encapsulation Error 0x{encapsulationStatus:X4}: {GetEncapsulationStatusDescription(encapsulationStatus)}")
        {
            EncapsulationStatus = encapsulationStatus;
            AdditionalStatus = Array.Empty<ushort>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CipException"/> class from a CIP General Status code.
        /// </summary>
        public CipException(byte generalStatus, ushort[]? additionalStatus = null, string? context = null)
            : base(FormatCipMessage(generalStatus, additionalStatus, context))
        {
            GeneralStatus = generalStatus;
            AdditionalStatus = additionalStatus ?? Array.Empty<ushort>();
        }

        private static string FormatCipMessage(byte generalStatus, ushort[]? additionalStatus, string? context)
        {
            string desc = GetCipGeneralStatusDescription(generalStatus);
            string ext = string.Empty;
            if (additionalStatus != null && additionalStatus.Length > 0)
            {
                ext = $" [Extended: 0x{additionalStatus[0]:X4}]";
            }
            string ctx = string.IsNullOrEmpty(context) ? string.Empty : $" (Context: {context})";
            return $"CIP Service Error 0x{generalStatus:X2}: {desc}{ext}{ctx}";
        }

        /// <summary>
        /// Resolves human-readable description for EtherNet/IP encapsulation status codes.
        /// </summary>
        public static string GetEncapsulationStatusDescription(uint status)
        {
            switch (status)
            {
                case 0x0000: return "Success";
                case 0x0001: return "The sender issued an invalid or unsupported encapsulation command";
                case 0x0002: return "Insufficient memory resources on the receiver to handle command";
                case 0x0003: return "Incorrectly formed or incomplete data in encapsulation packet";
                case 0x0064: return "Invalid session ID specified in encapsulation header";
                case 0x0065: return "Invalid length specified in encapsulation header";
                case 0x0069: return "Unsupported encapsulation protocol revision";
                default: return $"Unknown encapsulation error code 0x{status:X4}";
            }
        }

        /// <summary>
        /// Resolves human-readable description for CIP general status codes.
        /// </summary>
        public static string GetCipGeneralStatusDescription(byte status)
        {
            switch ((CipGeneralStatus)status)
            {
                case CipGeneralStatus.Success: return "Success";
                case CipGeneralStatus.ConnectionFailure: return "Connection failure";
                case CipGeneralStatus.ResourceUnavailable: return "Resource unavailable";
                case CipGeneralStatus.InvalidParameterValue: return "Invalid parameter value";
                case CipGeneralStatus.PathSegmentError: return "Path segment error (symbol syntax error)";
                case CipGeneralStatus.PathDestinationUnknown: return "Path destination unknown (tag does not exist on controller)";
                case CipGeneralStatus.PartialTransfer: return "Partial transfer (fragmentation required)";
                case CipGeneralStatus.ConnectionLost: return "Connection lost";
                case CipGeneralStatus.ServiceNotSupported: return "Service not supported";
                case CipGeneralStatus.InvalidAttributeValue: return "Invalid attribute value";
                case CipGeneralStatus.AttributeListError: return "Attribute list error";
                case CipGeneralStatus.AlreadyInRequestedMode: return "Already in requested mode/object state";
                case CipGeneralStatus.ObjectStateConflict: return "Object state conflict";
                case CipGeneralStatus.ObjectAlreadyExists: return "Object already exists";
                case CipGeneralStatus.AttributeNotSettable: return "Attribute not settable";
                case CipGeneralStatus.PrivilegeViolation: return "Privilege violation (write access denied)";
                case CipGeneralStatus.DeviceStateConflict: return "Device state conflict";
                case CipGeneralStatus.ReplyDataTooLarge: return "Reply data too large";
                case CipGeneralStatus.FragmentationPrerequisite: return "Fragmentation prerequisite not met";
                case CipGeneralStatus.NotEnoughData: return "Not enough data provided for service";
                case CipGeneralStatus.AttributeNotSupported: return "Attribute not supported";
                case CipGeneralStatus.TooMuchData: return "Too much data provided for service";
                case CipGeneralStatus.KeyFailure: return "Key failure in path";
                case CipGeneralStatus.PathSizeInvalid: return "Path size invalid";
                default: return $"Unknown CIP general status 0x{status:X2}";
            }
        }
    }
}
