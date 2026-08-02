using System;
using System.Text;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Core
{
    public sealed class EnergyDiagnosticsReport
    {
        public string Message { get; internal set; }
        public string Title { get; internal set; }
        public bool HasWarning { get; internal set; }
        public bool ReferenceValid { get; internal set; }
        public bool BoundaryConditionOk { get; internal set; }
        public bool HelmholtzEquationOk { get; internal set; }
        public bool ScatteringSymmetryOk { get; internal set; }
        public bool ScatteredEnergyNonNegative { get; internal set; }
        public bool LocalBalanceOk { get; internal set; }
        public bool ContourBalanceOk { get; internal set; }
        public bool AbsorptionFormsAgree { get; internal set; }
        public double Incident { get; internal set; }
        public double ReflectedScattered { get; internal set; }
        public double ForwardScattered { get; internal set; }
        public double OppositeSideSignedFlux { get; internal set; }
    }

    public static class EnergyDiagnosticsReportBuilder
    {
        public const double BoundaryConditionTolerance = 0.05;
        public const double HelmholtzTolerance = 1e-3;
        public const double AbsorptionAgreementTolerance = 0.08;
        public const double LocalBalanceTolerance = 0.02;
        public const double ContourBalanceTolerance = 0.03;
        public const double ScatteringSymmetryTolerance = 1e-8;

        public static EnergyDiagnosticsReport Build(Solver solver, double skinDepth, string caseName)
        {
            if (solver == null) throw new ArgumentNullException(nameof(solver));
            if (caseName == null) throw new ArgumentNullException(nameof(caseName));

            Solver.EnergyComponents energy = solver.CalculateEnergyComponents(includeContourDiagnostic: true);
            Solver.FarFieldScatteredEnergyComponents farField =
                solver.CalculateFarFieldScatteredEnergy(360, Math.Max(8 * solver.N, 80));
            Solver.ScatteredSheetFluxComponents scatteredSheet = solver.CalculateScatteredSheetFluxComponents();

            double incident = energy.Incident;
            double absorbedByField = skinDepth > 0 ? solver.CalculateAbsorbedEnergyByBoundaryValue() : 0.0;
            double absorbedDifference = Math.Abs(energy.Absorbed - absorbedByField);
            bool referenceValid = incident >= 1e-8;
            Func<double, string> percentText = value =>
                double.IsNaN(value) || double.IsInfinity(value) ? "н/д" : string.Format("{0:P2}", value);

            double absorbedFraction = Fraction(energy.Absorbed, incident, referenceValid);
            double absorbedByFieldFraction = Fraction(absorbedByField, incident, referenceValid);
            double absorbedDifferenceFraction = Fraction(absorbedDifference, incident, referenceValid);
            double fluxAbsorbedFraction = Fraction(energy.FluxAbsorbed, incident, referenceValid);
            double incomingFraction = Fraction(energy.IncidentSideIncoming, incident, referenceValid);
            double outgoingFraction = Fraction(energy.OppositeSideSignedFlux, incident, referenceValid);
            double incidentSideDeficitFraction = Fraction(energy.IncidentSideDeficit, incident, referenceValid);
            double localResidualFraction = Fraction(Math.Abs(energy.LocalBalanceResidual), incident, referenceValid);
            double contourResidualFraction = Fraction(Math.Abs(energy.SignedContourResidual), incident, referenceValid);
            double farReflectedFraction = Fraction(farField.ReflectedScattered, incident, referenceValid);
            double farForwardFraction = Fraction(farField.TransmittedScattered, incident, referenceValid);
            double farTotalFraction = Fraction(farField.TotalScattered, incident, referenceValid);
            double farMismatchFraction = Fraction(
                Math.Abs(farField.ReflectedScattered - farField.TransmittedScattered), incident, referenceValid);
            double sheetAboveFraction = Fraction(scatteredSheet.AboveOutgoing, incident, referenceValid);
            double sheetBelowFraction = Fraction(scatteredSheet.BelowOutgoing, incident, referenceValid);
            double sheetMismatchFraction = Fraction(scatteredSheet.AbsoluteMismatch, incident, referenceValid);
            double localTotal = energy.IncidentSideDeficit + energy.OppositeSideSignedFlux + energy.Absorbed;
            double localTotalFraction = Fraction(localTotal, incident, referenceValid);

            double bcError = solver.VerifyBoundaryConditions();
            double helmholtzError = solver.VerifyHelmholtz();
            bool boundaryConditionOk = IsFinite(bcError) && bcError < BoundaryConditionTolerance;
            bool helmholtzEquationOk = IsFinite(helmholtzError) && helmholtzError < HelmholtzTolerance;
            bool absorptionFormsAgree = skinDepth <= 0 || !referenceValid ||
                IsFinite(absorbedDifferenceFraction) && absorbedDifferenceFraction < AbsorptionAgreementTolerance;
            bool localBalanceOk = referenceValid && IsFinite(localResidualFraction) &&
                localResidualFraction < LocalBalanceTolerance;
            bool contourBalanceOk = referenceValid && IsFinite(contourResidualFraction) &&
                contourResidualFraction < ContourBalanceTolerance;
            bool scatteringSymmetryOk = !referenceValid ||
                IsFinite(farMismatchFraction) && IsFinite(sheetMismatchFraction) &&
                farMismatchFraction < ScatteringSymmetryTolerance &&
                sheetMismatchFraction < ScatteringSymmetryTolerance;
            bool scatteredEnergyNonNegative = IsFinite(farField.ReflectedScattered) &&
                IsFinite(farField.TransmittedScattered) &&
                farField.ReflectedScattered >= -1e-12 && farField.TransmittedScattered >= -1e-12;

            StringBuilder message = new StringBuilder();
            message.AppendLine("=== КОНТРОЛЬ ТОЧНОСТИ РЕШЕНИЯ ===");
            message.AppendLine("=== " + caseName + " ===");
            message.AppendLine();

            message.AppendLine("1. Граничное условие тонкого листа:");
            if (skinDepth <= 0)
            {
                message.AppendLine(string.Format("   Невязка u = 0: {0:P2}", bcError));
            }
            else
            {
                message.AppendLine(string.Format("   Невязка u - qJ = 0: {0:P2}", bcError));
                message.AppendLine("   Zs = " + FormatComplex(solver.chi.Re, solver.chi.Im) + " Ом");
                message.AppendLine("   q = " + FormatComplex(solver.SheetCoefficient.Re, solver.SheetCoefficient.Im));
            }
            message.AppendLine(boundaryConditionOk
                ? "   OK: граничное условие выполнено."
                : "   ВНИМАНИЕ: увеличьте N и проверьте сходимость.");
            message.AppendLine();

            message.AppendLine("2. Уравнение Гельмгольца:");
            message.AppendLine(string.Format("   Относительная невязка вне пластин: {0:E2}", helmholtzError));
            message.AppendLine(helmholtzEquationOk
                ? "   OK: уравнение выполнено."
                : "   ВНИМАНИЕ: проверьте квадратуру поля.");
            message.AppendLine();

            message.AppendLine("3. Энергии рассеянного поля (основной результат):");
            message.AppendLine(string.Format("   Отражённая R_scat:              {0:F6} ({1} от I)",
                farField.ReflectedScattered, percentText(farReflectedFraction)));
            message.AppendLine(string.Format("   Рассеянная вперёд T_scat:       {0:F6} ({1} от I)",
                farField.TransmittedScattered, percentText(farForwardFraction)));
            message.AppendLine(string.Format("   Суммарное рассеяние:            {0:F6} ({1} от I)",
                farField.TotalScattered, percentText(farTotalFraction)));
            message.AppendLine(string.Format("   |R_scat - T_scat| / I:          {0}", percentText(farMismatchFraction)));
            message.AppendLine(string.Format("   Поток рассеяния вверх у листа:  {0:F6} ({1} от I)",
                scatteredSheet.AboveOutgoing, percentText(sheetAboveFraction)));
            message.AppendLine(string.Format("   Поток рассеяния вниз у листа:   {0:F6} ({1} от I)",
                scatteredSheet.BelowOutgoing, percentText(sheetBelowFraction)));
            message.AppendLine(string.Format("   Расхождение потоков у листа:    {0:E6} ({1} от I)",
                scatteredSheet.AbsoluteMismatch, percentText(sheetMismatchFraction)));
            message.AppendLine(scatteringSymmetryOk && scatteredEnergyNonNegative
                ? "   OK: рассеянные потоки сверху и снизу неотрицательны и совпадают."
                : "   ВНИМАНИЕ: нарушена симметрия рассеяния тонкого листа.");
            message.AppendLine("   T_scat - поле, излучённое током вперёд; это не полный поток падающей волны за листом.");
            message.AppendLine();

            message.AppendLine("4. ЗСЭ по полным знаковым потокам на пластинах:");
            message.AppendLine("   I считается только по проекции пластин: 0.5*k*|sin(theta)|*sum(L).");
            message.AppendLine(string.Format("   Падающий поток I:               {0:F6} ({1})",
                incident, percentText(1.0)));
            if (!referenceValid)
                message.AppendLine("   При скользящем падении sin(theta)=0, поэтому нормированные доли не определены.");
            message.AppendLine(string.Format("   Полный Fz сверху (знаковый):    {0:F6}", energy.AboveFlux));
            message.AppendLine(string.Format("   Полный Fz снизу (знаковый):     {0:F6}", energy.BelowFlux));
            message.AppendLine(string.Format("   Вход со стороны падения:        {0:F6} ({1})",
                energy.IncidentSideIncoming, percentText(incomingFraction)));
            message.AppendLine(string.Format("   Знаковый поток с другой стороны:{0:F6} ({1})",
                energy.OppositeSideSignedFlux, percentText(outgoingFraction)));
            message.AppendLine(string.Format("   Дефицит входа D = I - вход:     {0:F6} ({1})",
                energy.IncidentSideDeficit, percentText(incidentSideDeficitFraction)));

            if (skinDepth > 0)
            {
                message.AppendLine(string.Format("   A_J по поверхностному току:     {0:F6} ({1})",
                    energy.Absorbed, percentText(absorbedFraction)));
                message.AppendLine(string.Format("   A_u по полю на листе:           {0:F6} ({1})",
                    absorbedByField, percentText(absorbedByFieldFraction)));
                message.AppendLine(string.Format("   A_flux = вход - выход:          {0:F6} ({1})",
                    energy.FluxAbsorbed, percentText(fluxAbsorbedFraction)));
                message.AppendLine(string.Format("   |A_J - A_u| / I:                {0}", percentText(absorbedDifferenceFraction)));
            }
            else
            {
                message.AppendLine("   Поглощение A:                   0.000000 (" + percentText(0.0) + ")");
            }

            message.AppendLine(new string('-', 62));
            message.AppendLine(string.Format("   D + поток_другой_стороны + A_J: {0:F6} ({1})",
                localTotal, percentText(localTotalFraction)));
            message.AppendLine(string.Format("   Локальная невязка A_flux-A_J:   {0:E6} ({1})",
                energy.LocalBalanceResidual, percentText(localResidualFraction)));
            message.AppendLine(localBalanceOk
                ? "   OK: верхний и нижний потоки согласованы с поглощением."
                : "   ВНИМАНИЕ: локальный баланс требует проверки сетки и N.");
            message.AppendLine("   Отрицательный знаковый поток здесь допустим и не является отрицательной T_scat.");
            message.AppendLine();

            message.AppendLine("5. Независимый замкнутый контур:");
            message.AppendLine(string.Format("   Интеграл полного потока:        {0:E6}", energy.SignedContourFlux));
            message.AppendLine(string.Format("   Интеграл потока + A_J:          {0:E6} ({1} от I)",
                energy.SignedContourResidual, percentText(contourResidualFraction)));
            message.AppendLine(contourBalanceOk
                ? "   OK: глобальный знаковый баланс замыкается."
                : "   ВНИМАНИЕ: уточните квадратуру контрольного контура.");
            message.AppendLine();

            message.AppendLine("Примечание:");
            message.AppendLine("   Для конечной пластины полный прошедший поток содержит падающее поле и интерференцию.");
            message.AppendLine("   Поэтому он не равен T_scat и не сравнивается с R_scat как отдельная энергия рассеяния.");
            message.AppendLine("   Нормировка на геометрическую проекцию может дать рассеяние больше 100%: это сечение, а не вероятность.");

            bool allChecksOk = boundaryConditionOk && helmholtzEquationOk && scatteringSymmetryOk &&
                scatteredEnergyNonNegative &&
                (!referenceValid || localBalanceOk && contourBalanceOk && absorptionFormsAgree);

            return new EnergyDiagnosticsReport
            {
                Message = message.ToString(),
                Title = skinDepth <= 0
                    ? "Контроль точности (идеальный проводник)"
                    : "Контроль точности (тонкий импедансный лист)",
                HasWarning = !allChecksOk,
                ReferenceValid = referenceValid,
                BoundaryConditionOk = boundaryConditionOk,
                HelmholtzEquationOk = helmholtzEquationOk,
                ScatteringSymmetryOk = scatteringSymmetryOk,
                ScatteredEnergyNonNegative = scatteredEnergyNonNegative,
                LocalBalanceOk = localBalanceOk,
                ContourBalanceOk = contourBalanceOk,
                AbsorptionFormsAgree = absorptionFormsAgree,
                Incident = incident,
                ReflectedScattered = farField.ReflectedScattered,
                ForwardScattered = farField.TransmittedScattered,
                OppositeSideSignedFlux = energy.OppositeSideSignedFlux
            };
        }

        public static string Compose(params EnergyDiagnosticsReport[] reports)
        {
            StringBuilder builder = new StringBuilder();
            if (reports != null)
            {
                foreach (EnergyDiagnosticsReport report in reports)
                {
                    if (report == null) continue;
                    if (builder.Length > 0) builder.AppendLine();
                    builder.AppendLine(report.Title);
                    builder.AppendLine(new string('=', report.Title.Length));
                    builder.AppendLine(report.Message.Trim());
                }
            }

            return builder.Length == 0
                ? "Подробная диагностика отсутствует."
                : builder.ToString();
        }

        private static double Fraction(double value, double incident, bool referenceValid)
        {
            return referenceValid ? value / incident : double.NaN;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string FormatComplex(double real, double imaginary)
        {
            string sign = imaginary < 0.0 ? " - " : " + ";
            return string.Format("{0:F6}{1}{2:F6}i", real, sign, Math.Abs(imaginary));
        }
    }
}
