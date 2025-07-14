namespace Net
{
    public interface IMessage
    {
        public int ReqId { get; set; }
    }
    public interface IMessageHandler
    {
        void Handle(IMessage message);
    }
    public interface IMessageEncoder
    {
        byte[] Encode(IMessage message);
    }
    public interface IMessageDecoder
    {
        IMessage Decode(byte[] data);
    }
}
