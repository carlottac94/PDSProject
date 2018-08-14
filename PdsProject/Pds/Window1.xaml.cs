using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Pds
{
    /// <summary>
    /// Logica di interazione per Window1.xaml
    /// </summary>
    /// window1->finestra che si apre dal logo, che permette di cambiare lo stato e di aprire le impostazioni
    public partial class Window1 : Window
    {
        public App application { get; set; }
        private static System.Windows.Forms.NotifyIcon notifyIcon; //icona dell'applicazione nella barra di stato
        private Window2 win2;
        private System.Windows.Forms.ContextMenu contextMenu; //menu contestuale per esci
        private System.Windows.Forms.MenuItem menuItem1; //il menu contestuale di esci ha una sola opzione

        public Window1(App app)
        {
            application = app;
            this.contextMenu = new System.Windows.Forms.ContextMenu();
            this.menuItem1 = new System.Windows.Forms.MenuItem();

            // Initialize contextMenu1
            this.contextMenu.MenuItems.AddRange(
                        new System.Windows.Forms.MenuItem[] { this.menuItem1 });

            // Initialize menuItem1
            this.menuItem1.Index = 0;
            this.menuItem1.Text = "Esci";
            this.menuItem1.Click += new System.EventHandler(this.menuItem1_Click); //associo al click su esci del menu contestuale
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += (s, args) => ShowMainWindow(); //quando clicco sull'icona voglio che si apra questa window
            notifyIcon.Icon = Properties.Resources.icon1; //associo l'immagine
            notifyIcon.Visible = true;
            notifyIcon.ContextMenu = this.contextMenu; //associo il menu contestuale al click col tasto destro sull'icona
            InitializeComponent();
        }

        private void menuItem1_Click(object sender, EventArgs e)
        { //click su esci
            Close();
            application.visible = false;
            //Thread.Sleep(2000);//aspetto che invii il messaggio di offline
            Application.Current.Shutdown();
        }

        private void StatusButtonClick(object sender, RoutedEventArgs e)
        { //click sul bottone offline/online
            if (StatusButton.Content.Equals("Stato: Offline")) //se lo stato é offline e ci clicco sopra diventa online
            {
                StatusButton.Content = "Stato: Online";
                Properties.Settings.Default["Status"] = "Online";
                System.Console.WriteLine("Online");
                application.visible = true;

            }
            else
            { //se lo stato é online e ci clicco sopra diventa offline
                StatusButton.Content = "Stato: Offline";
                Properties.Settings.Default["Status"] = "Offline";
                System.Console.WriteLine("Offline");
                application.visible = false;
            }
        }

      

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //se clicco sul link impostazioni di condivisione apro la window2
            //passo al costruttore di win2 l'ultima immagine impostata dall'utente
            if (application.default_image == false)
                win2 = new Window2(Pds.Properties.Settings.Default["ImagePath"].ToString(),application);
            else
                win2 = new Window2("../../default_user_image.jpg",application);
           // win2.appWin = application;
            win2.Show();
        }
        private void ShowMainWindow()
        {
            if (this.IsVisible && Topmost == true)
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }
                else if (WindowState == WindowState.Maximized)
                {
                    this.Hide();
                }
                Topmost = false;
                Hide();
            }
            else
            {
                Topmost = true;
                Show();
            }

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width;
            this.Top = desktopWorkingArea.Bottom - this.Height;
        }

        public static void ShowMessageBox(string message)
        {
            notifyIcon.BalloonTipTitle = "Notifica";
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(600);
        }










    }
}
