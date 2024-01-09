using Digi.Examples.NetworkProtobuf;
using ProtoBuf;

namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(BuddyUpRequestPacket))]
    [ProtoInclude(11, typeof(BuddyUpConfirmationPacket))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}