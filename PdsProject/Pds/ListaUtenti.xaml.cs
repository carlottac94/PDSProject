using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
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
    /// Logica di interazione per ListaUtenti.xaml
    /// </summary>
    public partial class ListaUtenti : Window
    {
        public static ObservableCollection<User> _ListUsers=null;
        public App ParentWindow { get; set; }
        public string Path { get; set; } //path del file su cui l'utente clicca condividi con

   
        public static ObservableCollection<User> ListofUser //struttura dati dinamica che si aggiorna immediatamente
        {
            get
            {
                if(_ListUsers  == null)
                     _ListUsers = new ObservableCollection<User>();

                return _ListUsers;
            }
            set
            {
                _ListUsers = value;
            }
        }
        public ListaUtenti()
        {
            InitializeComponent();
            DataContext = this; //DataContext inizializzato in questo modo serve per poter fare il binding tra il layer dei dati e l'interfaccia
        }


        private void Button_Click_Condividi(Object sender, RoutedEventArgs e) { 
           if(Filelist.SelectedItems.Count == 0) //FileList è il nome assegnato all'oggeto ListBox dell'interfaccia 
            { //se l'utente clicca condividi senza aver cliccato prima su un utente
                MessageBox.Show("Nessun utente selezionato !");
            }
            else
            {
                Hide();
                try
                {
                    foreach (User curr in Filelist.SelectedItems) //posso selezionare anche più utenti contemporaneamente 
                    {
                        IPEndPoint ipDest = new IPEndPoint(IPAddress.Parse(curr.IPAddress), 17000);
                        Form2 f2 = new Form2(ipDest, Path); //form che si occupa dell'invio del file e mostra la barra di avanzamento

                    }
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Lista utenti selezionati per l'invio modificata. ");
                }
            }
        }
        private void Button_Click_Annulla(Object sender, RoutedEventArgs e)
        {
            Hide();
        }
        }
}
