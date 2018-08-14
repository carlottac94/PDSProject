using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Pds
{
    class PathManager //classe che gestisce la ricezione del path attraverso la pipe
    {
       //Metodo statico= accessibile senza creare un'istanza della classe     
        public static String ReceivePath()
        {
            string Path = null;
            NamedPipeServerStream pipe = new NamedPipeServerStream("pipesendpath", PipeDirection.InOut);

            try
            {
                pipe.WaitForConnection();
                StreamReader reader = new StreamReader(pipe);

                Path = reader.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Errore nella ricezione path del file/cartella da inviare : " + e);
            }

            pipe.Close();
            return Path;
        }
        
        
       
    }
}
