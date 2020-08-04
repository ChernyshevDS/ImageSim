namespace ImageSim.Services
{
    public interface IBinarySerializable
    {
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}
