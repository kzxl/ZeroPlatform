using System;
using System.Net;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Headless 4-octet state machine and calculation model for <c>IPAddressEdit</c>.
    /// Handles octet clamping (0–255), clipboard parsing, and bidirectional string/IPAddress synchronization.
    /// </summary>
    public class IPAddressModel
    {
        private readonly byte[] _octets = new byte[4];

        public event EventHandler? AddressChanged;
        public event EventHandler<int>? OctetChanged;

        public IPAddressModel()
        {
        }

        public IPAddressModel(byte o1, byte o2, byte o3, byte o4)
        {
            _octets[0] = o1;
            _octets[1] = o2;
            _octets[2] = o3;
            _octets[3] = o4;
        }

        public byte this[int index]
        {
            get => (index >= 0 && index < 4) ? _octets[index] : (byte)0;
            set => SetOctet(index, value);
        }

        public byte Octet1 => _octets[0];
        public byte Octet2 => _octets[1];
        public byte Octet3 => _octets[2];
        public byte Octet4 => _octets[3];

        public IPAddress Address
        {
            get => new IPAddress(_octets);
            set
            {
                if (value is null) return;
                var bytes = value.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    SetOctets(bytes[0], bytes[1], bytes[2], bytes[3]);
                }
            }
        }

        public string Text
        {
            get => $"{_octets[0]}.{_octets[1]}.{_octets[2]}.{_octets[3]}";
            set => TrySetFromText(value);
        }

        public bool SetOctet(int index, int value)
        {
            if (index < 0 || index >= 4) return false;

            byte clamped = (byte)Math.Max(0, Math.Min(255, value));
            if (_octets[index] != clamped)
            {
                _octets[index] = clamped;
                OctetChanged?.Invoke(this, index);
                AddressChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void SetOctets(byte o1, byte o2, byte o3, byte o4)
        {
            bool changed = _octets[0] != o1 || _octets[1] != o2 || _octets[2] != o3 || _octets[3] != o4;
            _octets[0] = o1;
            _octets[1] = o2;
            _octets[2] = o3;
            _octets[3] = o4;

            if (changed)
            {
                AddressChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool TrySetFromText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text!.Trim().Split('.');
            if (parts.Length != 4) return false;

            byte[] newOctets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(parts[i].Trim(), out newOctets[i]))
                    return false;
            }

            SetOctets(newOctets[0], newOctets[1], newOctets[2], newOctets[3]);
            return true;
        }

        public static bool TryParse(string? text, out IPAddressModel model)
        {
            model = new IPAddressModel();
            return model.TrySetFromText(text);
        }

        public override string ToString() => Text;
    }
}
