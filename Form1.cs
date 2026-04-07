using System;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DifrOnLenta = Diffraction.Program.DifrOnLenta;
using Compl = Diffraction.Program.Compl;

namespace Diffraction
{
    public partial class MainForm : Form
    {
        private Form2 currentForm2 = null;

        public MainForm()
        {
            InitializeComponent();

            // Настройка графика chartRealPart
            chartRealPart.Series[0].ChartType = SeriesChartType.Line;
            chartRealPart.Series[0].Color = Color.Blue;
            chartRealPart.Series[0].BorderWidth = 3;
            chartRealPart.Series[0].Name = "Полное поле u без скин-слоя";
            chartRealPart.Series.Add(new Series());

            chartRealPart.Series[1].ChartType = SeriesChartType.Line;
            chartRealPart.Series[1].Color = Color.FromArgb(180, 255, 0, 0); // Полупрозрачный красный
            chartRealPart.Series[1].BorderWidth = 3;
            chartRealPart.Series[1].BorderDashStyle = ChartDashStyle.Solid; // Сплошная линия
            chartRealPart.Series[1].Name = "Полное поле u со скин-слоем";

            // Включение и настройка легенды
            chartRealPart.Legends[0].Enabled = true;
            chartRealPart.Legends[0].Docking = Docking.Bottom;
            chartRealPart.Legends[0].Alignment = StringAlignment.Center;

            // Подписи осей
            chartRealPart.ChartAreas[0].AxisX.Title = "x";
            chartRealPart.ChartAreas[0].AxisY.Title = "y";

            // Настройка внешнего вида осей
            chartRealPart.ChartAreas[0].AxisX.TitleFont = new Font("Arial", 10, FontStyle.Bold);
            chartRealPart.ChartAreas[0].AxisY.TitleFont = new Font("Arial", 10, FontStyle.Bold);
            
            // Автоматический запуск формы с графиками при загрузке
            this.Shown += MainForm_Shown;
        }

        // Обработчик события Shown для автоматического открытия Form2
        private async void MainForm_Shown(object sender, EventArgs e)
        {
            // Автоматически открываем форму с графиками
            await OpenGraphicsFormAsync();
        }

        // Метод для открытия формы с графиками (рефакторинг button2_Click)
        private async Task OpenGraphicsFormAsync()
        {
            // Повторно используем Form2, если она уже открыта
            if (currentForm2 == null || currentForm2.IsDisposed)
            {
                currentForm2 = new Form2();
            }

            PlateCalculationInput input;
            if (!TryReadCalculationInput(out input))
                return;

            int width = currentForm2.pictureBoxNoSkin.Width;
            int height = currentForm2.pictureBoxNoSkin.Height;
            var progress = new Progress<string>(UpdateCalculationStatus);

            SetCalculationBusy(true, "Построение поля...");
            try
            {
                // Создание изображений графиков с помощью метода CreateGraphImages
                GraphImagePair images = await Task.Run(() => CreateGraphImages(input, width, height, progress));

                if (images != null)
                {
                    currentForm2.pictureBoxNoSkin.Image = images.ImageNoSkin;
                    currentForm2.pictureBoxSkin.Image = images.ImageSkin;
                }

                if (!currentForm2.Visible)
                    currentForm2.Show();
                else
                    currentForm2.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("Ошибка построения поля: {0}", ex.Message),
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetCalculationBusy(false, "Готово");
            }
        }

        // Обработчик события изменения значения числового поля xL.
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля xL больше или равно значению числового поля xR,
            // установить значение числового поля xL равным значению числового поля xR - 1.
            if (xL.Value >= xR.Value)
            {
                xL.Value = xR.Value - 1;
            }
            // Установить минимальное значение оси X графика chartRealPart равным значению числового поля xL.
            chartRealPart.ChartAreas[0].AxisX.Minimum = (int)xL.Value;
        }

        // Обработчик события изменения значения числового поля yDn.
        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля yDn больше или равно значению числового поля yUp,
            // установить значение числового поля yDn равным значению числового поля yUp - 1.
            if (yDn.Value >= yUp.Value)
            {
                yDn.Value = yUp.Value - 1;
            }
            // Установить минимальное значение оси Y графика chartRealPart равным значению числового поля yDn.
            chartRealPart.ChartAreas[0].AxisY.Minimum = (int)yDn.Value;
        }

        // Обработчик события изменения значения числового поля xR.
        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля xL больше или равно значению числового поля xR,
            // установить значение числового поля xR равным значению числового поля xL + 1.
            if (xL.Value >= xR.Value)
            {
                xR.Value = xL.Value + 1;
            }
            // Установить максимальное значение оси X графика chartRealPart равным значению числового поля xR.
            chartRealPart.ChartAreas[0].AxisX.Maximum = (int)xR.Value;
        }

        // Обработчик события изменения значения числового поля yUp.
        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля yDn больше или равно значению числового поля yUp,
            // установить значение числового поля yUp равным значению числового поля yDn + 1.
            if (yDn.Value >= yUp.Value)
            {
                yUp.Value = yDn.Value + 1;
            }
            // Установить максимальное значение оси Y графика chartRealPart равным значению числового поля yUp.
            chartRealPart.ChartAreas[0].AxisY.Maximum = (int)yUp.Value;
        }

        // Обработчик события изменения значения числового поля truncationParameterN.
        private void numericUpDown8_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля truncationParameterN меньше или равно нулю,
            // установить значение числового поля truncationParameterN равным 1.
            if (truncationParameterN.Value <= 0)
            {
                truncationParameterN.Value = 1;
            }
        }

        // Обработчик события изменения значения числового поля bandBoundaryA.
        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            EnsureIncreasingInterval(bandBoundaryA, bandBoundaryB, changedLeft: true);
        }

        // Обработчик события изменения значения числового поля bandBoundaryB.
        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            EnsureIncreasingInterval(bandBoundaryA, bandBoundaryB, changedLeft: false);
        }

        private void numericUpDown9_ValueChanged(object sender, EventArgs e)
        {
            EnsureIncreasingInterval(bandBoundaryA2, bandBoundaryB2, changedLeft: true);
        }

        private void numericUpDown10_ValueChanged(object sender, EventArgs e)
        {
            EnsureIncreasingInterval(bandBoundaryA2, bandBoundaryB2, changedLeft: false);
        }

        private void EnsureIncreasingInterval(NumericUpDown left, NumericUpDown right, bool changedLeft)
        {
            decimal gap = Math.Max(left.Increment, 0.001m);
            if (left.Value < right.Value) return;

            if (changedLeft)
            {
                decimal newLeft = right.Value - gap;
                if (newLeft >= left.Minimum)
                    left.Value = newLeft;
                else
                    right.Value = left.Value + gap;
            }
            else
            {
                decimal newRight = left.Value + gap;
                if (newRight <= right.Maximum)
                    right.Value = newRight;
                else
                    left.Value = right.Value - gap;
            }
        }

        private bool TryReadPlateParameters(out double alpha1, out double beta1, out double alpha2, out double beta2)
        {
            alpha1 = (double)bandBoundaryA.Value;
            beta1 = (double)bandBoundaryB.Value;
            alpha2 = (double)bandBoundaryA2.Value;
            beta2 = (double)bandBoundaryB2.Value;

            string error = ValidatePlateGeometry(alpha1, beta1, alpha2, beta2);
            if (error == null) return true;

            MessageBox.Show(error, "Ошибка геометрии пластин", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private static string ValidatePlateGeometry(double alpha1, double beta1, double alpha2, double beta2)
        {
            if (alpha1 >= beta1)
                return "Для пластины 1 должно выполняться alpha1 < beta1.";
            if (alpha2 >= beta2)
                return "Для пластины 2 должно выполняться alpha2 < beta2.";
            if (Math.Max(alpha1, alpha2) < Math.Min(beta1, beta2))
                return "Пластины накладываются друг на друга. Измените alpha/beta так, чтобы интервалы не пересекались.";
            return null;
        }

        // Обработчик события изменения значения числового поля wavelength.
        private void numericUpDown7_ValueChanged(object sender, EventArgs e)
        {
            // Если значение числового поля wavelength меньше или равно нулю,
            // установить значение числового поля wavelength равным 1.
            if (wavelength.Value <= 0)
            {
                wavelength.Value = 1;
            }
        }

        // Обработчик события нажатия кнопки button1.
        private async void button1_Click(object sender, EventArgs e)
        {
            // Очистка предыдущих данных
            chartRealPart.Series[0].Points.Clear(); // Без скин-слоя
            chartRealPart.Series[1].Points.Clear(); // Со скин-слоем
            textBoxChebPolynomial.Clear();

            PlateCalculationInput input;
            if (!TryReadCalculationInput(out input))
                return;

            int imageWidth = 0;
            int imageHeight = 0;
            bool updateGraphics = currentForm2 != null && !currentForm2.IsDisposed && currentForm2.Visible;
            if (updateGraphics)
            {
                imageWidth = currentForm2.pictureBoxNoSkin.Width;
                imageHeight = currentForm2.pictureBoxNoSkin.Height;
            }

            var progress = new Progress<string>(UpdateCalculationStatus);
            CalculationResult result = null;

            SetCalculationBusy(true, "Запуск расчета...");

            try
            {
                result = await Task.Run(() => RunFullCalculation(input, imageWidth, imageHeight, progress));
                ApplyCalculationResult(result, updateGraphics);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("Ошибка расчета: {0}", ex.Message),
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetCalculationBusy(false, "Готово");
            }

            if (result != null)
            {
                if (result.NoSkinReport != null)
                    ShowAccuracyReport(result.NoSkinReport);
                if (result.SkinReport != null)
                    ShowAccuracyReport(result.SkinReport);
            }
        }

        private class PlateCalculationInput
        {
            public int Param;
            public double Alpha1, Beta1, Alpha2, Beta2;
            public double PlotLeft, PlotRight;
            public double X1, X2, Y1, Y2;
            public double Angle, Len, SkinDepth;
        }

        private class CalculationResult
        {
            public bool NoSkinSolved;
            public bool SkinSolved;
            public double[] XValues;
            public double[] NoSkinReal;
            public double[] SkinReal;
            public string CoefficientsText;
            public string ConductivityText;
            public Color ConductivityColor;
            public AccuracyReport NoSkinReport;
            public AccuracyReport SkinReport;
            public GraphImagePair Images;
        }

        private class AccuracyReport
        {
            public string Message;
            public string Title;
            public MessageBoxIcon Icon;
        }

        private bool TryReadCalculationInput(out PlateCalculationInput input)
        {
            input = null;

            double alpha1, beta1, alpha2, beta2;
            if (!TryReadPlateParameters(out alpha1, out beta1, out alpha2, out beta2))
                return false;

            input = new PlateCalculationInput
            {
                Param = (int)truncationParameterN.Value,
                Alpha1 = alpha1,
                Beta1 = beta1,
                Alpha2 = alpha2,
                Beta2 = beta2,
                PlotLeft = Math.Min(alpha1, alpha2),
                PlotRight = Math.Max(beta1, beta2),
                X1 = (double)xL.Value,
                X2 = (double)xR.Value,
                Y1 = (double)yDn.Value,
                Y2 = (double)yUp.Value,
                Angle = (double)angleInDegrees.Value / 180 * Math.PI,
                Len = (double)wavelength.Value,
                SkinDepth = (double)skinDepthInput.Value
            };
            return true;
        }

        private CalculationResult RunFullCalculation(PlateCalculationInput input, int imageWidth, int imageHeight, IProgress<string> progress)
        {
            CalculationResult result = new CalculationResult();

            progress.Report("Решение без скин-слоя...");
            DifrOnLenta qNoSkin = new DifrOnLenta(input.Alpha1, input.Beta1, input.Alpha2, input.Beta2, input.Len, input.Angle, input.Param, 0);
            result.NoSkinSolved = qNoSkin.SolveDifr() == 1;

            progress.Report("Решение со скин-слоем...");
            DifrOnLenta qSkin = new DifrOnLenta(input.Alpha1, input.Beta1, input.Alpha2, input.Beta2, input.Len, input.Angle, input.Param, input.SkinDepth);
            result.SkinSolved = qSkin.SolveDifr() == 1;
            if (!result.SkinSolved)
                throw new InvalidOperationException("Ошибка решения задачи с учетом скин-слоя");

            progress.Report("Подготовка графика...");
            result.XValues = BuildPlotXValues(input.PlotLeft, input.PlotRight, 1000);
            double zPlot = input.Len / 10.0;
            if (result.NoSkinSolved)
                result.NoSkinReal = SampleRealPart(qNoSkin, result.XValues, zPlot);
            result.SkinReal = SampleRealPart(qSkin, result.XValues, zPlot);

            progress.Report("Подготовка коэффициентов...");
            result.CoefficientsText = BuildCoefficientText(input, qNoSkin, qSkin, result.NoSkinSolved);
            BuildConductivityStatus(input, qSkin, out result.ConductivityText, out result.ConductivityColor);

            progress.Report("Расчет проверок точности...");
            if (result.NoSkinSolved)
                result.NoSkinReport = BuildAccuracyReport(qNoSkin, skinDepth: 0, caseName: "БЕЗ СКИН-СЛОЯ (идеальный проводник)");
            result.SkinReport = BuildAccuracyReport(qSkin, input.SkinDepth, caseName: "СО СКИН-СЛОЕМ");

            if (imageWidth > 0 && imageHeight > 0 && result.NoSkinSolved && result.SkinSolved)
            {
                progress.Report("Построение поля...");
                result.Images = CreateGraphImages(input, imageWidth, imageHeight, qNoSkin, qSkin, progress);
            }

            return result;
        }

        private static double[] BuildPlotXValues(double left, double right, int segments)
        {
            double[] values = new double[segments + 1];
            double h = (right - left) / segments;
            for (int i = 0; i < values.Length; i++)
                values[i] = left + h * i;
            return values;
        }

        private static double[] SampleRealPart(DifrOnLenta solver, double[] xValues, double z)
        {
            double[] values = new double[xValues.Length];
            Parallel.For(0, xValues.Length, i =>
            {
                values[i] = solver.u(xValues[i], z).Re;
            });
            return values;
        }

        private string BuildCoefficientText(PlateCalculationInput input, DifrOnLenta qNoSkin, DifrOnLenta qSkin, bool noSkinSolved)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Импедансный коэффициент χ:");
            builder.AppendFormat("χ = {0:F6} + {1:F6}i{2}", qSkin.chi.Re, qSkin.chi.Im, Environment.NewLine);
            builder.AppendLine();

            int totalCoefficients = input.Param * qSkin.PlateCount;
            builder.AppendFormat("Коэффициенты Чебышева (всего {0}, по {1} на пластину):{2}",
                totalCoefficients, input.Param, Environment.NewLine);
            builder.AppendFormat("{0,-10} {1,-4} {2,-25} {3,-25}{4}",
                "Пластина", "#", "БЕЗ скин-слоя", "СО скин-слоем", Environment.NewLine);
            builder.AppendLine(new string('.', 76));

            for (int plateIndex = 0; plateIndex < qSkin.PlateCount; plateIndex++)
            {
                for (int coeffIndex = 0; coeffIndex < input.Param; coeffIndex++)
                {
                    int globalIndex = plateIndex * input.Param + coeffIndex;
                    string noSkinCoeff = noSkinSolved
                        ? string.Format("{0:F4}+{1:F4}i", qNoSkin.y[globalIndex].Re, qNoSkin.y[globalIndex].Im)
                        : "нет решения";
                    string skinCoeff = string.Format("{0:F4}+{1:F4}i", qSkin.y[globalIndex].Re, qSkin.y[globalIndex].Im);
                    builder.AppendFormat("{0,-10} {1,-4} {2,-25} {3,-25}{4}",
                        plateIndex + 1, coeffIndex + 1, noSkinCoeff, skinCoeff, Environment.NewLine);
                }
            }

            return builder.ToString();
        }

        private void BuildConductivityStatus(PlateCalculationInput input, DifrOnLenta qSkin, out string text, out Color color)
        {
            if (input.SkinDepth > 0)
            {
                try
                {
                    double conductivity = qSkin.CalculateConductivity(input.SkinDepth, input.Len);
                    text = string.Format("Проводимость: {0:E2} См/м", conductivity);
                    color = Color.DarkBlue;
                }
                catch (Exception ex)
                {
                    text = string.Format("Ошибка: {0}", ex.Message);
                    color = Color.Red;
                }
            }
            else
            {
                text = "Проводимость: не рассчитана (идеальный проводник)";
                color = Color.Gray;
            }
        }

        private void ApplyCalculationResult(CalculationResult result, bool updateGraphics)
        {
            chartRealPart.Series[0].Points.Clear();
            chartRealPart.Series[1].Points.Clear();

            if (result.NoSkinReal != null)
                chartRealPart.Series[0].Points.DataBindXY(result.XValues, result.NoSkinReal);
            if (result.SkinReal != null)
                chartRealPart.Series[1].Points.DataBindXY(result.XValues, result.SkinReal);

            textBoxChebPolynomial.Text = result.CoefficientsText ?? string.Empty;
            lblConductivity.Text = result.ConductivityText;
            lblConductivity.ForeColor = result.ConductivityColor;

            if (updateGraphics && result.Images != null && currentForm2 != null && !currentForm2.IsDisposed)
            {
                currentForm2.pictureBoxNoSkin.Image = result.Images.ImageNoSkin;
                currentForm2.pictureBoxSkin.Image = result.Images.ImageSkin;
                currentForm2.Refresh();
            }
        }

        private void SetCalculationBusy(bool busy, string status)
        {
            CalculateButton.Enabled = !busy;
            buttonGraphic.Enabled = !busy;
            groupBox1.Enabled = !busy;
            groupBox2.Enabled = !busy;
            groupBox3.Enabled = !busy;
            groupBox4.Enabled = !busy;
            groupBoxSkin.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            progressCalculation.Visible = busy;
            progressCalculation.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            progressCalculation.MarqueeAnimationSpeed = busy ? 30 : 0;
            UpdateCalculationStatus(status);
        }

        private void UpdateCalculationStatus(string status)
        {
            labelCalculationStatus.Text = status;
        }

        // Новый метод для отображения отчета о точности
        private AccuracyReport BuildAccuracyReport(DifrOnLenta solver, double skinDepth, string caseName)
        {
            // Расчет энергий
            var energyComp = solver.CalculateEnergyComponents();

            double incidentEnergy = energyComp.Incident;
            double reflectedEnergy = energyComp.Reflected;
            double transmittedEnergy = energyComp.Transmitted;
            double absorbedEnergy = energyComp.Absorbed;
            bool energyReferenceValid = Math.Abs(incidentEnergy) >= 1e-8;
            Func<double, string> percentText = value =>
                (double.IsNaN(value) || double.IsInfinity(value)) ? "н/д" : string.Format("{0:P2}", value);

            // Относительные доли энергии
            double reflectedFraction = energyReferenceValid ? reflectedEnergy / incidentEnergy : double.NaN;
            double transmittedFraction = energyReferenceValid ? transmittedEnergy / incidentEnergy : double.NaN;
            double absorbedFraction = energyReferenceValid ? absorbedEnergy / incidentEnergy : double.NaN;

            double sumRAT = reflectedEnergy + absorbedEnergy + transmittedEnergy;
            double sumFraction = energyReferenceValid ? sumRAT / incidentEnergy : double.NaN;

            // Формирование сообщения для всплывающего окна
            StringBuilder energyMessage = new StringBuilder();
            energyMessage.AppendLine($"=== КОНТРОЛЬ ТОЧНОСТИ РЕШЕНИЯ ===");
            energyMessage.AppendLine($"=== {caseName} ===");
            energyMessage.AppendLine();
            energyMessage.AppendLine("На основе физических законов и тождеств");
            energyMessage.AppendLine();

            // 1. Граничные условия (условие Леонтовича)
            double bcError = solver.VerifyBoundaryConditions();
            energyMessage.AppendLine("1. Граничные условия (условие Леонтовича):");

            if (skinDepth == 0)
            {
                energyMessage.AppendLine(string.Format("   Погрешность на ленте (u = 0): {0:P2}", bcError));
                energyMessage.AppendLine("   (для идеального проводника)");
            }
            else
            {
                energyMessage.AppendLine(string.Format("   Погрешность на ленте (u + χ*du/dn = 0): {0:P2}", bcError));
                energyMessage.AppendLine(string.Format("   χ = {0:F4} + {1:F4}i", solver.chi.Re, solver.chi.Im));
            }

            if (bcError < 0.05)
                energyMessage.AppendLine("   ✓ Условие выполняется отлично");
            else if (bcError < 0.15)
                energyMessage.AppendLine("   ✓ Условие выполняется хорошо");
            else
                energyMessage.AppendLine("   ⚠ Требуется увеличить N");
            energyMessage.AppendLine();

            // 2. Уравнение Гельмгольца (Закон распространения волны)
            double helmError = solver.VerifyHelmholtz();
            energyMessage.AppendLine("2. Уравнение Гельмгольца (Δu + k²u = 0):");
            energyMessage.AppendLine(string.Format("   Невязка в свободном пространстве: {0:E2}", helmError));

            if (helmError < 1e-6)
                energyMessage.AppendLine("   ✓ Уравнение выполняется с высокой точностью");
            else if (helmError < 1e-3)
                energyMessage.AppendLine("   ✓ Уравнение выполняется удовлетворительно");
            else
                energyMessage.AppendLine("   ⚠ Требуется увеличить M_quad");
            energyMessage.AppendLine();

            // 3. Энергетическая диагностика. Текущая формула отраженной энергии не является строгим ЗСЭ.
            energyMessage.AppendLine("3. Энергетическая диагностика (оценка, не строгий ЗСЭ):");
            energyMessage.AppendLine("   Важно: отражение сейчас оценивается по потоку через одну контрольную вертикаль,");
            energyMessage.AppendLine("   а не по полному дальнему полю или замкнутому контуру.");
            energyMessage.AppendLine(string.Format("   Падающая энергия:     {0:F6} (100.00%)", incidentEnergy));
            if (!energyReferenceValid)
                energyMessage.AppendLine("   Энергетические проценты не рассчитаны: опорная энергия близка к нулю.");
            energyMessage.AppendLine(string.Format("   Отраженная:           {0:F6} ({1})", reflectedEnergy, percentText(reflectedFraction)));
            energyMessage.AppendLine(string.Format("   Прошедшая:            {0:F6} ({1})", transmittedEnergy, percentText(transmittedFraction)));

            if (skinDepth > 0)
                energyMessage.AppendLine(string.Format("   Поглощенная:          {0:F6} ({1})", absorbedEnergy, percentText(absorbedFraction)));
            else
                energyMessage.AppendLine("   Поглощенная:          0.000000 (0.00%)");

            energyMessage.AppendLine(new string('-', 50));
            energyMessage.AppendLine(string.Format("   ИТОГО (расчетная сумма): {0:F6} ({1} от падающей)",
                sumRAT, percentText(sumFraction)));

            // Проверка энергетического баланса
            double balanceError = Math.Abs(incidentEnergy - sumRAT);
            double relativeError = energyReferenceValid ? balanceError / incidentEnergy : double.NaN;

            bool energyConservationOk = energyReferenceValid && relativeError < 0.10; // погрешность менее 10%

            energyMessage.AppendLine(string.Format("   Дисбаланс энергии:       {0}", percentText(relativeError)));

            if (energyConservationOk)
            {
                energyMessage.AppendLine("   ✓ Энергетическая оценка близка к балансу");
            }
            else if (!energyReferenceValid)
            {
                energyMessage.AppendLine("   ⚠ Энергетическая оценка недоступна для этой контрольной поверхности");
            }
            else
            {
                energyMessage.AppendLine("   ⚠ Энергетическая оценка нестабильна; это не доказывает ухудшение ГУ при росте N");
            }

            energyMessage.AppendLine();
            energyMessage.AppendLine("Физические проверки (физичность):");

            // Проверка физичности каждой компоненты
            bool allPositive = (reflectedEnergy >= -1e-10) &&
                               (transmittedEnergy >= -1e-10) &&
                               (absorbedEnergy >= -1e-10);

            if (allPositive)
            {
                energyMessage.AppendLine("✓ Отрицательных энергий не обнаружено");
            }
            else
            {
                energyMessage.AppendLine("✗ Обнаружены нефизичные (отрицательные) значения!");
                if (reflectedEnergy < 0)
                    energyMessage.AppendLine($"   Отраженная энергия отрицательна: {reflectedEnergy:F6}");
                if (transmittedEnergy < 0)
                    energyMessage.AppendLine($"   Прошедшая энергия отрицательна: {transmittedEnergy:F6}");
                if (absorbedEnergy < 0)
                    energyMessage.AppendLine($"   Поглощенная энергия отрицательна: {absorbedEnergy:F6}");
            }

            // Дополнительная проверка для идеального проводника
            if (skinDepth == 0)
            {
                energyMessage.AppendLine();
                energyMessage.AppendLine("Специальная проверка для идеального проводника:");
                if (Math.Abs(absorbedEnergy) < 1e-10)
                    energyMessage.AppendLine("✓ Поглощение отсутствует (как и должно быть)");
                else
                    energyMessage.AppendLine($"⚠ Поглощение должно быть 0, но получено: {absorbedEnergy:E6}");

                // Проверка: отраженная + прошедшая = падающая
                double sumRT = reflectedEnergy + transmittedEnergy;
                double rtError = energyReferenceValid ? Math.Abs(incidentEnergy - sumRT) / incidentEnergy : double.NaN;
                double rtFraction = energyReferenceValid ? sumRT / incidentEnergy : double.NaN;
                energyMessage.AppendLine(string.Format("   Отраженная + Прошедшая = {0:F6} ({1} от падающей)",
                    sumRT, percentText(rtFraction)));
                energyMessage.AppendLine(string.Format("   Отклонение расчетной энергетической оценки: {0}", percentText(rtError)));
            }

            energyMessage.AppendLine();
            energyMessage.AppendLine("========================================");
            energyMessage.AppendLine("Рекомендации:");

            if (bcError > 0.10)
                energyMessage.AppendLine("• Увеличьте N (параметр усечения) для улучшения граничных условий");
            if (helmError > 1e-3)
                energyMessage.AppendLine("• Увеличьте M_quad (число узлов квадратуры) для лучшей точности поля");
            if (!energyReferenceValid)
                energyMessage.AppendLine("• Для энергетики выберите угол/контрольную поверхность с ненулевой опорной энергией");
            else if (relativeError > 0.10)
                energyMessage.AppendLine("• Проверьте CSV-sweep и формулу энергетики: текущая оценка не является строгим ЗСЭ");

            if (bcError < 0.05 && helmError < 1e-3 && relativeError < 0.05)
                energyMessage.AppendLine("✓ Все проверки пройдены успешно! Решение физически корректно.");
            else if (bcError < 0.05 && helmError < 1e-3)
                energyMessage.AppendLine("✓ ГУ и уравнение Гельмгольца пройдены; энергетическая диагностика требует отдельной проверки.");

            MessageBoxIcon icon;
            bool localChecksOk = bcError < 0.10 && helmError < 1e-3;
            if (skinDepth == 0)
            {
                // Для идеального проводника
                icon = localChecksOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            }
            else
            {
                // Для проводника со скин-слоем
                icon = (bcError < 0.15 && helmError < 1e-3) ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            }

            string title = skinDepth == 0 ?
                "Контроль точности (идеальный проводник)" :
                "Контроль точности (скин-эффект)";

            return new AccuracyReport
            {
                Message = energyMessage.ToString(),
                Title = title,
                Icon = icon
            };
        }





        private void ShowAccuracyReport(AccuracyReport report)
        {
            MessageBox.Show(
                report.Message,
                report.Title,
                MessageBoxButtons.OK,
                report.Icon
            );
        }

        // Обработчик события нажатия кнопки button2
        private async void button2_Click(object sender, EventArgs e)
        {
            await OpenGraphicsFormAsync();
        }

        // Метод для создания изображения графика.
        private class GraphImagePair
        {
            public Bitmap ImageNoSkin;
            public Bitmap ImageSkin;
        }

        private GraphImagePair CreateGraphImages(PlateCalculationInput input, int width, int height, IProgress<string> progress)
        {
            progress.Report("Решение поля без скин-слоя...");
            DifrOnLenta qNoSkin = new DifrOnLenta(input.Alpha1, input.Beta1, input.Alpha2, input.Beta2, input.Len, input.Angle, input.Param, 0);
            if (qNoSkin.SolveDifr() != 1)
                return null;

            progress.Report("Решение поля со скин-слоем...");
            DifrOnLenta qSkin = new DifrOnLenta(input.Alpha1, input.Beta1, input.Alpha2, input.Beta2, input.Len, input.Angle, input.Param, input.SkinDepth);
            if (qSkin.SolveDifr() != 1)
                return null;

            return CreateGraphImages(input, width, height, qNoSkin, qSkin, progress);
        }

        private GraphImagePair CreateGraphImages(
            PlateCalculationInput input,
            int width,
            int height,
            DifrOnLenta qNoSkin,
            DifrOnLenta qSkin,
            IProgress<string> progress)
        {
            progress.Report("Расчет значений поля...");
            int pixelCount = width * height;
            double[] uNoSkin = new double[pixelCount];
            double[] uSkin = new double[pixelCount];

            double zEps = input.Len / 1000.0;

            Parallel.For(0, width, i =>
            {
                double x = input.X1 + i / (double)width * (input.X2 - input.X1);
                for (int j = 0; j < height; j++)
                {
                    double y = input.Y1 + j / (double)height * (input.Y2 - input.Y1);
                    double ySafe = y;
                    if (Math.Abs(y) < zEps && IsPointOnAnyPlate(x, input.Alpha1, input.Beta1, input.Alpha2, input.Beta2))
                        ySafe = (y >= 0) ? zEps : -zEps;

                    int index = j * width + i;
                    uNoSkin[index] = Compl.Abs(qNoSkin.u(x, ySafe));
                    uSkin[index] = Compl.Abs(qSkin.u(x, ySafe));
                }
            });

            double uMaxNoSkin = FindMax(uNoSkin);
            double uMaxSkin = FindMax(uSkin);

            progress.Report("Формирование изображений...");
            Bitmap imageNoSkin = CreateGrayscaleBitmap(uNoSkin, width, height, uMaxNoSkin);
            Bitmap imageSkin = CreateGrayscaleBitmap(uSkin, width, height, uMaxSkin);

            int yC = height / 2;
            DrawPlateMarker(imageNoSkin, input.Alpha1, input.Beta1, input.X1, input.X2, yC);
            DrawPlateMarker(imageNoSkin, input.Alpha2, input.Beta2, input.X1, input.X2, yC);
            DrawPlateMarker(imageSkin, input.Alpha1, input.Beta1, input.X1, input.X2, yC);
            DrawPlateMarker(imageSkin, input.Alpha2, input.Beta2, input.X1, input.X2, yC);

            return new GraphImagePair { ImageNoSkin = imageNoSkin, ImageSkin = imageSkin };
        }

        private static double FindMax(double[] values)
        {
            double max = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i]) && values[i] > max)
                    max = values[i];
            }
            return max > 0 ? max : 1.0;
        }

        private static Bitmap CreateGrayscaleBitmap(double[] values, int width, int height, double maxValue)
        {
            Bitmap image = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = image.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = data.Stride;
                byte[] bytes = new byte[stride * height];

                Parallel.For(0, height, y =>
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int valueIndex = y * width + x;
                        int color = ScaleToByte(values[valueIndex], maxValue);
                        int byteIndex = rowOffset + x * 3;
                        bytes[byteIndex] = (byte)color;
                        bytes[byteIndex + 1] = (byte)color;
                        bytes[byteIndex + 2] = (byte)color;
                    }
                });

                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                image.UnlockBits(data);
            }

            return image;
        }

        private static int ScaleToByte(double value, double maxValue)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || maxValue <= 0)
                return 0;
            int color = (int)(value / maxValue * 255.0);
            if (color < 0) return 0;
            if (color > 255) return 255;
            return color;
        }

        private static bool IsPointOnAnyPlate(double x, double alpha1, double beta1, double alpha2, double beta2)
        {
            return (x >= alpha1 && x <= beta1) || (x >= alpha2 && x <= beta2);
        }

        private void DrawPlateMarker(Bitmap image, double alphaValue, double betaValue, double xMin, double xMax, int yCenter)
        {
            if (Math.Abs(xMax - xMin) < 1e-12) return;

            int xStart = (int)((alphaValue - xMin) / (xMax - xMin) * image.Width);
            int xEnd = (int)((betaValue - xMin) / (xMax - xMin) * image.Width);
            if (xStart > xEnd)
            {
                int tmp = xStart;
                xStart = xEnd;
                xEnd = tmp;
            }
            if (xEnd < 0 || xStart >= image.Width) return;

            xStart = Math.Max(0, xStart);
            xEnd = Math.Min(image.Width - 1, xEnd);

            for (int x = xStart; x <= xEnd; x++)
                for (int dy = -1; dy <= 1; dy++)
                    SafeSetPixel(image, x, yCenter + dy, Color.Black);

            for (int dy = -4; dy <= 4; dy++)
            {
                SafeSetPixel(image, xStart, yCenter + dy, Color.Black);
                SafeSetPixel(image, xEnd, yCenter + dy, Color.Black);
            }
        }

        // Функция для безопасной установки пикселей
        private void SafeSetPixel(Bitmap image, int x, int y, Color color)
        {
            if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
            {
                image.SetPixel(x, y, color);
            }
        }
    }
}
