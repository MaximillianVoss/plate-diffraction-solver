using System;

namespace Diffraction.WpfPrototype.Models;

public sealed class EnergySnapshot
{
    public EnergySnapshot(
        double skinDepth,
        double reflectedScattered,
        double forwardScattered,
        double absorbed,
        double sheetAbove,
        double sheetBelow,
        double fluxAbsorbed,
        double localBalanceErrorPercent,
        bool isAvailable = true,
        double extinction = double.NaN,
        double crossSectionScale = double.NaN)
    {
        SkinDepth = skinDepth;
        ReflectedScattered = reflectedScattered;
        ForwardScattered = forwardScattered;
        Absorbed = absorbed;
        SheetAbove = sheetAbove;
        SheetBelow = sheetBelow;
        FluxAbsorbed = fluxAbsorbed;
        LocalBalanceErrorPercent = localBalanceErrorPercent;
        IsAvailable = isAvailable;
        Extinction = extinction;
        CrossSectionScale = crossSectionScale;
    }

    public static EnergySnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        isAvailable: false);

    public bool IsAvailable { get; }
    public double SkinDepth { get; }
    public double ReflectedScattered { get; }
    public double ForwardScattered { get; }
    public double Absorbed { get; }
    public double SheetAbove { get; }
    public double SheetBelow { get; }
    public double FluxAbsorbed { get; }
    public double LocalBalanceErrorPercent { get; }
    public double Extinction { get; }
    public double CrossSectionScale { get; }
    public bool HasGlobalBalance => IsAvailable && double.IsFinite(Extinction) && Extinction > 0;
    public double OpticalBalanceErrorPercent => HasGlobalBalance
        ? Math.Abs(Extinction - ReflectedScattered - ForwardScattered - Absorbed) / Extinction * 100.0
        : double.NaN;
    public double ReflectedCrossSection => ReflectedScattered * CrossSectionScale;
    public double ForwardCrossSection => ForwardScattered * CrossSectionScale;
    public double AbsorbedCrossSection => Absorbed * CrossSectionScale;
    public double ExtinctionCrossSection => Extinction * CrossSectionScale;
    public string OpticalBalanceErrorDisplay => HasGlobalBalance ? $"{OpticalBalanceErrorPercent:0.000}%" : "н/д";

    public double FarFieldMismatchPercent =>
        Math.Abs(ReflectedScattered - ForwardScattered) * 100.0;

    public double SheetMismatchPercent =>
        Math.Abs(SheetAbove - SheetBelow) * 100.0;

    public bool IsWithinTolerance => HasGlobalBalance && LocalBalanceErrorPercent <= 2.0 && OpticalBalanceErrorPercent <= 2.0;
    public bool IsFarFieldWithinTolerance => IsAvailable && FarFieldMismatchPercent <= 0.000001;
    public string BalanceStatus => !IsAvailable ? "Нет расчёта" : IsWithinTolerance ? "В допуске" : "Проверить";
    public string FarFieldStatus => !IsAvailable
        ? "Нет расчёта"
        : IsFarFieldWithinTolerance ? "R_scat и T_scat совпадают в допуске" : "Различие R_scat и T_scat выше допуска";
    public string BalanceForeground => !IsAvailable ? "#667085" : IsWithinTolerance ? "#16803C" : "#DC6803";
    public string BalanceBackground => !IsAvailable ? "#F9FAFB" : IsWithinTolerance ? "#F0FAF4" : "#FFF6ED";
    public string BalanceBorder => !IsAvailable ? "#D0D5DD" : IsWithinTolerance ? "#ABEFC6" : "#FEDF89";
    public string FarFieldForeground => !IsAvailable ? "#667085" : IsFarFieldWithinTolerance ? "#16803C" : "#DC6803";
    public string FarFieldBackground => !IsAvailable ? "#F9FAFB" : IsFarFieldWithinTolerance ? "#F0FAF4" : "#FFF6ED";
    public string FarFieldBorder => !IsAvailable ? "#D0D5DD" : IsFarFieldWithinTolerance ? "#ABEFC6" : "#FEDF89";
    public string ReflectedDisplay => IsAvailable && double.IsFinite(ReflectedCrossSection) ? $"{ReflectedCrossSection:0.000}" : "—";
    public string ForwardDisplay => IsAvailable && double.IsFinite(ForwardCrossSection) ? $"{ForwardCrossSection:0.000}" : "—";
    public string AbsorbedDisplay => IsAvailable && double.IsFinite(AbsorbedCrossSection) ? $"{AbsorbedCrossSection:0.000}" : "—";
    public string SkinDepthDisplay => IsAvailable ? $"{SkinDepth:0.000000}" : "—";
    public string ReflectedPercentDisplay => IsAvailable ? $"{ReflectedScattered:P1} от I_plate" : "расчёт не выполнен";
    public string ForwardPercentDisplay => IsAvailable ? $"{ForwardScattered:P1} от I_plate" : "расчёт не выполнен";
    public string AbsorbedPercentDisplay => IsAvailable ? $"{Absorbed:P1} от I_plate" : "расчёт не выполнен";
    public string LocalBalanceErrorDisplay => IsAvailable ? $"{LocalBalanceErrorPercent:0.000}%" : "—";
    public string LocalBalanceResidualDisplay =>
        IsAvailable ? $"Локальная невязка / I_plate: {LocalBalanceErrorDisplay}; оптическая теорема / P_ext: {OpticalBalanceErrorDisplay}; допуск каждой ≤ 2%." : "Нажмите «Рассчитать»";
    public string CompactBalanceDisplay =>
        IsAvailable ? $"{BalanceStatus}  •  {LocalBalanceResidualDisplay}" : "Расчёт не выполнен";
    public string CoefficientBasisDisplay => IsAvailable
        ? DescribeCoefficientBasis(SkinDepth)
        : "Коэффициенты ещё не рассчитаны.";
    public static string DescribeCoefficientBasis(double skinDepth) => skinDepth > 0
        ? "δ > 0: J(τ) = Σ c_n P_n(τ), n = 0…N−1; полиномы Лежандра без веса."
        : "PEC, δ = 0: J(τ) = Σ c_n T_n(τ)/√(1−τ²), n = 0…N−1; взвешенный базис Чебышёва.";
    public string SheetPairDisplay => IsAvailable ? $"{SheetAbove:0.000} / {SheetBelow:0.000}" : "— / —";
    public string SheetMismatchDisplay => IsAvailable ? $"{SheetMismatchPercent:0.000000}%" : "—";
}
