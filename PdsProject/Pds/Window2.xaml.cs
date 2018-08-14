using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.IO;

namespace Pds
{
    /// <summary>
    /// Logica di interazione per Window2.xaml
    /// window2->finestar delle impostazioni di condivisione
    /// </summary>
    public partial class Window2 : Window
    {
        private String ImagePath = string.Empty;
        public App appWin { get; set; }
        public string myPath;
        
       
        public Window2(string path,App application)
        {

            myPath = path;
            appWin = application;
            InitializeComponent();
            if (Properties.Settings.Default["AutoActivated"].Equals("YES"))
                AutoCheckBox.IsChecked = true;
            else
                AutoCheckBox.IsChecked = false;

            if (appWin.default_path == true)
            {
                DefaultCheckBox.IsChecked = true;
                ChoiceCheckBox.IsChecked = false;

            }
            else
            {
                DefaultCheckBox.IsChecked = false;
                ChoiceCheckBox.IsChecked = true;
            }
              


                
        }

        /*class ImpostazioniCondivisione 
        {
            public string myPath;
            public string myImage;
        }*/

       public void Image_Loaded(object sender, RoutedEventArgs e)
        {
            Image img = sender as Image;
            //BitmapImage bitmapImage = new BitmapImage();
            // img.Width = bitmapImage.DecodePixelWidth = 80;
            // Natural px width of image source.
            // You don't need to set Height; the system maintains aspect ratio, and calculates the other
            // dimension, as long as one dimension measurement is provided.var uri = new System.Uri("c:\\foo");
            if (appWin.default_image == false)
                myPath=Pds.Properties.Settings.Default["ImagePath"].ToString();
            else
                myPath = "../../default_user_image.jpg";
            var uri = new System.Uri(Path.GetFullPath(myPath));
            var converted = uri.AbsoluteUri;
            Image.Source =new BitmapImage( new Uri(converted));

        }


            private void Sfoglia_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Seleziona una immagine";
            op.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "Portable Network Graphic (*.png)|*.png";
            if (op.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImagePath = op.FileName;
                string directoryPath = Path.GetDirectoryName(ImagePath);
            }


        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DefaultCheckBox.IsChecked == true)
            {
                appWin.default_path = true;
            }

            else
            {
                appWin.default_path = false;

            }

            if (AutoCheckBox.IsChecked == true)
            {
                Properties.Settings.Default["AutoActivated"] = "YES";
                System.Console.WriteLine("ricevuto : YES");
            }
            else
            {
                Properties.Settings.Default["AutoActivated"] = "NO";
                System.Console.WriteLine("ricevuto : NO");
            }

            if (!ImagePath.Equals(string.Empty))
            {
                appWin.default_image = false;
                Properties.Settings.Default["ImagePath"] = ImagePath;
            }
           
            Properties.Settings.Default.Save();
            
            Hide();
        }
    }

   
}


