using System.Text;

namespace Ws2Explorer.Compiler;

public static class GBKEncoding {
    public static Encoding Encoding { get; }

    static GBKEncoding() {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding = Encoding.GetEncoding("gbk");
    }
}