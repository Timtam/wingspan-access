using System.Text;

namespace WingspanAccessInstaller;

/// <summary>
/// The Static Forms key problem reports go to. It is built in from the environment variable
/// WA_STATICFORMS_KEY (see WingspanAccessInstaller.csproj) and read only from there: a build without
/// it has no report function.
/// </summary>
internal static class ReportKey
{
    public static bool Available => ReportKeyData.Data.Length > 0;

    public static string Value
    {
        get
        {
            byte[] data = ReportKeyData.Data;
            var key = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                key[i] = (byte)(data[i] ^ ReportKeyData.Pad[i % ReportKeyData.Pad.Length]);
            return Encoding.UTF8.GetString(key);
        }
    }
}
