using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SpotifyOverlay
{
    public partial class FpsDialog : Window
    {
        public int SelectedFps { get; private set; } = 144;

        public FpsDialog(int currentFps)
        {
            InitializeComponent();
            SelectedFps = currentFps;
            FpsTextBox.Text = currentFps == 0 ? "Auto" : currentFps.ToString();
            FpsTextBox.Focus();
            FpsTextBox.SelectAll();
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                string text = btn.Content.ToString() ?? "";
                if (text == "Auto" || text == "Авто")
                {
                    FpsTextBox.Text = "Auto";
                }
                else
                {
                    FpsTextBox.Text = text;
                }
            }
        }

        private void FpsTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Save_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string text = FpsTextBox.Text.Trim();
            if (text.Equals("Auto", System.StringComparison.OrdinalIgnoreCase) || text.Equals("Авто", System.StringComparison.OrdinalIgnoreCase) || text == "0")
            {
                SelectedFps = 0; // 0 means Unlimited / Monitor VSync native
                DialogResult = true;
                Close();
            }
            else if (int.TryParse(text, out int fps) && fps >= 15 && fps <= 1000)
            {
                SelectedFps = fps;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Please enter a number between 15 and 1000 or 'Auto'", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
