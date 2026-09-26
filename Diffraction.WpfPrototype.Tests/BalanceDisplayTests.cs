using Diffraction.WpfPrototype.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class BalanceDisplayTests
{
    [DataTestMethod]
    [DataRow(3.0, 0.185)]
    [DataRow(0.1, 0.24)]
    public void WarningExplainsBothLocalAndOpticalResiduals(double localResidual, double absorbed)
    {
        var energy = new EnergySnapshot(0.01, 0.4, 0.4, absorbed, 0, 0, absorbed, localResidual,
            extinction: 1, crossSectionScale: 1);
        Assert.IsFalse(energy.IsWithinTolerance);
        Assert.AreEqual("#DC6803", energy.BalanceForeground);
        foreach (string display in new[] { energy.LocalBalanceResidualDisplay, energy.CompactBalanceDisplay })
        {
            StringAssert.Contains(display, energy.LocalBalanceErrorDisplay);
            StringAssert.Contains(display, energy.OpticalBalanceErrorDisplay);
            StringAssert.Contains(display, "I_plate");
            StringAssert.Contains(display, "P_ext");
            StringAssert.Contains(display, "2%");
        }
        StringAssert.Contains(energy.CompactBalanceDisplay, energy.BalanceStatus);
    }

    [TestMethod]
    public void MissingOpticalBalanceStaysUnavailableInBothExplanations()
    {
        var energy = new EnergySnapshot(0, 0.5, 0.5, 0, 0, 0, 0, 0);
        Assert.IsFalse(energy.IsWithinTolerance);
        StringAssert.Contains(energy.LocalBalanceResidualDisplay, energy.OpticalBalanceErrorDisplay);
        StringAssert.Contains(energy.CompactBalanceDisplay, energy.OpticalBalanceErrorDisplay);
        Assert.IsFalse(energy.CompactBalanceDisplay.Contains("NaN"));
        Assert.IsFalse(EnergySnapshot.Empty.CompactBalanceDisplay.Contains("0.000"));
    }
}
