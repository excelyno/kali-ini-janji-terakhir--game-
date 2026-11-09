using kali_ini_janji_terakhir.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir
{
    public class GameForm : Form
    {
        public GameForm()
        {
            InitializeGameForm();

            GamePanel gamePanel = new GamePanel();
            Controls.Add(gamePanel);
        }

        private void InitializeGameForm()
        {
            Text = "Kali Ini Janji Terakhir - RPG";
            Size = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;

            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
        }
    }
}