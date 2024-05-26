using System.Text;

namespace Ws2Explorer.Compiler;

public class Ws2Writer {
    public static string EncodingName = "JIS";

    public int Length { get; }
    public bool EndOfStream => stream.Position + 8 >= Length;

    private readonly Stream stream;
    private readonly BinaryWriter writer;
    private Encoding NowEncoding;

    public Ws2Writer(Stream stream) {
        this.stream = stream;
        writer = new BinaryWriter(stream);
        if (EncodingName == "GBK")
        {
            NowEncoding = GBKEncoding.Encoding;
        }
        else
        {
            NowEncoding = SjisEncoding.Encoding;
        }
    }

    public void Write(byte value) {
        writer.Write(value);
    }

    public void Write(ushort value) {
        writer.Write(value);
    }

    public void Write(int value) {
        writer.Write(value);
    }

    public void Write(uint value) {
        writer.Write(value);
    }

    public void Write(float value) {
        writer.Write(value);
    }

    public void Write(string value) {
        writer.Write(NowEncoding.GetBytes(value));
        writer.Write((byte)0);
    }
}