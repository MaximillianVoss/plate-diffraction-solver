using System.Drawing;

namespace Diffraction
{
    partial class MainForm
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        //private System.Windows.Forms.PictureBox pictureBox1;
        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.xL = new System.Windows.Forms.NumericUpDown();
            this.yDn = new System.Windows.Forms.NumericUpDown();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.yUp = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.xR = new System.Windows.Forms.NumericUpDown();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.truncationParameterN = new System.Windows.Forms.NumericUpDown();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.bandBoundaryB = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.bandBoundaryA = new System.Windows.Forms.NumericUpDown();
            this.label12 = new System.Windows.Forms.Label();
            this.bandBoundaryB2 = new System.Windows.Forms.NumericUpDown();
            this.label13 = new System.Windows.Forms.Label();
            this.bandBoundaryA2 = new System.Windows.Forms.NumericUpDown();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.angleInDegrees = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.wavelength = new System.Windows.Forms.NumericUpDown();
            this.textBoxChebPolynomial = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.CalculateButton = new System.Windows.Forms.Button();
            this.buttonGraphic = new System.Windows.Forms.Button();
            this.chartRealPart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.progressCalculation = new System.Windows.Forms.ProgressBar();
            this.labelCalculationStatus = new System.Windows.Forms.Label();
            this.checkBoxUseCuda = new System.Windows.Forms.CheckBox();
            this.labelExecutionTime = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.xL)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yDn)).BeginInit();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.yUp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.xR)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.truncationParameterN)).BeginInit();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryB)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryA)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryB2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryA2)).BeginInit();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.angleInDegrees)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.wavelength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartRealPart)).BeginInit();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 52);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "yDn";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 20);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(20, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "xL";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // xL
            //
            this.xL.Location = new System.Drawing.Point(51, 17);
            this.xL.Margin = new System.Windows.Forms.Padding(4);
            this.xL.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.xL.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.xL.Name = "xL";
            this.xL.Size = new System.Drawing.Size(105, 22);
            this.xL.TabIndex = 4;
            this.xL.Value = new decimal(new int[] {
            2,
            0,
            0,
            -2147483648});
            this.xL.ValueChanged += new System.EventHandler(this.numericUpDown1_ValueChanged);
            //
            // yDn
            //
            this.yDn.Location = new System.Drawing.Point(51, 49);
            this.yDn.Margin = new System.Windows.Forms.Padding(4);
            this.yDn.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.yDn.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.yDn.Name = "yDn";
            this.yDn.Size = new System.Drawing.Size(105, 22);
            this.yDn.TabIndex = 5;
            this.yDn.Value = new decimal(new int[] {
            3,
            0,
            0,
            -2147483648});
            this.yDn.ValueChanged += new System.EventHandler(this.numericUpDown2_ValueChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.yUp);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.xR);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.yDn);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.xL);
            this.groupBox1.Location = new System.Drawing.Point(16, 459);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(352, 81);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Параметры графика";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(191, 52);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(32, 16);
            this.label4.TabIndex = 8;
            this.label4.Text = "yUp";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // yUp
            // 
            this.yUp.Location = new System.Drawing.Point(233, 49);
            this.yUp.Margin = new System.Windows.Forms.Padding(4);
            this.yUp.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.yUp.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.yUp.Name = "yUp";
            this.yUp.Size = new System.Drawing.Size(105, 22);
            this.yUp.TabIndex = 9;
            this.yUp.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.yUp.ValueChanged += new System.EventHandler(this.numericUpDown4_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(191, 20);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(23, 16);
            this.label3.TabIndex = 6;
            this.label3.Text = "xR";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xR
            // 
            this.xR.Location = new System.Drawing.Point(233, 17);
            this.xR.Margin = new System.Windows.Forms.Padding(4);
            this.xR.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.xR.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.xR.Name = "xR";
            this.xR.Size = new System.Drawing.Size(105, 22);
            this.xR.TabIndex = 7;
            this.xR.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.xR.ValueChanged += new System.EventHandler(this.numericUpDown3_ValueChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.truncationParameterN);
            this.groupBox2.Location = new System.Drawing.Point(376, 459);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox2.Size = new System.Drawing.Size(168, 81);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Параметр усечения";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(8, 38);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(24, 16);
            this.label7.TabIndex = 2;
            this.label7.Text = "N=";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // truncationParameterN
            // 
            this.truncationParameterN.Location = new System.Drawing.Point(55, 36);
            this.truncationParameterN.Margin = new System.Windows.Forms.Padding(4);
            this.truncationParameterN.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.truncationParameterN.Name = "truncationParameterN";
            this.truncationParameterN.Size = new System.Drawing.Size(105, 22);
            this.truncationParameterN.TabIndex = 4;
            this.truncationParameterN.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.truncationParameterN.ValueChanged += new System.EventHandler(this.numericUpDown8_ValueChanged);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label12);
            this.groupBox3.Controls.Add(this.bandBoundaryB2);
            this.groupBox3.Controls.Add(this.label13);
            this.groupBox3.Controls.Add(this.bandBoundaryA2);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.bandBoundaryB);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.bandBoundaryA);
            this.groupBox3.Location = new System.Drawing.Point(552, 459);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox3.Size = new System.Drawing.Size(325, 81);
            this.groupBox3.TabIndex = 11;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Границы пластин";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(165, 25);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(47, 16);
            this.label6.TabIndex = 5;
            this.label6.Text = "beta1=";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // bandBoundaryB
            // 
            this.bandBoundaryB.DecimalPlaces = 3;
            this.bandBoundaryB.Increment = 0.1M;
            this.bandBoundaryB.Location = new System.Drawing.Point(220, 23);
            this.bandBoundaryB.Margin = new System.Windows.Forms.Padding(4);
            this.bandBoundaryB.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.bandBoundaryB.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.bandBoundaryB.Name = "bandBoundaryB";
            this.bandBoundaryB.Size = new System.Drawing.Size(90, 22);
            this.bandBoundaryB.TabIndex = 6;
            this.bandBoundaryB.Value = -0.5M;
            this.bandBoundaryB.ValueChanged += new System.EventHandler(this.numericUpDown6_ValueChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(8, 25);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(53, 16);
            this.label5.TabIndex = 2;
            this.label5.Text = "alpha1=";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // bandBoundaryA
            // 
            this.bandBoundaryA.DecimalPlaces = 3;
            this.bandBoundaryA.Increment = 0.1M;
            this.bandBoundaryA.Location = new System.Drawing.Point(67, 23);
            this.bandBoundaryA.Margin = new System.Windows.Forms.Padding(4);
            this.bandBoundaryA.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.bandBoundaryA.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.bandBoundaryA.Name = "bandBoundaryA";
            this.bandBoundaryA.Size = new System.Drawing.Size(90, 22);
            this.bandBoundaryA.TabIndex = 4;
            this.bandBoundaryA.Value = -1.5M;
            this.bandBoundaryA.ValueChanged += new System.EventHandler(this.numericUpDown5_ValueChanged);
            //
            // label12
            //
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(165, 54);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(47, 16);
            this.label12.TabIndex = 9;
            this.label12.Text = "beta2=";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // bandBoundaryB2
            //
            this.bandBoundaryB2.DecimalPlaces = 3;
            this.bandBoundaryB2.Increment = 0.1M;
            this.bandBoundaryB2.Location = new System.Drawing.Point(220, 52);
            this.bandBoundaryB2.Margin = new System.Windows.Forms.Padding(4);
            this.bandBoundaryB2.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.bandBoundaryB2.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.bandBoundaryB2.Name = "bandBoundaryB2";
            this.bandBoundaryB2.Size = new System.Drawing.Size(90, 22);
            this.bandBoundaryB2.TabIndex = 10;
            this.bandBoundaryB2.Value = 1.5M;
            this.bandBoundaryB2.ValueChanged += new System.EventHandler(this.numericUpDown10_ValueChanged);
            //
            // label13
            //
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(8, 54);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(53, 16);
            this.label13.TabIndex = 7;
            this.label13.Text = "alpha2=";
            this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // bandBoundaryA2
            //
            this.bandBoundaryA2.DecimalPlaces = 3;
            this.bandBoundaryA2.Increment = 0.1M;
            this.bandBoundaryA2.Location = new System.Drawing.Point(67, 52);
            this.bandBoundaryA2.Margin = new System.Windows.Forms.Padding(4);
            this.bandBoundaryA2.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.bandBoundaryA2.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.bandBoundaryA2.Name = "bandBoundaryA2";
            this.bandBoundaryA2.Size = new System.Drawing.Size(90, 22);
            this.bandBoundaryA2.TabIndex = 8;
            this.bandBoundaryA2.Value = 0.5M;
            this.bandBoundaryA2.ValueChanged += new System.EventHandler(this.numericUpDown9_ValueChanged);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label9);
            this.groupBox4.Controls.Add(this.angleInDegrees);
            this.groupBox4.Controls.Add(this.label8);
            this.groupBox4.Controls.Add(this.wavelength);
            this.groupBox4.Location = new System.Drawing.Point(885, 459);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox4.Size = new System.Drawing.Size(276, 81);
            this.groupBox4.TabIndex = 11;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Параметры волны";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(8, 52);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(114, 16);
            this.label9.TabIndex = 5;
            this.label9.Text = "Угол падения в °";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // angleInDegrees
            // 
            this.angleInDegrees.Location = new System.Drawing.Point(157, 49);
            this.angleInDegrees.Margin = new System.Windows.Forms.Padding(4);
            this.angleInDegrees.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.angleInDegrees.Name = "angleInDegrees";
            this.angleInDegrees.Size = new System.Drawing.Size(105, 22);
            this.angleInDegrees.TabIndex = 6;
            this.angleInDegrees.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(8, 20);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(132, 16);
            this.label8.TabIndex = 2;
            this.label8.Text = "Норм. длина волны";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // wavelength
            // 
            this.wavelength.Location = new System.Drawing.Point(157, 17);
            this.wavelength.Margin = new System.Windows.Forms.Padding(4);
            this.wavelength.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.wavelength.Name = "wavelength";
            this.wavelength.Size = new System.Drawing.Size(105, 22);
            this.wavelength.TabIndex = 4;
            this.wavelength.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.wavelength.ValueChanged += new System.EventHandler(this.numericUpDown7_ValueChanged);
            // 
            // textBoxChebPolynomial
            // 
            this.textBoxChebPolynomial.Location = new System.Drawing.Point(885, 31);
            this.textBoxChebPolynomial.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxChebPolynomial.Multiline = true;
            this.textBoxChebPolynomial.Name = "textBoxChebPolynomial";
            this.textBoxChebPolynomial.ReadOnly = true;
            this.textBoxChebPolynomial.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxChebPolynomial.Size = new System.Drawing.Size(275, 420);
            this.textBoxChebPolynomial.TabIndex = 12;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(912, 11);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(208, 16);
            this.label10.TabIndex = 13;
            this.label10.Text = "Коэф. разложения по пол. Чеб.";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(269, 11);
            this.label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(261, 16);
            this.label11.TabIndex = 14;
            this.label11.Text = "Вещественная часть искомой функции";
            // 
            // CalculateButton
            // 
            this.CalculateButton.Location = new System.Drawing.Point(16, 5);
            this.CalculateButton.Margin = new System.Windows.Forms.Padding(4);
            this.CalculateButton.Name = "CalculateButton";
            this.CalculateButton.Size = new System.Drawing.Size(100, 28);
            this.CalculateButton.TabIndex = 16;
            this.CalculateButton.Text = "Рассчитать";
            this.CalculateButton.UseVisualStyleBackColor = true;
            this.CalculateButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // buttonGraphic
            // 
            this.buttonGraphic.Location = new System.Drawing.Point(124, 5);
            this.buttonGraphic.Margin = new System.Windows.Forms.Padding(4);
            this.buttonGraphic.Name = "buttonGraphic";
            this.buttonGraphic.Size = new System.Drawing.Size(100, 28);
            this.buttonGraphic.TabIndex = 17;
            this.buttonGraphic.Text = "Графики";
            this.buttonGraphic.UseVisualStyleBackColor = true;
            this.buttonGraphic.Click += new System.EventHandler(this.button2_Click);
            //
            // checkBoxUseCuda
            //
            this.checkBoxUseCuda.AutoSize = true;
            this.checkBoxUseCuda.Checked = true;
            this.checkBoxUseCuda.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxUseCuda.Location = new System.Drawing.Point(236, 10);
            this.checkBoxUseCuda.Name = "checkBoxUseCuda";
            this.checkBoxUseCuda.Size = new System.Drawing.Size(135, 20);
            this.checkBoxUseCuda.TabIndex = 18;
            this.checkBoxUseCuda.Text = "Использовать CUDA";
            this.checkBoxUseCuda.UseVisualStyleBackColor = true;
            // 
            // chartRealPart
            // 
            chartArea1.Name = "ChartArea1";
            this.chartRealPart.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            this.chartRealPart.Legends.Add(legend1);
            this.chartRealPart.Location = new System.Drawing.Point(16, 41);
            this.chartRealPart.Margin = new System.Windows.Forms.Padding(4);
            this.chartRealPart.Name = "chartRealPart";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            series1.Legend = "Legend1";
            series1.Name = "fun(x)";
            series1.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            series1.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.chartRealPart.Series.Add(series1);
            this.chartRealPart.Size = new System.Drawing.Size(861, 411);
            this.chartRealPart.TabIndex = 18;
            this.chartRealPart.Text = "chart1";
            // Примечание: вторая ChartArea добавляется программно в конструкторе MainForm()

            // 
            // skinDepthInput
            // 
            this.skinDepthInput = new System.Windows.Forms.NumericUpDown();
            this.skinDepthInput.DecimalPlaces = 6;
            this.skinDepthInput.Increment = 0.0001m;
            this.skinDepthInput.Location = new System.Drawing.Point(120, 20);
            this.skinDepthInput.Name = "skinDepthInput";
            this.skinDepthInput.Size = new System.Drawing.Size(120, 22);
            this.skinDepthInput.Minimum = 0;
            this.skinDepthInput.Maximum = 10;
            this.skinDepthInput.Value = 0.100000m; // Значение по умолчанию
                                              // 
                                              // labelSkin
                                              // 
            this.labelSkin = new System.Windows.Forms.Label();
            this.labelSkin.AutoSize = true;
            this.labelSkin.Location = new System.Drawing.Point(10, 22);
            this.labelSkin.Name = "labelSkin";
            this.labelSkin.Text = "Толщина:";
            // 
            // groupBoxSkin
            // 
            this.groupBoxSkin = new System.Windows.Forms.GroupBox();
            this.groupBoxSkin.Controls.Add(this.labelSkin);
            this.groupBoxSkin.Controls.Add(this.skinDepthInput);
            this.groupBoxSkin.Location = new System.Drawing.Point(16, 550); // Позиция под другими GroupBox
            this.groupBoxSkin.Name = "groupBoxSkin";
            this.groupBoxSkin.Size = new System.Drawing.Size(250, 60);
            this.groupBoxSkin.Text = "Параметры скин-слоя";
            // 
            // Добавление groupBoxSkin на форму
            // 
            this.Controls.Add(this.groupBoxSkin);
            // 
            // lblConductivity
            // 
            this.lblConductivity = new System.Windows.Forms.Label();
            this.lblConductivity.AutoSize = true;
            this.lblConductivity.Location = new System.Drawing.Point(900, 550);
            this.lblConductivity.Name = "lblConductivity";
            this.lblConductivity.Size = new System.Drawing.Size(200, 16);
            this.lblConductivity.Text = "Проводимость: не рассчитана";
            this.Controls.Add(this.lblConductivity);
            //
            // progressCalculation
            //
            this.progressCalculation.Location = new System.Drawing.Point(280, 552);
            this.progressCalculation.MarqueeAnimationSpeed = 0;
            this.progressCalculation.Name = "progressCalculation";
            this.progressCalculation.Size = new System.Drawing.Size(590, 18);
            this.progressCalculation.Style = System.Windows.Forms.ProgressBarStyle.Blocks;
            this.progressCalculation.TabIndex = 19;
            this.progressCalculation.Visible = false;
            //
            // labelCalculationStatus
            //
            this.labelCalculationStatus.AutoSize = true;
            this.labelCalculationStatus.Location = new System.Drawing.Point(280, 580);
            this.labelCalculationStatus.Name = "labelCalculationStatus";
            this.labelCalculationStatus.Size = new System.Drawing.Size(45, 16);
            this.labelCalculationStatus.TabIndex = 20;
            this.labelCalculationStatus.Text = "Готово";
            //
            // labelExecutionTime
            //
            this.labelExecutionTime.AutoSize = true;
            this.labelExecutionTime.Location = new System.Drawing.Point(280, 604);
            this.labelExecutionTime.Name = "labelExecutionTime";
            this.labelExecutionTime.Size = new System.Drawing.Size(121, 16);
            this.labelExecutionTime.TabIndex = 21;
            this.labelExecutionTime.Text = "Время решения: н/д";

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1171, 650);
            this.Controls.Add(this.labelExecutionTime);
            this.Controls.Add(this.checkBoxUseCuda);
            this.Controls.Add(this.labelCalculationStatus);
            this.Controls.Add(this.progressCalculation);
            this.Controls.Add(this.chartRealPart);
            this.Controls.Add(this.buttonGraphic);
            this.Controls.Add(this.CalculateButton);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.textBoxChebPolynomial);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MainForm";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.xL)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yDn)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.yUp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.xR)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.truncationParameterN)).EndInit();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryB)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryA)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryB2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandBoundaryA2)).EndInit();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.angleInDegrees)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.wavelength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartRealPart)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown xL;
        private System.Windows.Forms.NumericUpDown yDn;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown yUp;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown xR;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown truncationParameterN;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown bandBoundaryB;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown bandBoundaryA;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.NumericUpDown bandBoundaryB2;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown bandBoundaryA2;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown wavelength;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.NumericUpDown angleInDegrees;
        private System.Windows.Forms.TextBox textBoxChebPolynomial;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button CalculateButton;
        private System.Windows.Forms.Button buttonGraphic;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartRealPart;
        private System.Windows.Forms.NumericUpDown skinDepthInput;
        private System.Windows.Forms.Label labelSkin;
        private System.Windows.Forms.GroupBox groupBoxSkin;
        private System.Windows.Forms.Label lblConductivity;
        private System.Windows.Forms.ProgressBar progressCalculation;
        private System.Windows.Forms.Label labelCalculationStatus;
        private System.Windows.Forms.CheckBox checkBoxUseCuda;
        private System.Windows.Forms.Label labelExecutionTime;
    }
}
