using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using ISCSI_Util.Helpers;
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
            this.Title = "iSCSI Util";
            
            DataContext = new MainWindowViewModel();
        }

        
        // Este evento se dispara cuando la ventana ya está visible
        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Llamar a tu método existente
            await SolicitarPassword();

            // Una vez guardada la contraseña en Credenciales.AdminPassword,
            // arrancar el demonio iscsid
            IscsiHelper.AsegurarServicioIscsid();
        }
        
        
        
        public async Task SolicitarPassword()
        {
           
                // Primero se crea el diálogo
                var dialog = new PasswordDialog();
dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
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