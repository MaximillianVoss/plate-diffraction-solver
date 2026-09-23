using System.Globalization;
using System.Text;
using Diffraction.WpfPrototype.Models;

namespace Diffraction.WpfPrototype.Services;

/// <summary>
/// Формирует CSV-файлы исследовательских таблиц для серий расчёта.
/// Разделитель — запятая, десятичный разделитель — точка (инвариантная культура),
/// чтобы файлы одинаково читались и в Excel, и в скриптах.
/// </summary>
public static class StudyCsvExporter
{
    private const string EnergyHeader = "E_ref, %,E_tr, %,E_abs, %,Сумма, %,Дисбаланс, %";
    private static CultureInfo InvariantCulture { get; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Таблица исследования изменения толщины скин-слоя: зависимость компонент энергии от δ.
    /// </summary>
    public static string BuildSkinDepthStudyCsv(
        IReadOnlyList<EnergyStudyRow> rows,
        string parametersSummary)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        builder.AppendLine("Исследование изменения толщины скин-слоя");
        builder.AppendLine(CsvComment(parametersSummary));
        builder.AppendLine("Толщина скин-слоя δ, мкм," + EnergyHeader);
        foreach (EnergyStudyRow row in rows)
        {
            builder.Append(FormatArgument(row.ArgumentValue));
            AppendEnergyCells(builder, row);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Таблицы исследования изменения угла падения: отдельно для идеального проводника (δ = 0)
    /// и для выбранной толщины скин-слоя.
    /// </summary>
    public static string BuildAngleStudyCsv(
        IReadOnlyList<EnergyStudyRow> idealRows,
        IReadOnlyList<EnergyStudyRow> skinRows,
        double skinDepthMicrometers,
        string parametersSummary)
    {
        ArgumentNullException.ThrowIfNull(idealRows);
        ArgumentNullException.ThrowIfNull(skinRows);
        if (idealRows.Count != skinRows.Count)
            throw new ArgumentException("Таблицы идеального проводника и скин-слоя должны содержать одинаковое число углов.");

        var builder = new StringBuilder();
        builder.AppendLine("Исследование изменения угла падения");
        builder.AppendLine(CsvComment(parametersSummary));
        builder.AppendLine("Таблица 1. Идеальный проводник (без скин-слоя)");
        builder.AppendLine("Угол θ, °," + EnergyHeader);
        foreach (EnergyStudyRow row in idealRows)
        {
            builder.Append(FormatArgument(row.ArgumentValue));
            AppendEnergyCells(builder, row);
        }

        builder.AppendLine();
        builder.AppendLine("Таблица 2. Со скин-слоем (δ = " + FormatArgument(skinDepthMicrometers) + " мкм)");
        builder.AppendLine("Угол θ, °," + EnergyHeader);
        foreach (EnergyStudyRow row in skinRows)
        {
            builder.Append(FormatArgument(row.ArgumentValue));
            AppendEnergyCells(builder, row);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Одна строка энергетики для одиночного расчёта: выбранная δ и компоненты энергии.
    /// </summary>
    public static string BuildSingleRunCsv(EnergyStudyRow row, string parametersSummary)
    {
        ArgumentNullException.ThrowIfNull(row);
        var builder = new StringBuilder();
        builder.AppendLine("Энергетика одиночного расчёта");
        builder.AppendLine(CsvComment(parametersSummary));
        builder.AppendLine("Толщина скин-слоя δ, мкм," + EnergyHeader);
        builder.Append(FormatArgument(row.ArgumentValue));
        AppendEnergyCells(builder, row);
        return builder.ToString();
    }

    private static void AppendEnergyCells(StringBuilder builder, EnergyStudyRow row)
    {
        builder.Append(',');
        builder.Append(FormatPercent(row.ReflectedPercent));
        builder.Append(',');
        builder.Append(FormatPercent(row.TransmittedPercent));
        builder.Append(',');
        builder.Append(FormatPercent(row.AbsorbedPercent));
        builder.Append(',');
        builder.Append(FormatPercent(row.SumPercent));
        builder.Append(',');
        builder.Append(FormatPercent(row.ImbalancePercent));
        builder.AppendLine();
    }

    private static string FormatArgument(double value) => value.ToString("0.######", InvariantCulture);
    private static string FormatPercent(double value) => value.ToString("0.00", InvariantCulture);

    private static string CsvComment(string text)
    {
        // Комментарий должен занимать одну ячейку, чтобы не ломать разбор строки.
        return "\"" + text.Replace("\"", "'") + "\"";
    }
}
