using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Pds
{
    public class User
    {
        public string Name { get ; set ; }
        public string IPAddress { get; set; }
        public BitmapSource Image { get; set; }
        public DateTime time { get; set; } //time dell'invio dell'ultimo pacchetto di annuncia presenza

       
        public bool IsConnected() //TRUE = se l'ultimo pacchetto dell'utente è arrivato prima di 15 secondi
        {
            TimeSpan diff =DateTime.Now - time;
            TimeSpan baseInterval = new TimeSpan(0, 0, 15);
            if ( TimeSpan.Compare(diff,baseInterval)==1)
                return false;
            else
                return true;
        }
    }
}
