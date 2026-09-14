namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// MQTT Control Packet Types (MQTT v3.1.1 / v5.0).
    /// </summary>
    public enum MqttPacketType : byte
    {
        Reserved = 0,
        Connect = 1,
        ConnAck = 2,
        Publish = 3,
        PubAck = 4,
        PubRec = 5,
        PubRel = 6,
        PubComp = 7,
        Subscribe = 8,
        SubAck = 9,
        Unsubscribe = 10,
        UnsubAck = 11,
        PingReq = 12,
        PingResp = 13,
        Disconnect = 14,
        Auth = 15
    }

    /// <summary>
    /// Quality of Service levels.
    /// </summary>
    public enum MqttQoS : byte
    {
        AtMostOnce = 0,
        AtLeastOnce = 1,
        ExactlyOnce = 2,
        Reserved = 3
    }

    /// <summary>
    /// MQTT Fixed Header representation.
    /// </summary>
    public readonly struct MqttFixedHeader
    {
        public MqttPacketType PacketType { get; }
        public bool Dup { get; }
        public MqttQoS QoS { get; }
        public bool Retain { get; }
        public int RemainingLength { get; }

        public MqttFixedHeader(MqttPacketType packetType, bool dup, MqttQoS qos, bool retain, int remainingLength)
        {
            PacketType = packetType;
            Dup = dup;
            QoS = qos;
            Retain = retain;
            RemainingLength = remainingLength;
        }

        public byte FirstByte
        {
            get
            {
                byte val = (byte)((byte)PacketType << 4);
                if (Dup) val |= 0x08;
                val |= (byte)((byte)QoS << 1);
                if (Retain) val |= 0x01;
                return val;
            }
        }
    }
}
