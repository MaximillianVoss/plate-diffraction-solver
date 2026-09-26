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
    private static readonly string[] EnergyHeaders =
    {
        "R_scat_pct_I_plate",
        "T_scat_pct_I_plate",
        "A_J_pct_I_plate",
        "sum_pct_I_plate",
        "extinction_pct_I_plate",
        "optical_balance_error_pct_extinction",
        "cross_section_scale_um",
        "has_global_balance"
    };
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
        builder.AppendLine(CsvCell("Исследование изменения толщины скин-слоя"));
        builder.AppendLine(CsvCell(parametersSummary));
        AppendHeader(builder, "skin_depth_um");
        foreach (EnergyStudyRow row in rows)
        {
            builder.Append(FormatNumber(row.ArgumentValue));
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
        builder.AppendLine(CsvCell("Исследование изменения угла падения"));
        builder.AppendLine(CsvCell(parametersSummary));
        builder.AppendLine(CsvCell("Таблица 1. Идеальный проводник (без скин-слоя)"));
        AppendHeader(builder, "angle_deg");
        foreach (EnergyStudyRow row in idealRows)
        {
            builder.Append(FormatNumber(row.ArgumentValue));
            AppendEnergyCells(builder, row);
        }

        builder.AppendLine();
        builder.AppendLine(CsvCell("Таблица 2. Со скин-слоем (δ = " + FormatNumber(skinDepthMicrometers) + " мкм)"));
        AppendHeader(builder, "angle_deg");
        foreach (EnergyStudyRow row in skinRows)
        {
            builder.Append(FormatNumber(row.ArgumentValue));
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
        builder.AppendLine(CsvCell("Энергетика одиночного расчёта"));
        builder.AppendLine(CsvCell(parametersSummary));
        AppendHeader(builder, "skin_depth_um");
        builder.Append(FormatNumber(row.ArgumentValue));
        AppendEnergyCells(builder, row);
        return builder.ToString();
    }

    private static void AppendEnergyCells(StringBuilder builder, EnergyStudyRow row)
    {
        builder.Append(',');
        builder.Append(FormatNumber(row.ReflectedPercent));
        builder.Append(',');
        builder.Append(FormatNumber(row.TransmittedPercent));
        builder.Append(',');
        builder.Append(FormatNumber(row.AbsorbedPercent));
        builder.Append(',');
        builder.Append(FormatNumber(row.SumPercent));
        builder.Append(',');
        builder.Append(row.HasGlobalBalance ? FormatNumber(row.ExtinctionPercent) : string.Empty);
        builder.Append(',');
        builder.Append(row.HasGlobalBalance ? FormatNumber(row.ImbalancePercent) : string.Empty);
        builder.Append(',');
        builder.Append(FormatNumber(row.CrossSectionScale));
        builder.Append(',');
        builder.Append(row.HasGlobalBalance ? "true" : "false");
        builder.AppendLine();
    }

    private static void AppendHeader(StringBuilder builder, string argumentHeader)
    {
        builder.Append(CsvCell(argumentHeader));
        foreach (string header in EnergyHeaders)
            builder.Append(',').Append(CsvCell(header));
        builder.AppendLine();
    }

    private static string FormatNumber(double value) =>
        double.IsFinite(value) ? value.ToString("R", InvariantCulture) : string.Empty;

    private static string CsvCell(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
