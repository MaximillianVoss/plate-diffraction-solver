using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Diffraction.WpfPrototype.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class NumericInputConverterTests
{
    private static CultureInfo Culture => CultureInfo.GetCultureInfo("ru-RU");
    private static IValueConverter Create() => (IValueConverter)new NumericInputConverter().ProvideValue(null!);

    [DataTestMethod]
    [DataRow("0,", 0.0)]
    [DataRow("0,0", 0.0)]
    [DataRow("0,01", 0.01)]
    [DataRow("1E-2", 0.01)]
    [DataRow("1,0000", 1.0)]
    public void ValidInputKeepsItsSpelling(string text, double number)
    {
        IValueConverter converter = Create();
        Assert.AreEqual(number, converter.ConvertBack(text, typeof(double), null!, Culture));
        Assert.AreEqual(text, converter.Convert(number, typeof(string), null!, Culture));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("-")]
    [DataRow("1E-")]
    public void InvalidInputIsRejectedWithoutThrowing(string text)
    {
        Assert.AreSame(DependencyProperty.UnsetValue, Create().ConvertBack(text, typeof(double), null!, Culture));
    }

    [TestMethod]
    public void IntegerInputDoesNotAcceptFractionsOrOverflow()
    {
        IValueConverter converter = Create();
        Assert.AreEqual(2, converter.ConvertBack("2", typeof(int), null!, Culture));
        Assert.AreSame(DependencyProperty.UnsetValue, converter.ConvertBack("2,5", typeof(int), null!, Culture));
        Assert.AreSame(DependencyProperty.UnsetValue, converter.ConvertBack("2147483648", typeof(int), null!, Culture));
    }

    [TestMethod]
    public void EditorsDoNotShareCachedTextAndExternalChangesRefreshTheDisplay()
    {
        IValueConverter first = Create();
        IValueConverter second = Create();
        first.ConvertBack("0,0", typeof(double), null!, Culture);
        Assert.AreEqual("0", second.Convert(0.0, typeof(string), null!, Culture));
        Assert.AreEqual("2", first.Convert(2.0, typeof(string), null!, Culture));
        Assert.AreEqual("0", first.Convert(0.0, typeof(string), null!, Culture));
    }
}
