using System.Threading.Tasks;
using Avalonia.Controls;
using ISCSI_Util.Utils;
using ISCSI_Util.ViewModels;

namespace ISCSI_Util.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Width = 500;
            this.Height = 580;
            this.MinHeight = 580;
            this.MinWidth = 500;
            this.MaxHeight = 580;
            this.MaxWidth = 500;

            DataContext = new MainWindowViewModel();
        }

        public async Task SolicitarPassword()
        {
            if (string.IsNullOrEmpty(Credenciales.AdminPassword))
            {
                // Primero se crea el diálogo
                var dialog = new PasswordDialog();

                // Luego se asigna el DataContext
                dialog.DataContext = new PasswordDialogViewModel(pass =>
                {
                    Credenciales.AdminPassword = pass;
                    dialog.Close();
                });

                await dialog.ShowDialog(this);
            }
        }
    }
}