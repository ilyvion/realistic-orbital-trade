using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Loading a save with a pawn mid-way through a bulk hauling trip logged errors about
// `pendingMassShortfall`: its Thing-keyed dictionary was scribed without working lists, which
// Scribe needs to resolve reference keys. Any logged error fails the test.
[TestFixture(TestType.MainMenu)]
internal sealed class HaulMultipleToTransporterScribeTests
{
    [Test]
    public static void JobDriverLoadsWithoutErrors()
    {
        var saveDriver = new JobDriver_HaulMultipleToTransporter();
        var loadDriver = new JobDriver_HaulMultipleToTransporter();

        ScribeRoundTrip.Run(saveDriver, loadDriver);

        Expect.IsFalse(loadDriver.ended);
    }
}
