using Digi.NetworkLib;
using ProtoBuf;

namespace Digi.Examples.NetworkProtobuf
{
    // An example packet with a string and a number.
    // Note that it must be ProtoIncluded in RegisterPackets.cs!
    [ProtoContract]
    public class BuddyUpRequestPacket : PacketBase
    {
        public BuddyUpRequestPacket() { }

        [ProtoMember(1)]
        public long ReceiverFaction;

        public void Setup(long receiverFaction)
        {
            ReceiverFaction = receiverFaction;
        }
        public static event ReceiveDelegate<BuddyUpRequestPacket> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }
    }
}
