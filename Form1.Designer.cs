namespace Trans2
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            richTextBox1 = new RichTextBox();
            dataGridView1 = new DataGridView();
            Id = new DataGridViewTextBoxColumn();
            TokenType = new DataGridViewTextBoxColumn();
            Value = new DataGridViewTextBoxColumn();
            treeView1 = new TreeView();
            comboBox1 = new ComboBox();
            treeView2 = new TreeView();
            richTextBox2 = new RichTextBox();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // richTextBox1
            // 
            richTextBox1.Location = new Point(12, 12);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(1010, 841);
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            richTextBox1.TextChanged += richTextBox1_TextChanged;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { Id, TokenType, Value });
            dataGridView1.Location = new Point(1042, 24);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.Size = new Size(775, 829);
            dataGridView1.TabIndex = 1;
            // 
            // Id
            // 
            Id.FillWeight = 200F;
            Id.HeaderText = "Id";
            Id.MinimumWidth = 6;
            Id.Name = "Id";
            Id.ReadOnly = true;
            Id.Width = 200;
            // 
            // TokenType
            // 
            TokenType.FillWeight = 200F;
            TokenType.HeaderText = "Token type";
            TokenType.MinimumWidth = 6;
            TokenType.Name = "TokenType";
            TokenType.ReadOnly = true;
            TokenType.Width = 200;
            // 
            // Value
            // 
            Value.FillWeight = 300F;
            Value.HeaderText = "Value";
            Value.MinimumWidth = 6;
            Value.Name = "Value";
            Value.ReadOnly = true;
            Value.Width = 300;
            // 
            // treeView1
            // 
            treeView1.Location = new Point(1042, 24);
            treeView1.Name = "treeView1";
            treeView1.Size = new Size(775, 829);
            treeView1.TabIndex = 3;
            treeView1.Visible = false;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Lexical analysis", "Syntax analysis", "Semantic analysis", "Interpretation" });
            comboBox1.Location = new Point(800, 880);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(222, 28);
            comboBox1.TabIndex = 4;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // treeView2
            // 
            treeView2.Location = new Point(1042, 24);
            treeView2.Name = "treeView2";
            treeView2.Size = new Size(775, 829);
            treeView2.TabIndex = 5;
            treeView2.Visible = false;
            // 
            // richTextBox2
            // 
            richTextBox2.Location = new Point(1042, 24);
            richTextBox2.Name = "richTextBox2";
            richTextBox2.Size = new Size(775, 829);
            richTextBox2.TabIndex = 6;
            richTextBox2.Text = "";
            richTextBox2.Visible = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1829, 938);
            Controls.Add(richTextBox2);
            Controls.Add(treeView2);
            Controls.Add(comboBox1);
            Controls.Add(treeView1);
            Controls.Add(dataGridView1);
            Controls.Add(richTextBox1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox richTextBox1;
        private DataGridView dataGridView1;
        private DataGridViewTextBoxColumn Id;
        private DataGridViewTextBoxColumn TokenType;
        private DataGridViewTextBoxColumn Value;
        private TreeView treeView1;
        private ComboBox comboBox1;
        private TreeView treeView2;
        private RichTextBox richTextBox2;
    }
}
