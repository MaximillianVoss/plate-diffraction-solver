using System;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Text;
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
        private void MainForm_Shown(object sender, EventArgs e)
        {
            // Автоматически открываем форму с графиками
            OpenGraphicsForm();
        }

        // Метод для открытия формы с графиками (рефакторинг button2_Click)
        private void OpenGraphicsForm()
        {
            // Повторно используем Form2, если она уже открыта
            if (currentForm2 == null || currentForm2.IsDisposed)
            {
                currentForm2 = new Form2();
            }

            // Создание изображений графиков с помощью метода CreateGraphImages
            GraphImagePair images = CreateGraphImages(currentForm2);

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
        private void button1_Click(object sender, EventArgs e)
        {
            // Очистка предыдущих данных
            chartRealPart.Series[0].Points.Clear(); // Без скин-слоя
            chartRealPart.Series[1].Points.Clear(); // Со скин-слоем
            textBoxChebPolynomial.Clear();

            // Параметры задачи
            int param = (int)truncationParameterN.Value;
            double alpha1, beta1, alpha2, beta2;
            if (!TryReadPlateParameters(out alpha1, out beta1, out alpha2, out beta2))
                return;

            double plotLeft = Math.Min(alpha1, alpha2);
            double plotRight = Math.Max(beta1, beta2);
            double angle = (double)angleInDegrees.Value / 180 * Math.PI;
            double len = (double)wavelength.Value;
            double skinDepth = (double)skinDepthInput.Value;

            // Решение БЕЗ скин-слоя (skinDepth = 0)
            DifrOnLenta qNoSkin = new DifrOnLenta(alpha1, beta1, alpha2, beta2, len, angle, param, 0);
            bool noSkinSolved = false;

            // z-смещение от поверхности для избежания сингулярности H0 при z=0
            double z_plot = len / 10.0;

            try
            {
                if (qNoSkin.SolveDifr() == 1)
                {
                    noSkinSolved = true;
                    // Построение графика для случая без скин-слоя
                    double h = (plotRight - plotLeft) / 1000, x = plotLeft;
                    while (x <= plotRight)
                    {
                        chartRealPart.Series[0].Points.AddXY(x, qNoSkin.u(x, z_plot).Re);
                        x += h;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("Ошибка решения без скин-слоя: {0}", ex.Message),
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Решение С УЧЕТОМ скин-слоя
            DifrOnLenta qSkin = new DifrOnLenta(alpha1, beta1, alpha2, beta2, len, angle, param, skinDepth);
            try
            {
                if (qSkin.SolveDifr() == 1)
                {
                    // Вывод в текстовое поле коэффициентов разложения по полиномам Чебышева
                    textBoxChebPolynomial.Clear();

                    // Заголовок с информацией о коэффициенте χ
                    textBoxChebPolynomial.Text += "Импедансный коэффициент χ:" + Environment.NewLine;
                    textBoxChebPolynomial.Text += string.Format("χ = {0:F6} + {1:F6}i{2}", qSkin.chi.Re, qSkin.chi.Im, Environment.NewLine);
                    textBoxChebPolynomial.Text += Environment.NewLine;

                    // Вывод ВСЕХ коэффициентов Чебышева для каждой пластины
                    int totalCoefficients = param * qSkin.PlateCount;
                    textBoxChebPolynomial.Text += string.Format("Коэффициенты Чебышева (всего {0}, по {1} на пластину):{2}",
                        totalCoefficients, param, Environment.NewLine);
                    textBoxChebPolynomial.Text += string.Format("{0,-10} {1,-4} {2,-25} {3,-25}{4}", "Пластина", "#", "БЕЗ скин-слоя", "СО скин-слоем", Environment.NewLine);
                    textBoxChebPolynomial.Text += new string('.', 76) + Environment.NewLine;

                    for (int plateIndex = 0; plateIndex < qSkin.PlateCount; plateIndex++)
                    {
                        for (int coeffIndex = 0; coeffIndex < param; coeffIndex++)
                        {
                            int globalIndex = plateIndex * param + coeffIndex;
                            string noSkinCoeff = noSkinSolved
                                ? string.Format("{0:F4}+{1:F4}i", qNoSkin.y[globalIndex].Re, qNoSkin.y[globalIndex].Im)
                                : "нет решения";
                            string skinCoeff = string.Format("{0:F4}+{1:F4}i", qSkin.y[globalIndex].Re, qSkin.y[globalIndex].Im);
                            textBoxChebPolynomial.Text += string.Format("{0,-10} {1,-4} {2,-25} {3,-25}{4}",
                                plateIndex + 1, coeffIndex + 1, noSkinCoeff, skinCoeff, Environment.NewLine);
                        }
                    }

                    // Построение графика для случая со скин-слоем
                    double h = (plotRight - plotLeft) / 1000, x = plotLeft;
                    while (x <= plotRight)
                    {
                        chartRealPart.Series[1].Points.AddXY(x, qSkin.u(x, z_plot).Re);
                        x += h;
                    }

                    // Расчет проводимости материала (только при ненулевой толщине скин-слоя)
                    if (skinDepth > 0)
                    {
                        try
                        {
                            double skinDepth_m = skinDepth;
                            double wavelength_m = len;
                            double conductivity = qSkin.CalculateConductivity(skinDepth_m, wavelength_m);

                            lblConductivity.Text = string.Format("Проводимость: {0:E2} См/м", conductivity);
                            lblConductivity.ForeColor = Color.DarkBlue;
                        }
                        catch (Exception ex)
                        {
                            lblConductivity.Text = string.Format("Ошибка: {0}", ex.Message);
                            lblConductivity.ForeColor = Color.Red;
                        }
                    }
                    else
                    {
                        lblConductivity.Text = "Проводимость: не рассчитана (идеальный проводник)";
                        lblConductivity.ForeColor = Color.Gray;
                    }

                    // ========== ВЫВОД ДЛЯ СЛУЧАЯ БЕЗ СКИН-СЛОЯ ==========
                    if (noSkinSolved)
                    {
                        ShowAccuracyReport(qNoSkin, skinDepth: 0, caseName: "БЕЗ СКИН-СЛОЯ (идеальный проводник)");
                    }

                    // ========== ВЫВОД ДЛЯ СЛУЧАЯ СО СКИН-СЛОЕМ ==========
                    ShowAccuracyReport(qSkin, skinDepth, caseName: "СО СКИН-СЛОЕМ");
                }
                else
                {
                    MessageBox.Show(
                        "Ошибка решения задачи с учетом скин-слоя!",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("Ошибка при решении со скин-слоем: {0}", ex.Message),
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Обновляем черно-белое ЭМ поле после пересчета
            if (currentForm2 != null && !currentForm2.IsDisposed && currentForm2.Visible)
            {
                GraphImagePair images = CreateGraphImages(currentForm2);
                if (images != null)
                {
                    currentForm2.pictureBoxNoSkin.Image = images.ImageNoSkin;
                    currentForm2.pictureBoxSkin.Image = images.ImageSkin;
                    currentForm2.Refresh();
                }
            }
        }

        // Новый метод для отображения отчета о точности
        private void ShowAccuracyReport(DifrOnLenta solver, double skinDepth, string caseName)
        {
            // Расчет энергий
            var energyComp = solver.CalculateEnergyComponents();

            double incidentEnergy = energyComp.Incident;
            double reflectedEnergy = energyComp.Reflected;
            double transmittedEnergy = energyComp.Transmitted;
            double absorbedEnergy = energyComp.Absorbed;

            // Относительные доли энергии
            double reflectedFraction = reflectedEnergy / incidentEnergy;
            double transmittedFraction = transmittedEnergy / incidentEnergy;
            double absorbedFraction = absorbedEnergy / incidentEnergy;

            double sumRAT = reflectedEnergy + absorbedEnergy + transmittedEnergy;

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

            // 3. Энергетический баланс (Закон сохранения энергии)
            energyMessage.AppendLine("3. Энергетический баланс (закон сохранения энергии):");
            energyMessage.AppendLine(string.Format("   Падающая энергия:     {0:F6} (100.00%)", incidentEnergy));
            energyMessage.AppendLine(string.Format("   Отраженная:           {0:F6} ({1:P2})", reflectedEnergy, reflectedFraction));
            energyMessage.AppendLine(string.Format("   Прошедшая:            {0:F6} ({1:P2})", transmittedEnergy, transmittedFraction));

            if (skinDepth > 0)
                energyMessage.AppendLine(string.Format("   Поглощенная:          {0:F6} ({1:P2})", absorbedEnergy, absorbedFraction));
            else
                energyMessage.AppendLine("   Поглощенная:          0.000000 (0.00%)");

            energyMessage.AppendLine(new string('-', 50));
            energyMessage.AppendLine(string.Format("   ИТОГО (расчетная сумма): {0:F6} ({1:P2} от падающей)",
                sumRAT, sumRAT / incidentEnergy));

            // Проверка энергетического баланса
            double balanceError = Math.Abs(incidentEnergy - sumRAT);
            double relativeError = balanceError / Math.Max(incidentEnergy, 1e-10);

            bool energyConservationOk = relativeError < 0.10; // погрешность менее 10%

            energyMessage.AppendLine(string.Format("   Дисбаланс энергии:       {0:P2}", relativeError));

            if (energyConservationOk)
            {
                energyMessage.AppendLine("   ✓ ЗСЭ выполняется в пределах численной погрешности");
            }
            else
            {
                energyMessage.AppendLine("   ⚠ ЗСЭ нарушен: требуется увеличить N или M_quad");
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
                double rtError = Math.Abs(incidentEnergy - sumRT) / incidentEnergy;
                energyMessage.AppendLine(string.Format("   Отраженная + Прошедшая = {0:F6} ({1:P2} от падающей)",
                    sumRT, sumRT / incidentEnergy));
                energyMessage.AppendLine(string.Format("   Отклонение от ЗСЭ: {0:P2}", rtError));
            }

            energyMessage.AppendLine();
            energyMessage.AppendLine("========================================");
            energyMessage.AppendLine("Рекомендации:");

            if (bcError > 0.10)
                energyMessage.AppendLine("• Увеличьте N (параметр усечения) для улучшения граничных условий");
            if (helmError > 1e-3)
                energyMessage.AppendLine("• Увеличьте M_quad (число узлов квадратуры) для лучшей точности поля");
            if (relativeError > 0.10)
                energyMessage.AppendLine("• Уточните параметры интегрирования для лучшего энергобаланса");

            if (bcError < 0.05 && helmError < 1e-3 && relativeError < 0.05)
                energyMessage.AppendLine("✓ Все проверки пройдены успешно! Решение физически корректно.");

            MessageBoxIcon icon;
            if (skinDepth == 0)
            {
                // Для идеального проводника
                icon = (relativeError < 0.05 && bcError < 0.10) ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            }
            else
            {
                // Для проводника со скин-слоем
                icon = (energyConservationOk && bcError < 0.15) ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            }

            string title = skinDepth == 0 ?
                "Контроль точности (идеальный проводник)" :
                "Контроль точности (скин-эффект)";

            MessageBox.Show(
                energyMessage.ToString(),
                title,
                MessageBoxButtons.OK,
                icon
            );
        }





        // Обработчик события нажатия кнопки button2
        private void button2_Click(object sender, EventArgs e)
        {
            OpenGraphicsForm();
        }


        // Метод для создания изображения графика.
        private class GraphImagePair
        {
            public Bitmap ImageNoSkin;
            public Bitmap ImageSkin;
        }

        private GraphImagePair CreateGraphImages(Form2 form2)
        {
            // Создание экземпляра класса DifrOnLenta с параметрами, заданными значениями числовых полей
            double y1 = (double)yDn.Value, y2 = (double)yUp.Value;
            double x1 = (double)xL.Value, x2 = (double)xR.Value;
            int param = (int)truncationParameterN.Value;
            double alpha1, beta1, alpha2, beta2;
            if (!TryReadPlateParameters(out alpha1, out beta1, out alpha2, out beta2))
                return null;
            double angle = (double)angleInDegrees.Value / 180 * Math.PI;
            double len = (double)wavelength.Value;
            double skinDepth = (double)skinDepthInput.Value;

            // Решение без скин-слоя
            DifrOnLenta qNoSkin = new DifrOnLenta(alpha1, beta1, alpha2, beta2, len, angle, param, 0);
            if (qNoSkin.SolveDifr() != 1)
            {
                return null;
            }

            // Решение со скин-слоем
            DifrOnLenta qSkin = new DifrOnLenta(alpha1, beta1, alpha2, beta2, len, angle, param, skinDepth);
            if (qSkin.SolveDifr() != 1)
            {
                return null;
            }

            // Создание массива значений функции u(x, y) на плоскости (x, y).
            int w = form2.pictureBoxNoSkin.Width;
            int h = form2.pictureBoxNoSkin.Height;

            double[,] uNoSkin = new double[w, h];
            double[,] uSkin = new double[w, h];

            double uMaxNoSkin = 0;
            double uMaxSkin = 0;

            double z_eps = len / 1000.0; // минимальное смещение от поверхности
            for (int i = 0; i < w; i++)
            {
                double x = x1 + i / (double)w * (x2 - x1);
                for (int j = 0; j < h; j++)
                {
                    double y = y1 + j / (double)h * (y2 - y1);
                    // Избегаем z=0 на полоске для устранения сингулярности H0
                    double y_safe = y;
                    if (Math.Abs(y) < z_eps && IsPointOnAnyPlate(x, alpha1, beta1, alpha2, beta2))
                        y_safe = (y >= 0) ? z_eps : -z_eps;
                    uNoSkin[i, j] = Compl.Abs(qNoSkin.u(x, y_safe));
                    uSkin[i, j] = Compl.Abs(qSkin.u(x, y_safe));
                    if (uMaxNoSkin < uNoSkin[i, j])
                        uMaxNoSkin = uNoSkin[i, j];
                    if (uMaxSkin < uSkin[i, j])
                        uMaxSkin = uSkin[i, j];
                }
            }

            // Создание изображений графиков.
            Bitmap imageNoSkin = new Bitmap(w, h);
            Bitmap imageSkin = new Bitmap(w, h);

            for (int i = 1; i < w; i++)
            {
                for (int j = 1; j < h; j++)
                {
                    int colNoSkin = (int)(uNoSkin[i, j] / uMaxNoSkin * 255);
                    int colSkin = (int)(uSkin[i, j] / uMaxSkin * 255);
                    Color colorNoSkin = Color.FromArgb(colNoSkin, colNoSkin, colNoSkin);
                    Color colorSkin = Color.FromArgb(colSkin, colSkin, colSkin);

                    // Рисуем пиксели для поля без скин-слоя и со скин-слоем
                    imageNoSkin.SetPixel(i, j, colorNoSkin);
                    imageSkin.SetPixel(i, j, colorSkin);
                }
            }

            // Добавление на изображение маркеров пластин.
            int yC = h / 2;

            DrawPlateMarker(imageNoSkin, alpha1, beta1, x1, x2, yC);
            DrawPlateMarker(imageNoSkin, alpha2, beta2, x1, x2, yC);
            DrawPlateMarker(imageSkin, alpha1, beta1, x1, x2, yC);
            DrawPlateMarker(imageSkin, alpha2, beta2, x1, x2, yC);

            return new GraphImagePair { ImageNoSkin = imageNoSkin, ImageSkin = imageSkin };
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
