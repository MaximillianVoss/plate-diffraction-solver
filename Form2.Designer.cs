namespace Diffraction
{
    partial class Form2
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pictureBoxNoSkin = new System.Windows.Forms.PictureBox();
            this.pictureBoxSkin = new System.Windows.Forms.PictureBox();
            this.labelNoSkin = new System.Windows.Forms.Label();
            this.labelSkin = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxNoSkin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSkin)).BeginInit();
            this.SuspendLayout();
            //
            // pictureBoxNoSkin
            //
            this.pictureBoxNoSkin.Location = new System.Drawing.Point(12, 40);
            this.pictureBoxNoSkin.Name = "pictureBoxNoSkin";
            this.pictureBoxNoSkin.Size = new System.Drawing.Size(400, 400);
            this.pictureBoxNoSkin.TabIndex = 0;
            this.pictureBoxNoSkin.TabStop = false;
            //
            // pictureBoxSkin
            //
            this.pictureBoxSkin.Location = new System.Drawing.Point(420, 40);
            this.pictureBoxSkin.Name = "pictureBoxSkin";
            this.pictureBoxSkin.Size = new System.Drawing.Size(400, 400);
            this.pictureBoxSkin.TabIndex = 1;
            this.pictureBoxSkin.TabStop = false;
            //
            // labelNoSkin
            //
            this.labelNoSkin.AutoSize = true;
            this.labelNoSkin.Location = new System.Drawing.Point(12, 15);
            this.labelNoSkin.Name = "labelNoSkin";
            this.labelNoSkin.Size = new System.Drawing.Size(120, 16);
            this.labelNoSkin.TabIndex = 2;
            this.labelNoSkin.Text = "Поле без скин-слоя";
            //
            // labelSkin
            //
            this.labelSkin.AutoSize = true;
            this.labelSkin.Location = new System.Drawing.Point(420, 15);
            this.labelSkin.Name = "labelSkin";
            this.labelSkin.Size = new System.Drawing.Size(110, 16);
            this.labelSkin.TabIndex = 3;
            this.labelSkin.Text = "Поле со скин-слоем";
            //
            // Form2
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(832, 450);
            this.Controls.Add(this.labelSkin);
            this.Controls.Add(this.labelNoSkin);
            this.Controls.Add(this.pictureBoxSkin);
            this.Controls.Add(this.pictureBoxNoSkin);
            this.Name = "Form2";
            this.Text = "Form2";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxNoSkin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSkin)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // Изменение уровня доступа на internal
        internal System.Windows.Forms.PictureBox pictureBoxNoSkin;
        internal System.Windows.Forms.PictureBox pictureBoxSkin;
        internal System.Windows.Forms.Label labelNoSkin;
        internal System.Windows.Forms.Label labelSkin;
    }
}
