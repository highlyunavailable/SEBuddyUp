using Digi.NetworkLib;
using ProtoBuf;

namespace Digi.Examples.NetworkProtobuf
{
    // An example packet with a string and a number.
    // Note that it must be ProtoIncluded in RegisterPackets.cs!
    [ProtoContract]
    public class BuddyUpConfirmationPacket : PacketBase
    {
        public BuddyUpConfirmationPacket() { }

        [ProtoMember(1)]
        public long SenderFaction;
        [ProtoMember(2)]
        public long ReceiverFaction;

        public void Setup(long senderFaction, long receiverFaction)
        {
            SenderFaction = senderFaction;
            ReceiverFaction = receiverFaction;
        }
        public static event ReceiveDelegate<BuddyUpConfirmationPacket> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }
    }
}
