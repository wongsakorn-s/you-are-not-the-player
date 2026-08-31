using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Sim.Events;

namespace Game.Sim.Logging;

public static class WorldEventTrace
{
    public static void WriteJsonl(IEnumerable<WorldEvent> worldEvents, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(worldEvents);
        ArgumentNullException.ThrowIfNull(output);

        var logger = new JsonlWorldEventLogger(output);
        foreach (WorldEvent worldEvent in worldEvents)
        {
            logger.Write(worldEvent);
        }

        logger.Flush();
    }

    public static string ComputeSha256(IEnumerable<WorldEvent> worldEvents)
    {
        ArgumentNullException.ThrowIfNull(worldEvents);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        WriteJsonl(worldEvents, output);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(output.ToString()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
