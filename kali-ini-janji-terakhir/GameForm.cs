using kali_ini_janji_terakhir.Forms;

namespace kali_ini_janji_terakhir
{
    public class GameForm : Form
    {
        private readonly GamePanel panel = new();

        public GameForm()
        {
            Text = "Kali Ini Janji Terakhir - Kebun";
            ClientSize = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            KeyPreview = true;
            Controls.Add(panel);
            KeyDown += (_, e) => { panel.KeyDownEvt(e.KeyCode); e.Handled = true; e.SuppressKeyPress = true; };
            KeyUp += (_, e) => panel.KeyUpEvt(e.KeyCode);
            MouseWheel += (_, e) => panel.WheelEvt(e.Delta);
            panel.MouseWheel += (_, e) => panel.WheelEvt(e.Delta);
            Deactivate += (_, _) => panel.LoseFocusEvt();
        }
    }
}
