namespace ZeroNetwork.Mqtt
{
    /// <summary>
    /// MQTT Control Packet types as defined by OASIS MQTT v3.1.1 specification.
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
        Disconnect = 14
    }

    /// <summary>
    /// MQTT Quality of Service (QoS) levels.
    /// </summary>
    public enum MqttQoS : byte
    {
        AtMostOnce = 0,
        AtLeastOnce = 1,
        ExactlyOnce = 2
    }

    /// <summary>
    /// MQTT Connection Return Codes defined in CONNACK packets.
    /// </summary>
    public enum MqttConnectReturnCode : byte
    {
        ConnectionAccepted = 0,
        UnacceptableProtocolVersion = 1,
        IdentifierRejected = 2,
        ServerUnavailable = 3,
        BadUsernameOrPassword = 4,
        NotAuthorized = 5
    }
}
